using Microsoft.EntityFrameworkCore;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.Modules.InternalLife.Application.Abstractions;
using Sirh.Modules.InternalLife.Domain.Entities;

namespace Sirh.Modules.InternalLife.Infrastructure.Persistence;

public sealed class OnboardingQuizRepository(InternalLifeDbContext dbContext) : IOnboardingQuizRepository
{
    public Task<OnboardingQuiz?> GetByIdWithQuestionsAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.OnboardingQuizzes
            .Include(q => q.Questions)
            .ThenInclude(qn => qn.Choices)
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.OnboardingQuizzes.AsNoTracking().AnyAsync(q => q.Id == id, cancellationToken);

    public async Task<PagedResult<OnboardingQuiz>> GetPagedAsync(
        Guid? companyId,
        bool? isPublished,
        bool includeUniversalCompany,
        string? search,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.OnboardingQuizzes
            .AsNoTracking()
            .Include(q => q.Questions)
            .AsQueryable();

        if (companyId.HasValue)
        {
            query = includeUniversalCompany
                ? query.Where(q => q.CompanyId == null || q.CompanyId == companyId.Value)
                : query.Where(q => q.CompanyId == companyId.Value);
        }

        if (isPublished.HasValue)
        {
            query = query.Where(q => q.IsPublished == isPublished.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(q =>
                q.Title.ToLower().Contains(term) ||
                (q.Description != null && q.Description.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(q => q.Title)
            .Skip(pagedRequest.Skip)
            .Take(pagedRequest.NormalizedPageSize)
            .ToArrayAsync(cancellationToken);

        return PagedResult<OnboardingQuiz>.Create(
            items,
            totalCount,
            pagedRequest.NormalizedPage,
            pagedRequest.NormalizedPageSize);
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<QuizAttempt>>> GetAttemptsByQuizIdsForEmployeeAsync(
        IReadOnlyCollection<Guid> quizIds,
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        if (quizIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<QuizAttempt>>();
        }

        var rows = await dbContext.QuizAttempts
            .AsNoTracking()
            .Where(a => a.EmployeeId == employeeId && quizIds.Contains(a.QuizId))
            .ToArrayAsync(cancellationToken);

        return rows
            .GroupBy(a => a.QuizId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<QuizAttempt>)g.ToArray());
    }

    public Task<QuizAttempt?> GetAttemptWithQuizAndQuestionsAsync(
        Guid attemptId,
        CancellationToken cancellationToken = default) =>
        dbContext.QuizAttempts
            .Include(a => a.Answers)
            .Include(a => a.Quiz)
            .ThenInclude(q => q.Questions)
            .ThenInclude(qn => qn.Choices)
            .FirstOrDefaultAsync(a => a.Id == attemptId, cancellationToken);

    public async Task<PagedResult<QuizAttempt>> GetAttemptsPagedAsync(
        Guid quizId,
        Guid? employeeId,
        bool? passed,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.QuizAttempts
            .AsNoTracking()
            .Where(a => a.QuizId == quizId);

        if (employeeId.HasValue)
        {
            query = query.Where(a => a.EmployeeId == employeeId.Value);
        }

        if (passed.HasValue)
        {
            query = query.Where(a => a.Passed == passed.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(a => a.StartedAtUtc)
            .Skip(pagedRequest.Skip)
            .Take(pagedRequest.NormalizedPageSize)
            .ToArrayAsync(cancellationToken);

        return PagedResult<QuizAttempt>.Create(
            items,
            totalCount,
            pagedRequest.NormalizedPage,
            pagedRequest.NormalizedPageSize);
    }

    public void ReplaceQuestions(OnboardingQuiz quiz, IReadOnlyCollection<QuizQuestion> newQuestions)
    {
        dbContext.QuizQuestions.RemoveRange(quiz.Questions);
        quiz.Questions.Clear();

        foreach (var question in newQuestions)
        {
            quiz.Questions.Add(question);
            dbContext.QuizQuestions.Add(question);
        }
    }

    public async Task AddAsync(OnboardingQuiz quiz, CancellationToken cancellationToken = default) =>
        await dbContext.OnboardingQuizzes.AddAsync(quiz, cancellationToken);

    public async Task AddAttemptAsync(QuizAttempt attempt, CancellationToken cancellationToken = default) =>
        await dbContext.QuizAttempts.AddAsync(attempt, cancellationToken);

    public void AddAttemptAnswers(IReadOnlyCollection<QuizAttemptAnswer> answers) =>
        dbContext.QuizAttemptAnswers.AddRange(answers);

    public void Remove(OnboardingQuiz quiz) => dbContext.OnboardingQuizzes.Remove(quiz);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
