using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.Modules.Health.Application.Abstractions;
using Sirh.Modules.Health.Application.DTOs;
using Sirh.Modules.Health.Domain.Entities;
using Sirh.Modules.Health.Domain.Enums;
using Sirh.Modules.Health.Infrastructure.Persistence;
using Sirh.Modules.HumanResources.Application.DTOs;
using Sirh.Modules.Identity.Application.DTOs;
using Sirh.Modules.Identity.Domain.Constants;
using Sirh.Modules.Organization.Application.DTOs;
using Xunit;

namespace Sirh.IntegrationTests.Health;

public sealed class NotificationsAndRemindersTests
{
    private const string AdminEmail = "admin.rh@nova-conseil.fr";
    private const string DemoPassword = "SirhDemo123!";
    private const string UserPassword = "NewEmployee123!";

    private static readonly JsonSerializerOptions ApiJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static readonly byte[] MinimalPdfBytes =
        Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\ntrailer\n<<>>\n%%EOF\n");

    [Fact]
    public async Task Notifications_Endpoints_Should_Enforce_Ownership_And_Sync_UnreadCount_With_AuthMe()
    {
        using var factory = new ApiWebApplicationFactory();
        using var adminClient = await CreateAuthenticatedClientAsync(factory, AdminEmail, DemoPassword);

        var companyId = (await adminClient.GetFromJsonAsync<PagedResult<CompanyDto>>("/api/v1/companies"))!.Items.First().Id;
        var empA = await CreateEmployeeAsync(adminClient, companyId, "Mme", "Alice", "NotifA", [IdentityRoles.Employee]);
        var empB = await CreateEmployeeAsync(adminClient, companyId, "M", "Bruno", "NotifB", [IdentityRoles.Employee]);

        Guid notifA1Id;
        Guid notifBId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HealthDbContext>();
            var notifA1 = new Notification(empA.Id, NotificationCategory.System, "Test", null, "Info A1", "Contenu A1");
            var notifA2 = new Notification(empA.Id, NotificationCategory.MedicalVisitReminder, "MedicalVisit", Guid.NewGuid(), "Info A2", "Contenu A2");
            var notifB = new Notification(empB.Id, NotificationCategory.System, "Test", null, "Info B", "Contenu B");

            db.Notifications.AddRange(notifA1, notifA2, notifB);
            await db.SaveChangesAsync();

            notifA1Id = notifA1.Id;
            notifBId = notifB.Id;
        }

        using var empAClient = await CreateAuthenticatedClientAsync(factory, empA.Email, UserPassword);

        // 1. Unread count = 2 on both /notifications/unread-count and /auth/me
        var unreadDto = await empAClient.GetFromJsonAsync<UnreadCountDto>("/api/v1/notifications/unread-count");
        unreadDto.ShouldNotBeNull();
        unreadDto.UnreadCount.ShouldBe(2);

        var meBefore = await empAClient.GetFromJsonAsync<CurrentUserProfileResponse>("/api/v1/auth/me");
        meBefore.ShouldNotBeNull();
        meBefore.UnreadNotificationsCount.ShouldBe(2);

        var listUnread = await empAClient.GetFromJsonAsync<PagedResult<NotificationDto>>(
            "/api/v1/notifications?isRead=false",
            ApiJsonOptions);
        listUnread.ShouldNotBeNull();
        listUnread.TotalCount.ShouldBe(2);

