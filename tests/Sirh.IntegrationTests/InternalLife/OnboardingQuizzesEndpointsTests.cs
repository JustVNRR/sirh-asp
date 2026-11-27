using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Shouldly;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.Modules.HumanResources.Application.DTOs;
using Sirh.Modules.Identity.Application.DTOs;
using Sirh.Modules.Identity.Domain.Constants;
using Sirh.Modules.InternalLife.Application.DTOs;
using Sirh.Modules.InternalLife.Domain.Enums;
using Sirh.Modules.Organization.Application.DTOs;
using Xunit;

namespace Sirh.IntegrationTests.InternalLife;

public sealed class OnboardingQuizzesEndpointsTests
{
    private const string AdminEmail = "admin.rh@nova-conseil.fr";
    private const string EmployeeEmail = "employe.rh@nova-conseil.fr";
    private const string DemoPassword = "SirhDemo123!";
    private const string UserPassword = "NewEmployee123!";

    private static readonly JsonSerializerOptions ApiJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    [Fact]
    public async Task Hr_Should_Author_Validate_Publish_And_Hide_AnswerKeys_From_Employees()
    {
        using var factory = new ApiWebApplicationFactory();
        using var adminClient = await CreateAuthenticatedClientAsync(factory, AdminEmail, DemoPassword);
        using var empClient = await CreateAuthenticatedClientAsync(factory, EmployeeEmail, DemoPassword);

        var companyId = (await adminClient.GetFromJsonAsync<PagedResult<CompanyDto>>("/api/v1/companies"))!.Items.First().Id;

        // 1. Standard employee cannot author quizzes (403)
        var forbiddenCreate = await empClient.PostAsJsonAsync(
            "/api/v1/onboarding-quizzes",
            new CreateOnboardingQuizRequest(companyId, "Forbidden", null, 70, []));
        forbiddenCreate.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // 2. HR creates a draft quiz with a question missing any correct choice -> 201 Created, but publish returns 400
        var draftCreate = await adminClient.PostAsJsonAsync(
            "/api/v1/onboarding-quizzes",
            new CreateOnboardingQuizRequest(
                companyId,
                "Quiz Onboarding Incomplet",
                "Version brouillon",
                75,
                [
                    new QuizQuestionInput(
                        "Question sans bonne réponse ?",
                        QuizQuestionType.SingleChoice,
                        null,
                        [
                            new QuizChoiceInput("Choix A", false),
                            new QuizChoiceInput("Choix B", false)
                        ])
                ]));
        draftCreate.StatusCode.ShouldBe(HttpStatusCode.Created);
        var draftQuiz = (await draftCreate.Content.ReadFromJsonAsync<OnboardingQuizDetailDto>(ApiJsonOptions))!;
        draftQuiz.IsPublished.ShouldBeFalse();
        draftQuiz.Version.ShouldBe(1);

        var invalidPublish = await adminClient.PatchAsJsonAsync(
            $"/api/v1/onboarding-quizzes/{draftQuiz.Id}/publish",
            new PublishOnboardingQuizRequest(true, draftQuiz.RowVersion));
        invalidPublish.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // 3. Stale RowVersion on PUT returns 409; valid PUT fixes quiz, increments Version, and allows Publish
        var validQuestions = new[]
        {
            new QuizQuestionInput(
                "Quelle est la convention collective appliquée ?",
                QuizQuestionType.SingleChoice,
                "Syntec est la convention de l'entreprise.",
                [
                    new QuizChoiceInput("Syntec", true),
                    new QuizChoiceInput("Métallurgie", false)
                ])
        };

        var stalePut = await adminClient.PutAsJsonAsync(
            $"/api/v1/onboarding-quizzes/{draftQuiz.Id}",
            new UpdateOnboardingQuizRequest(
                companyId,
                "Quiz Stale",
                null,
                75,
                validQuestions,
                Convert.ToBase64String([9, 9, 9])));
        stalePut.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var validPut = await adminClient.PutAsJsonAsync(
            $"/api/v1/onboarding-quizzes/{draftQuiz.Id}",
            new UpdateOnboardingQuizRequest(
                companyId,
                "Quiz Onboarding Syntec",
                "Quiz officiel",
                80,
                validQuestions,
                draftQuiz.RowVersion));
        validPut.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updatedQuiz = (await validPut.Content.ReadFromJsonAsync<OnboardingQuizDetailDto>(ApiJsonOptions))!;
        updatedQuiz.Version.ShouldBe(2);

        var publishResp = await adminClient.PatchAsJsonAsync(
            $"/api/v1/onboarding-quizzes/{draftQuiz.Id}/publish",
            new PublishOnboardingQuizRequest(true, updatedQuiz.RowVersion));
        publishResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var publishedQuiz = (await publishResp.Content.ReadFromJsonAsync<OnboardingQuizDetailDto>(ApiJsonOptions))!;
        publishedQuiz.IsPublished.ShouldBeTrue();

        // 4. Employee sees published quiz without IsCorrect or Explanation
        var empList = await empClient.GetFromJsonAsync<PagedResult<OnboardingQuizSummaryDto>>(
            "/api/v1/onboarding-quizzes",
            ApiJsonOptions);
        empList.ShouldNotBeNull();
        empList.Items.Select(q => q.Id).ShouldContain(draftQuiz.Id);

        var empDetail = await empClient.GetFromJsonAsync<OnboardingQuizDetailDto>(
            $"/api/v1/onboarding-quizzes/{draftQuiz.Id}",
            ApiJsonOptions);
        empDetail.ShouldNotBeNull();
        empDetail.Questions.Single().Explanation.ShouldBeNull();
        empDetail.Questions.Single().Choices.ShouldAllBe(c => c.IsCorrect == null);
    }

