using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using Sirh.Api.Infrastructure;
using Sirh.Api.Middleware;
using Sirh.BuildingBlocks.Application.Abstractions;
using Sirh.BuildingBlocks.Infrastructure;
using Sirh.Modules.Identity.Infrastructure;
using Sirh.Modules.Identity.Infrastructure.Persistence;
using Sirh.Modules.Identity.Presentation;
using Sirh.Modules.Organization.Infrastructure;
using Sirh.Modules.Organization.Infrastructure.Persistence;
using Sirh.Modules.Organization.Presentation;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddBuildingBlocksInfrastructure(builder.Configuration);
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddOrganizationModule(builder.Configuration);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too Many Requests",
            Detail = "Rate limit exceeded. Please retry later.",
            Instance = context.HttpContext.Request.Path
        };
        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

        var problemDetailsService = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context.HttpContext,
            ProblemDetails = problem
        });
    };

    options.AddPolicy(AuthEndpoints.AuthRateLimitPolicy, httpContext =>
    {
        var config = httpContext.RequestServices.GetRequiredService<IConfiguration>();
        var authPermitLimit = config.GetValue("RateLimiting:AuthPermitLimit", 10);
        var authWindowSeconds = config.GetValue("RateLimiting:AuthWindowSeconds", 60);
        var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "local-client";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = authPermitLimit,
                Window = TimeSpan.FromSeconds(authWindowSeconds),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()
                .WithExposedHeaders("Content-Disposition", CorrelationIdMiddleware.HeaderName);
        }
    });
});

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Identity:AutoMigrate"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await db.Database.MigrateAsync();
}

if (app.Configuration.GetValue<bool>("Identity:SeedOnStartup"))
{
    await app.Services.SeedIdentityAsync();
}

if (app.Configuration.GetValue<bool>("Organization:AutoMigrate"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var orgDb = scope.ServiceProvider.GetRequiredService<OrganizationDbContext>();
    await orgDb.Database.MigrateAsync();
}

if (app.Configuration.GetValue<bool>("Organization:SeedOnStartup"))
{
    await app.Services.SeedOrganizationAsync();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseSerilogRequestLogging();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseCors("FrontendPolicy");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("SIRH API v1")
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    }).AllowAnonymous();
}

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                durationMs = entry.Value.Duration.TotalMilliseconds
            })
        };
        await context.Response.WriteAsJsonAsync(payload);
    }
}).AllowAnonymous();

app.MapIdentityEndpoints();
app.MapOrganizationEndpoints();

app.Run();

public partial class Program;
