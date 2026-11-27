using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.InternalLife.Application.DTOs;

namespace Sirh.Modules.InternalLife.Application.Abstractions;

public interface IOnboardingQuizService
{
    Task<Result<PagedResult<OnboardingQuizSummaryDto>>> GetPagedAsync(
        Guid? companyId,
        bool? published,
        string? search,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default);

    Task<Result<OnboardingQuizDetailDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<OnboardingQuizDetailDto>> CreateAsync(
        CreateOnboardingQuizRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<OnboardingQuizDetailDto>> UpdateAsync(
        Guid id,
        UpdateOnboardingQuizRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<OnboardingQuizDetailDto>> SetPublishedAsync(
        Guid id,
        PublishOnboardingQuizRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<StartedQuizAttemptDto>> StartAttemptAsync(Guid quizId, CancellationToken cancellationToken = default);

    Task<Result<QuizAttemptResultDto>> SubmitAttemptAsync(
        Guid attemptId,
        SubmitQuizAttemptRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PagedResult<QuizAttemptSummaryDto>>> GetAttemptsAsync(
        Guid quizId,
        Guid? employeeId,
        bool? passed,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default);
}