    [Fact]
    public async Task Employee_Should_Start_Attempt_Without_Answers_Submit_Score_Block_Resubmit_And_Hr_Read_Results()
    {
        using var factory = new ApiWebApplicationFactory();
        using var adminClient = await CreateAuthenticatedClientAsync(factory, AdminEmail, DemoPassword);

        var companyId = (await adminClient.GetFromJsonAsync<PagedResult<CompanyDto>>("/api/v1/companies"))!.Items.First().Id;
        var empA = await CreateEmployeeAsync(adminClient, companyId, "Mme", "Clara", "QuizA", [IdentityRoles.Employee]);
        var empB = await CreateEmployeeAsync(adminClient, companyId, "M", "David", "QuizB", [IdentityRoles.Employee]);

        // 1. HR creates and publishes a 2-question quiz (PassingScorePercent = 50)
        var createResp = await adminClient.PostAsJsonAsync(
            "/api/v1/onboarding-quizzes",
            new CreateOnboardingQuizRequest(
                companyId,
                "Sécurité & Règlement Intérieur",
                "Évaluation d'arrivée",
                50,
                [
                    new QuizQuestionInput(
                        "Le port du badge est-il obligatoire ?",
                        QuizQuestionType.SingleChoice,
                        "Le badge nominatif est obligatoire sur site.",
                        [
                            new QuizChoiceInput("Oui", true),
                            new QuizChoiceInput("Non", false)
                        ]),
                    new QuizQuestionInput(
                        "Quels canaux utiliser pour une urgence informatique ?",
                        QuizQuestionType.MultipleChoice,
                        "Portail IT et hotline interne.",
                        [
                            new QuizChoiceInput("Portail IT", true),
                            new QuizChoiceInput("Hotline interne", true),
                            new QuizChoiceInput("Réseaux sociaux", false)
                        ])
                ]));
        createResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var quiz = (await createResp.Content.ReadFromJsonAsync<OnboardingQuizDetailDto>(ApiJsonOptions))!;

        var publishResp = await adminClient.PatchAsJsonAsync(
            $"/api/v1/onboarding-quizzes/{quiz.Id}/publish",
            new PublishOnboardingQuizRequest(true, quiz.RowVersion));
        publishResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        var q1 = quiz.Questions.Single(q => q.OrderIndex == 1);
        var q1Correct = q1.Choices.Single(c => c.IsCorrect == true).Id;
        var q2 = quiz.Questions.Single(q => q.OrderIndex == 2);
        var q2Wrong = q2.Choices.Single(c => c.IsCorrect == false).Id;

        // 2. Employee A starts an attempt -> JSON response MUST NOT leak "isCorrect"
        using var empAClient = await CreateAuthenticatedClientAsync(factory, empA.Email, UserPassword);
        var startResp = await empAClient.PostAsync($"/api/v1/onboarding-quizzes/{quiz.Id}/attempts", null);
        startResp.StatusCode.ShouldBe(HttpStatusCode.Created);

        var rawAttemptJson = await startResp.Content.ReadAsStringAsync();
        rawAttemptJson.ShouldNotContain("isCorrect", Case.Insensitive);

        var startedAttempt = JsonSerializer.Deserialize<StartedQuizAttemptDto>(rawAttemptJson, ApiJsonOptions)!;
        startedAttempt.Questions.Count.ShouldBe(2);

        // 3. Employee B cannot submit Employee A's attempt (403 Forbidden)
        using var empBClient = await CreateAuthenticatedClientAsync(factory, empB.Email, UserPassword);
        var hijackSubmit = await empBClient.PostAsJsonAsync(
            $"/api/v1/onboarding-quizzes/attempts/{startedAttempt.AttemptId}/submit",
            new SubmitQuizAttemptRequest([new SubmitQuestionAnswerRequest(q1.Id, [q1Correct])]));
        hijackSubmit.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // 4. Employee A submits 1 correct + 1 wrong answer -> 50% >= 50% -> Passed = true with corrections
        var submitResp = await empAClient.PostAsJsonAsync(
            $"/api/v1/onboarding-quizzes/attempts/{startedAttempt.AttemptId}/submit",
            new SubmitQuizAttemptRequest(
            [
                new SubmitQuestionAnswerRequest(q1.Id, [q1Correct]),
                new SubmitQuestionAnswerRequest(q2.Id, [q2Wrong])
            ]));
        submitResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = (await submitResp.Content.ReadFromJsonAsync<QuizAttemptResultDto>(ApiJsonOptions))!;
        result.Status.ShouldBe(QuizAttemptStatus.Completed);
        result.CorrectAnswersCount.ShouldBe(1);
        result.TotalQuestionsCount.ShouldBe(2);
        result.ScorePercent.ShouldBe(50);
        result.Passed.ShouldBeTrue();
        result.Corrections.Count.ShouldBe(2);
        result.Corrections.Single(c => c.QuestionId == q1.Id).IsCorrect.ShouldBeTrue();
        result.Corrections.Single(c => c.QuestionId == q2.Id).IsCorrect.ShouldBeFalse();
        result.Corrections.Single(c => c.QuestionId == q2.Id).Explanation.ShouldBe("Portail IT et hotline interne.");

        // 5. Re-submission on completed attempt returns 409 Conflict
        var resubmitResp = await empAClient.PostAsJsonAsync(
            $"/api/v1/onboarding-quizzes/attempts/{startedAttempt.AttemptId}/submit",
            new SubmitQuizAttemptRequest([new SubmitQuestionAnswerRequest(q1.Id, [q1Correct])]));
        resubmitResp.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // 6. Employee A reads own attempts & sees score in quiz summary; cannot query empB's attempts (403)
        var myAttempts = await empAClient.GetFromJsonAsync<PagedResult<QuizAttemptSummaryDto>>(
            $"/api/v1/onboarding-quizzes/{quiz.Id}/attempts",
            ApiJsonOptions);
        myAttempts.ShouldNotBeNull();
        myAttempts.TotalCount.ShouldBe(1);
        myAttempts.Items.Single().ScorePercent.ShouldBe(50);

        (await empAClient.GetAsync($"/api/v1/onboarding-quizzes/{quiz.Id}/attempts?employeeId={empB.Id}"))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var empQuizzes = await empAClient.GetFromJsonAsync<PagedResult<OnboardingQuizSummaryDto>>(
            "/api/v1/onboarding-quizzes",
            ApiJsonOptions);
        var mySummary = empQuizzes!.Items.Single(q => q.Id == quiz.Id);
        mySummary.MyStatus.ShouldBe("Completed");
        mySummary.MyBestScorePercent.ShouldBe(50);
        mySummary.MyHasPassed.ShouldBe(true);

        // 7. HR consults all quiz attempts across employees
        var hrAttempts = await adminClient.GetFromJsonAsync<PagedResult<QuizAttemptSummaryDto>>(
            $"/api/v1/onboarding-quizzes/{quiz.Id}/attempts",
            ApiJsonOptions);
        hrAttempts.ShouldNotBeNull();
        hrAttempts.TotalCount.ShouldBe(1);
        hrAttempts.Items.Single().EmployeeId.ShouldBe(empA.Id);
        hrAttempts.Items.Single().Passed.ShouldBeTrue();
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

    private static async Task<EmployeeDetailDto> CreateEmployeeAsync(
        HttpClient adminClient,
        Guid companyId,
        string honorific,
        string firstName,
        string lastName,
        IReadOnlyList<string> roles)
    {
        var response = await adminClient.PostAsJsonAsync(
            "/api/v1/employees",
            new CreateEmployeeRequest(
                companyId,
                null,
                honorific,
                firstName,
                lastName,
                UserPassword,
                roles));
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<EmployeeDetailDto>())!;
    }
}
