using System.Text.Json;
using Sirh.BuildingBlocks.Application.Abstractions;
using Sirh.BuildingBlocks.Application.Contracts;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.InternalLife.Application.Abstractions;
using Sirh.Modules.InternalLife.Application.DTOs;
using Sirh.Modules.InternalLife.Domain.Constants;
using Sirh.Modules.InternalLife.Domain.Entities;
using Sirh.Modules.InternalLife.Domain.Enums;
using Sirh.Modules.InternalLife.Domain.Services;

namespace Sirh.Modules.InternalLife.Application.Services;

public sealed class OnboardingQuizService(
    IOnboardingQuizRepository quizRepository,
    IOrganizationLookup organizationLookup,
    IHumanResourcesLookup humanResourcesLookup,
    IDateTimeProvider dateTimeProvider,
    ICurrentUser currentUser) : IOnboardingQuizService
{
    public async Task<Result<PagedResult<OnboardingQuizSummaryDto>>> GetPagedAsync(
        Guid? companyId,
        bool? published,
        string? search,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default)
    {
        var isAuthor = HasPermission(InternalLifePermissions.QuizzesWrite);

        Guid? effectiveCompanyId = companyId;
        bool? effectivePublished = published;
        var includeUniversalCompany = false;

        if (!isAuthor)
        {
            effectivePublished = true;
            includeUniversalCompany = true;

            if (currentUser.EmployeeId.HasValue)
            {
                var employeeCompanyId = await humanResourcesLookup.GetEmployeeCompanyIdAsync(
                    currentUser.EmployeeId.Value,
                    cancellationToken);

                if (employeeCompanyId.HasValue)
                {
                    if (companyId.HasValue && companyId.Value != employeeCompanyId.Value)
                    {
                        return Error.Forbidden(
                            "Quizzes.Forbidden",
                            "You can only view onboarding quizzes for your own company.");
                    }

                    effectiveCompanyId = employeeCompanyId.Value;
                }
            }
        }

        var page = await quizRepository.GetPagedAsync(
            effectiveCompanyId,
            effectivePublished,
            includeUniversalCompany,
            search,
            pagedRequest,
            cancellationToken);

        IReadOnlyDictionary<Guid, IReadOnlyList<QuizAttempt>> attemptsByQuiz = new Dictionary<Guid, IReadOnlyList<QuizAttempt>>();
        if (currentUser.EmployeeId.HasValue && page.Items.Count > 0)
        {
            attemptsByQuiz = await quizRepository.GetAttemptsByQuizIdsForEmployeeAsync(
                page.Items.Select(q => q.Id).ToArray(),
                currentUser.EmployeeId.Value,
                cancellationToken);
        }

        var mapped = page.Items.Select(quiz =>
        {
            attemptsByQuiz.TryGetValue(quiz.Id, out var attempts);
            return MapToSummaryDto(quiz, attempts);
        }).ToArray();

        return PagedResult<OnboardingQuizSummaryDto>.Create(mapped, page.TotalCount, page.Page, page.PageSize);
    }

    public async Task<Result<OnboardingQuizDetailDto>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var quiz = await quizRepository.GetByIdWithQuestionsAsync(id, cancellationToken);
        if (quiz is null)
        {
            return Error.NotFound("Quizzes.NotFound", $"Onboarding quiz '{id}' was not found.");
        }

        var isAuthor = HasPermission(InternalLifePermissions.QuizzesWrite);
        if (!isAuthor)
        {
            if (!quiz.IsPublished)
            {
                return Error.NotFound("Quizzes.NotFound", $"Onboarding quiz '{id}' was not found.");
            }

            if (quiz.CompanyId.HasValue && currentUser.EmployeeId.HasValue)
            {
                var employeeCompanyId = await humanResourcesLookup.GetEmployeeCompanyIdAsync(
                    currentUser.EmployeeId.Value,
                    cancellationToken);

                if (employeeCompanyId.HasValue && employeeCompanyId.Value != quiz.CompanyId.Value)
                {
                    return Error.Forbidden(
                        "Quizzes.Forbidden",
                        "You do not have access to this company's onboarding quiz.");
                }
            }
        }

        return MapToDetailDto(quiz, includeAnswerKey: isAuthor);
    }

    public async Task<Result<OnboardingQuizDetailDto>> CreateAsync(
        CreateOnboardingQuizRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateQuizInputAsync(
            request.CompanyId,
            request.Title,
            request.PassingScorePercent,
            request.Questions,
            cancellationToken);
        if (validation.IsFailure)
        {
            return validation.Error;
        }

        var quiz = new OnboardingQuiz(
            request.CompanyId,
            request.Title,
            request.Description,
            request.PassingScorePercent);

        PopulateQuestions(quiz, request.Questions);

        await quizRepository.AddAsync(quiz, cancellationToken);
        await quizRepository.SaveChangesAsync(cancellationToken);

        return MapToDetailDto(quiz, includeAnswerKey: true);
    }

    public async Task<Result<OnboardingQuizDetailDto>> UpdateAsync(
        Guid id,
        UpdateOnboardingQuizRequest request,
        CancellationToken cancellationToken = default)
    {
        var quiz = await quizRepository.GetByIdWithQuestionsAsync(id, cancellationToken);
        if (quiz is null)
        {
            return Error.NotFound("Quizzes.NotFound", $"Onboarding quiz '{id}' was not found.");
        }

        var rowVersionResult = InternalLifeConcurrencyHelper.ValidateRowVersion(request.RowVersion, quiz.RowVersion);
        if (rowVersionResult.IsFailure)
        {
            return rowVersionResult.Error;
        }

        var validation = await ValidateQuizInputAsync(
            request.CompanyId,
            request.Title,
            request.PassingScorePercent,
            request.Questions,
            cancellationToken);
        if (validation.IsFailure)
        {
            return validation.Error;
        }

        quiz.UpdateMetadata(
            request.CompanyId,
            request.Title,
            request.Description,
            request.PassingScorePercent);

        var newQuestions = BuildQuestionEntities(quiz.Id, request.Questions);
        quizRepository.ReplaceQuestions(quiz, newQuestions);

        if (quiz.IsPublished)
        {
            var publishValidation = quiz.ValidateForPublish();
            if (publishValidation.IsFailure)
            {
                return publishValidation.Error;
            }
        }

        await quizRepository.SaveChangesAsync(cancellationToken);
        return MapToDetailDto(quiz, includeAnswerKey: true);
    }

    public async Task<Result<OnboardingQuizDetailDto>> SetPublishedAsync(
        Guid id,
        PublishOnboardingQuizRequest request,
        CancellationToken cancellationToken = default)
    {
        var quiz = await quizRepository.GetByIdWithQuestionsAsync(id, cancellationToken);
        if (quiz is null)
        {
            return Error.NotFound("Quizzes.NotFound", $"Onboarding quiz '{id}' was not found.");
        }

        var rowVersionResult = InternalLifeConcurrencyHelper.ValidateRowVersion(request.RowVersion, quiz.RowVersion);
        if (rowVersionResult.IsFailure)
        {
            return rowVersionResult.Error;
        }

        if (request.IsPublished)
        {
            var publishValidation = quiz.ValidateForPublish();
            if (publishValidation.IsFailure)
            {
                return publishValidation.Error;
            }
        }

        quiz.SetPublished(request.IsPublished);
        await quizRepository.SaveChangesAsync(cancellationToken);

        return MapToDetailDto(quiz, includeAnswerKey: true);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var quiz = await quizRepository.GetByIdWithQuestionsAsync(id, cancellationToken);
        if (quiz is null)
        {
            return Result.Failure(Error.NotFound("Quizzes.NotFound", $"Onboarding quiz '{id}' was not found."));
        }

        quizRepository.Remove(quiz);
        await quizRepository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<StartedQuizAttemptDto>> StartAttemptAsync(
        Guid quizId,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.EmployeeId.HasValue || currentUser.EmployeeId.Value == Guid.Empty)
        {
            return Error.Forbidden(
                "Quizzes.Forbidden",
                "Your account must be linked to an employee profile to take an onboarding quiz.");
        }

        var quiz = await quizRepository.GetByIdWithQuestionsAsync(quizId, cancellationToken);
        if (quiz is null)
        {
            return Error.NotFound("Quizzes.NotFound", $"Onboarding quiz '{quizId}' was not found.");
        }

        if (!quiz.IsPublished)
        {
            return Error.Validation("Quizzes.NotPublished", "This onboarding quiz is not published yet.");
        }

        var employeeId = currentUser.EmployeeId.Value;
        if (quiz.CompanyId.HasValue)
        {
            var employeeCompanyId = await humanResourcesLookup.GetEmployeeCompanyIdAsync(employeeId, cancellationToken);
            if (!employeeCompanyId.HasValue || employeeCompanyId.Value != quiz.CompanyId.Value)
            {
                return Error.Forbidden(
                    "Quizzes.Forbidden",
                    "You cannot take an onboarding quiz belonging to another company.");
            }
        }

        var attempt = new QuizAttempt(quiz.Id, employeeId, quiz.Version, dateTimeProvider.UtcNow);
        await quizRepository.AddAttemptAsync(attempt, cancellationToken);
        await quizRepository.SaveChangesAsync(cancellationToken);

        var questions = quiz.Questions
            .OrderBy(q => q.OrderIndex)
            .Select(q => new QuizAttemptQuestionDto(
                q.Id,
                q.OrderIndex,
                q.QuestionText,
                q.QuestionType,
                q.Choices
                    .OrderBy(c => c.OrderIndex)
                    .Select(c => new QuizAttemptChoiceDto(c.Id, c.OrderIndex, c.ChoiceText))
                    .ToArray()))
            .ToArray();

        return new StartedQuizAttemptDto(
            attempt.Id,
            quiz.Id,
            quiz.Title,
            attempt.QuizVersion,
            quiz.PassingScorePercent,
            attempt.StartedAtUtc,
            questions);
    }

    public async Task<Result<QuizAttemptResultDto>> SubmitAttemptAsync(
        Guid attemptId,
        SubmitQuizAttemptRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.EmployeeId.HasValue || currentUser.EmployeeId.Value == Guid.Empty)
        {
            return Error.Forbidden(
                "Quizzes.Forbidden",
                "Your account must be linked to an employee profile to submit a quiz attempt.");
        }

        var attempt = await quizRepository.GetAttemptWithQuizAndQuestionsAsync(attemptId, cancellationToken);
        if (attempt is null)
        {
            return Error.NotFound("Quizzes.AttemptNotFound", $"Quiz attempt '{attemptId}' was not found.");
        }

        if (attempt.EmployeeId != currentUser.EmployeeId.Value)
        {
            return Error.Forbidden("Quizzes.Forbidden", "You can only submit your own quiz attempt.");
        }

        if (attempt.Status == QuizAttemptStatus.Completed)
        {
            return Error.Conflict(
                "Quizzes.AttemptAlreadyCompleted",
                $"Quiz attempt '{attemptId}' has already been submitted.");
        }

        var answersMap = new Dictionary<Guid, IReadOnlyCollection<Guid>>();
        if (request.Answers is not null)
        {
            foreach (var answer in request.Answers)
            {
                answersMap[answer.QuestionId] = answer.SelectedChoiceIds ?? [];
            }
        }

        var evalResult = QuizScoringCalculator.Evaluate(
            attempt.Quiz.Questions.ToArray(),
            attempt.Quiz.PassingScorePercent,
            answersMap);

        if (evalResult.IsFailure)
        {
            return evalResult.Error;
        }

        var evaluation = evalResult.Value;
        foreach (var q in evaluation.Questions)
        {
            attempt.AddAnswer(q.QuestionId, JsonSerializer.Serialize(q.SelectedChoiceIds), q.IsCorrect);
        }

        quizRepository.AddAttemptAnswers(attempt.Answers.ToArray());

        attempt.Complete(
            evaluation.CorrectCount,
            evaluation.TotalQuestions,
            attempt.Quiz.PassingScorePercent,
            dateTimeProvider.UtcNow);

        await quizRepository.SaveChangesAsync(cancellationToken);

        var corrections = evaluation.Questions
            .Select(q => new QuizQuestionCorrectionDto(
                q.QuestionId,
                q.QuestionText,
                q.SelectedChoiceIds,
                q.CorrectChoiceIds,
                q.IsCorrect,
                q.Explanation))
            .ToArray();

        return new QuizAttemptResultDto(
            attempt.Id,
            attempt.QuizId,
            attempt.EmployeeId,
            attempt.QuizVersion,
            attempt.Status,
            attempt.CorrectAnswersCount,
            attempt.TotalQuestionsCount,
            attempt.ScorePercent,
            attempt.Passed,
            attempt.StartedAtUtc,
            attempt.CompletedAtUtc!.Value,
            corrections);
    }

    public async Task<Result<PagedResult<QuizAttemptSummaryDto>>> GetAttemptsAsync(
        Guid quizId,
        Guid? employeeId,
        bool? passed,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default)
    {
        if (!await quizRepository.ExistsAsync(quizId, cancellationToken))
        {
            return Error.NotFound("Quizzes.NotFound", $"Onboarding quiz '{quizId}' was not found.");
        }

        var canReadAll = HasPermission(InternalLifePermissions.QuizzesResultsAll);
        var effectiveEmployeeId = employeeId;

        if (!canReadAll)
        {
            if (!currentUser.EmployeeId.HasValue || currentUser.EmployeeId.Value == Guid.Empty)
            {
                return Error.Forbidden(
                    "Quizzes.Forbidden",
                    "You do not have permission to view quiz attempts.");
            }

            if (employeeId.HasValue && employeeId.Value != currentUser.EmployeeId.Value)
            {
                return Error.Forbidden("Quizzes.Forbidden", "You can only view your own quiz attempts.");
            }

            effectiveEmployeeId = currentUser.EmployeeId.Value;
        }

        var page = await quizRepository.GetAttemptsPagedAsync(
            quizId,
            effectiveEmployeeId,
            passed,
            pagedRequest,
            cancellationToken);

        var mapped = page.Items.Select(a => new QuizAttemptSummaryDto(
            a.Id,
            a.QuizId,
            a.EmployeeId,
            a.QuizVersion,
            a.Status,
            a.CorrectAnswersCount,
            a.TotalQuestionsCount,
            a.ScorePercent,
            a.Passed,
            a.StartedAtUtc,
            a.CompletedAtUtc)).ToArray();

        return PagedResult<QuizAttemptSummaryDto>.Create(mapped, page.TotalCount, page.Page, page.PageSize);
    }

    private async Task<Result> ValidateQuizInputAsync(
        Guid? companyId,
        string title,
        int passingScorePercent,
        IReadOnlyList<QuizQuestionInput>? questions,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Result.Failure(Error.Validation("Quizzes.TitleRequired", "Title is required."));
        }

        if (passingScorePercent is < 1 or > 100)
        {
            return Result.Failure(Error.Validation(
                "Quizzes.InvalidPassingScore",
                "PassingScorePercent must be between 1 and 100."));
        }

        if (questions is not null)
        {
            foreach (var q in questions)
            {
                if (string.IsNullOrWhiteSpace(q.QuestionText))
                {
                    return Result.Failure(Error.Validation("Quizzes.QuestionTextRequired", "QuestionText is required."));
                }

                if (!Enum.IsDefined(q.QuestionType))
                {
                    return Result.Failure(Error.Validation("Quizzes.InvalidQuestionType", $"Unsupported question type '{q.QuestionType}'."));
                }

                if (q.Choices is not null && q.Choices.Any(c => string.IsNullOrWhiteSpace(c.ChoiceText)))
                {
                    return Result.Failure(Error.Validation("Quizzes.ChoiceTextRequired", "ChoiceText is required for every choice."));
                }
            }
        }

        if (companyId.HasValue &&
            await organizationLookup.GetCompanyByIdAsync(companyId.Value, cancellationToken) is null)
        {
            return Result.Failure(Error.NotFound("Companies.NotFound", $"Company '{companyId.Value}' was not found."));
        }

        return Result.Success();
    }

    private static void PopulateQuestions(OnboardingQuiz quiz, IReadOnlyList<QuizQuestionInput>? inputs)
    {
        if (inputs is null)
        {
            return;
        }

        for (var qIndex = 0; qIndex < inputs.Count; qIndex++)
        {
            var qInput = inputs[qIndex];
            var question = quiz.AddQuestion(
                qInput.OrderIndex ?? qIndex + 1,
                qInput.QuestionText,
                qInput.QuestionType,
                qInput.Explanation);

            if (qInput.Choices is null)
            {
                continue;
            }

            for (var cIndex = 0; cIndex < qInput.Choices.Count; cIndex++)
            {
                var cInput = qInput.Choices[cIndex];
                question.AddChoice(cInput.OrderIndex ?? cIndex + 1, cInput.ChoiceText, cInput.IsCorrect);
            }
        }
    }

    private static IReadOnlyCollection<QuizQuestion> BuildQuestionEntities(
        Guid quizId,
        IReadOnlyList<QuizQuestionInput>? inputs)
    {
        var list = new List<QuizQuestion>();
        if (inputs is null)
        {
            return list;
        }

        for (var qIndex = 0; qIndex < inputs.Count; qIndex++)
        {
            var qInput = inputs[qIndex];
            var entity = new QuizQuestion(
                quizId,
                qInput.OrderIndex ?? qIndex + 1,
                qInput.QuestionText,
                qInput.QuestionType,
                qInput.Explanation);

            if (qInput.Choices is not null)
            {
                for (var cIndex = 0; cIndex < qInput.Choices.Count; cIndex++)
                {
                    var cInput = qInput.Choices[cIndex];
                    entity.AddChoice(cInput.OrderIndex ?? cIndex + 1, cInput.ChoiceText, cInput.IsCorrect);
                }
            }

            list.Add(entity);
        }

        return list;
    }

    private bool HasPermission(string permission) =>
        currentUser.IsAuthenticated &&
        currentUser.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    private static OnboardingQuizSummaryDto MapToSummaryDto(
        OnboardingQuiz quiz,
        IReadOnlyList<QuizAttempt>? employeeAttempts)
    {
        string? myStatus = null;
        int? myBestScore = null;
        bool? myHasPassed = null;

        if (employeeAttempts is not null)
        {
            var completed = employeeAttempts.Where(a => a.Status == QuizAttemptStatus.Completed).ToArray();
            if (completed.Length > 0)
            {
                myStatus = "Completed";
                myBestScore = completed.Max(a => a.ScorePercent);
                myHasPassed = completed.Any(a => a.Passed);
            }
            else if (employeeAttempts.Count > 0)
            {
                myStatus = "InProgress";
            }
            else
            {
                myStatus = "NotStarted";
            }
        }

        return new OnboardingQuizSummaryDto(
            quiz.Id,
            quiz.CompanyId,
            quiz.Title,
            quiz.Description,
            quiz.PassingScorePercent,
            quiz.IsPublished,
            quiz.Version,
            quiz.Questions.Count,
            myStatus,
            myBestScore,
            myHasPassed,
            quiz.CreatedAtUtc,
            quiz.UpdatedAtUtc,
            Convert.ToBase64String(quiz.RowVersion));
    }

    private static OnboardingQuizDetailDto MapToDetailDto(OnboardingQuiz quiz, bool includeAnswerKey) =>
        new(
            quiz.Id,
            quiz.CompanyId,
            quiz.Title,
            quiz.Description,
            quiz.PassingScorePercent,
            quiz.IsPublished,
            quiz.Version,
            quiz.Questions
                .OrderBy(q => q.OrderIndex)
                .Select(q => new QuizQuestionDto(
                    q.Id,
                    q.OrderIndex,
                    q.QuestionText,
                    q.QuestionType,
                    includeAnswerKey ? q.Explanation : null,
                    q.Choices
                        .OrderBy(c => c.OrderIndex)
                        .Select(c => new QuizChoiceDto(
                            c.Id,
                            c.OrderIndex,
                            c.ChoiceText,
                            includeAnswerKey ? c.IsCorrect : null))
                        .ToArray()))
                .ToArray(),
            quiz.CreatedAtUtc,
            quiz.UpdatedAtUtc,
            Convert.ToBase64String(quiz.RowVersion));
}
