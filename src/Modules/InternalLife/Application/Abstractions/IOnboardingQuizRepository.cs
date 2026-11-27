using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.Modules.InternalLife.Domain.Entities;

namespace Sirh.Modules.InternalLife.Application.Abstractions;

public interface IOnboardingQuizRepository
{
    Task<OnboardingQuiz?> GetByIdWithQuestionsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<OnboardingQuiz>> GetPagedAsync(
        Guid? companyId,
        bool? isPublished,
        bool includeUniversalCompany,
        string? search,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, IReadOnlyList<QuizAttempt>>> GetAttemptsByQuizIdsForEmployeeAsync(
        IReadOnlyCollection<Guid> quizIds,
        Guid employeeId,
        CancellationToken cancellationToken = default);

    Task<QuizAttempt?> GetAttemptWithQuizAndQuestionsAsync(
        Guid attemptId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<QuizAttempt>> GetAttemptsPagedAsync(
        Guid quizId,
        Guid? employeeId,
        bool? passed,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default);

    void ReplaceQuestions(OnboardingQuiz quiz, IReadOnlyCollection<QuizQuestion> newQuestions);

    Task AddAsync(OnboardingQuiz quiz, CancellationToken cancellationToken = default);

    Task AddAttemptAsync(QuizAttempt attempt, CancellationToken cancellationToken = default);

    void AddAttemptAnswers(IReadOnlyCollection<QuizAttemptAnswer> answers);

    void Remove(OnboardingQuiz quiz);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