        // 2. Anti-IDOR: A cannot mark or delete B's notification (403 Forbidden)
        (await empAClient.PatchAsync($"/api/v1/notifications/{notifBId}/read", null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await empAClient.DeleteAsync($"/api/v1/notifications/{notifBId}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // 3. Mark one as read -> count becomes 1
        (await empAClient.PatchAsync($"/api/v1/notifications/{notifA1Id}/read", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await empAClient.GetFromJsonAsync<UnreadCountDto>("/api/v1/notifications/unread-count"))!.UnreadCount.ShouldBe(1);

        // 4. Mark all as read -> count becomes 0 on both endpoints
        (await empAClient.PostAsync("/api/v1/notifications/read-all", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await empAClient.GetFromJsonAsync<UnreadCountDto>("/api/v1/notifications/unread-count"))!.UnreadCount.ShouldBe(0);
        (await empAClient.GetFromJsonAsync<CurrentUserProfileResponse>("/api/v1/auth/me"))!.UnreadNotificationsCount.ShouldBe(0);

        // 5. Delete own notification
        (await empAClient.DeleteAsync($"/api/v1/notifications/{notifA1Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var listAfterDelete = await empAClient.GetFromJsonAsync<PagedResult<NotificationDto>>(
            "/api/v1/notifications",
            ApiJsonOptions);
        listAfterDelete.ShouldNotBeNull();
        listAfterDelete.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task DailyHealthRemindersJob_Should_Alert_FutureVisits_And_ExpiringDocuments_Idempotently()
    {
        using var factory = new ApiWebApplicationFactory();
        using var adminClient = await CreateAuthenticatedClientAsync(factory, AdminEmail, DemoPassword);

        var companyId = (await adminClient.GetFromJsonAsync<PagedResult<CompanyDto>>("/api/v1/companies"))!.Items.First().Id;
        var hrEmp = await CreateEmployeeAsync(adminClient, companyId, null, "Mme", "Laura", "RessourcesHumaines", [IdentityRoles.Hr]);
        var empA = await CreateEmployeeAsync(adminClient, companyId, "M", "Mehdi", "RappelTest", [IdentityRoles.Employee]);

        var nowUtc = DateTime.UtcNow;
        var todayUtc = DateOnly.FromDateTime(nowUtc);

        // Create 3 medical visits: +3 days (due), -2 days (past - must ignore), +15 days (too far - must ignore)
        var dueVisit = await CreateMedicalVisitAsync(adminClient, empA.Id, nowUtc.AddDays(3), "Centre Due");
        var pastVisit = await CreateMedicalVisitAsync(adminClient, empA.Id, nowUtc.AddDays(-2), "Centre Past");
        var farVisit = await CreateMedicalVisitAsync(adminClient, empA.Id, nowUtc.AddDays(15), "Centre Far");

        // Create RQTH expiring in 20 days (<= 30j) and ResidentPermit expiring in 5 days (<= 7j)
        await CreateSpecialDocumentAsync(
            adminClient,
            empA.Id,
            "RqthCertificate",
            todayUtc.AddDays(20).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            permitNumber: null);

        await CreateSpecialDocumentAsync(
            adminClient,
            empA.Id,
            "ResidentPermit",
            todayUtc.AddDays(5).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            permitNumber: "75XYZ9999");

        // 1. Run daily job
        ReminderRunSummary firstRun;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<IDailyHealthRemindersJob>();
            firstRun = await job.RunAsync();
        }

        firstRun.MedicalVisitRemindersSent.ShouldBe(1);
        firstRun.DocumentExpiryAlerts30DaysSent.ShouldBe(1);
        firstRun.DocumentExpiryAlerts7DaysSent.ShouldBe(1);
        firstRun.NotificationsCreated.ShouldBe(5); // 1 visit (empA) + 2 doc alerts * 2 recipients (empA + hrEmp)
        firstRun.EmailsSent.ShouldBe(5);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HealthDbContext>();
            (await db.MedicalVisits.SingleAsync(v => v.Id == dueVisit.Id)).ReminderSentAtUtc.ShouldNotBeNull();
            (await db.MedicalVisits.SingleAsync(v => v.Id == pastVisit.Id)).ReminderSentAtUtc.ShouldBeNull();
            (await db.MedicalVisits.SingleAsync(v => v.Id == farVisit.Id)).ReminderSentAtUtc.ShouldBeNull();
        }

        // Verify notifications visible via API for Employee A and HR Employee
        using var empAClient = await CreateAuthenticatedClientAsync(factory, empA.Email, UserPassword);
        (await empAClient.GetFromJsonAsync<UnreadCountDto>("/api/v1/notifications/unread-count"))!.UnreadCount.ShouldBe(3);

        var empNotifs = await empAClient.GetFromJsonAsync<PagedResult<NotificationDto>>(
            "/api/v1/notifications",
            ApiJsonOptions);
        empNotifs.ShouldNotBeNull();
        empNotifs.Items.Count(n => n.Category == NotificationCategory.MedicalVisitReminder).ShouldBe(1);
        empNotifs.Items.Count(n => n.Category == NotificationCategory.DocumentExpiryAlert).ShouldBe(2);

        using var hrClient = await CreateAuthenticatedClientAsync(factory, hrEmp.Email, UserPassword);
        var hrNotifs = await hrClient.GetFromJsonAsync<PagedResult<NotificationDto>>(
            "/api/v1/notifications?category=DocumentExpiryAlert",
            ApiJsonOptions);
        hrNotifs.ShouldNotBeNull();
        hrNotifs.TotalCount.ShouldBe(2);

        // Verify emails queued via IEmailService
        factory.EmailService.SentMessages.Count(m => m.To == empA.Email).ShouldBe(3);
        factory.EmailService.SentMessages.Count(m => m.To == hrEmp.Email).ShouldBe(2);

        // 2. Second run must be completely idempotent (0 reminders, 0 duplicates)
        ReminderRunSummary secondRun;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<IDailyHealthRemindersJob>();
            secondRun = await job.RunAsync();
        }

        secondRun.MedicalVisitRemindersSent.ShouldBe(0);
        secondRun.DocumentExpiryAlerts30DaysSent.ShouldBe(0);
        secondRun.DocumentExpiryAlerts7DaysSent.ShouldBe(0);
        secondRun.NotificationsCreated.ShouldBe(0);
        secondRun.EmailsSent.ShouldBe(0);
    }

    private static Task<EmployeeDetailDto> CreateEmployeeAsync(
        HttpClient adminClient,
        Guid companyId,
        string honorific,
        string firstName,
        string lastName,
        IReadOnlyList<string> roles) =>
        CreateEmployeeInternalAsync(adminClient, companyId, null, honorific, firstName, lastName, roles);

    private static Task<EmployeeDetailDto> CreateEmployeeAsync(
        HttpClient adminClient,
        Guid companyId,
        Guid? directManagerId,
        string honorific,
        string firstName,
        string lastName,
        IReadOnlyList<string> roles) =>
        CreateEmployeeInternalAsync(adminClient, companyId, directManagerId, honorific, firstName, lastName, roles);

    private static async Task<EmployeeDetailDto> CreateEmployeeInternalAsync(
        HttpClient adminClient,
        Guid companyId,
        Guid? directManagerId,
        string honorific,
        string firstName,
        string lastName,
        IReadOnlyList<string> roles)
    {
        var response = await adminClient.PostAsJsonAsync(
            "/api/v1/employees",
            new CreateEmployeeRequest(
                companyId,
                directManagerId,
                honorific,
                firstName,
                lastName,
                UserPassword,
                roles));
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<EmployeeDetailDto>())!;
    }

    private static async Task<MedicalVisitDto> CreateMedicalVisitAsync(
        HttpClient adminClient,
        Guid employeeId,
        DateTime visitDateTimeUtc,
        string medicalCenter)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(employeeId.ToString()), "employeeId");
        form.Add(new StringContent("FollowUp"), "visitType");
        form.Add(new StringContent(visitDateTimeUtc.ToString("O", CultureInfo.InvariantCulture)), "visitDateTimeUtc");
        form.Add(new StringContent(medicalCenter), "medicalCenter");
        form.Add(new StringContent("false"), "carriedOut");
        var convPart = new ByteArrayContent(MinimalPdfBytes);
        convPart.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(convPart, "convocationFile", "convoc.pdf");

        var resp = await adminClient.PostAsync("/api/v1/medical-visits", form);
        resp.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await resp.Content.ReadFromJsonAsync<MedicalVisitDto>(ApiJsonOptions))!;
    }

    private static async Task CreateSpecialDocumentAsync(
        HttpClient adminClient,
        Guid employeeId,
        string documentType,
        string expiryDate,
        string? permitNumber)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(employeeId.ToString()), "employeeId");
        form.Add(new StringContent(documentType), "documentType");
        form.Add(new StringContent(expiryDate), "expiryDate");
        if (!string.IsNullOrWhiteSpace(permitNumber))
        {
            form.Add(new StringContent(permitNumber), "permitNumber");
        }

        var filePart = new ByteArrayContent(MinimalPdfBytes);
        filePart.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(filePart, "file", $"{documentType.ToLowerInvariant()}.pdf");

        var resp = await adminClient.PostAsync("/api/v1/employee-documents", form);
        resp.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private static async Task<HttpClient> CreateAuthenticatedClientAsync(
        ApiWebApplicationFactory factory,
        string email,
        string password)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(email, password));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tokens = (await response.Content.ReadFromJsonAsync<AuthTokensResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return client;
    }
}
