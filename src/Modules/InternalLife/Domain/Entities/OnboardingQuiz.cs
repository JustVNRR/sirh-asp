using Sirh.BuildingBlocks.Domain.Primitives;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.InternalLife.Domain.Enums;

namespace Sirh.Modules.InternalLife.Domain.Entities;

public sealed class OnboardingQuiz : AuditableEntity
{
    private OnboardingQuiz()
    {
    }

    public OnboardingQuiz(
        Guid? companyId,
        string title,
        string? description,
        int passingScorePercent)
    {
        CompanyId = companyId;
        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        PassingScorePercent = passingScorePercent;
        IsPublished = false;
        Version = 1;
    }

    public Guid? CompanyId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int PassingScorePercent { get; private set; }
    public bool IsPublished { get; private set; }
    public int Version { get; private set; }

    public ICollection<QuizQuestion> Questions { get; private set; } = [];
    public ICollection<QuizAttempt> Attempts { get; private set; } = [];

    public QuizQuestion AddQuestion(
        int orderIndex,
        string questionText,
        QuizQuestionType questionType,
        string? explanation)
    {
        var question = new QuizQuestion(Id, orderIndex, questionText, questionType, explanation);
        Questions.Add(question);
        return question;
    }

    public void UpdateMetadata(
        Guid? companyId,
        string title,
        string? description,
        int passingScorePercent)
    {
        CompanyId = companyId;
        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        PassingScorePercent = passingScorePercent;
        Version++;
    }

    public void SetPublished(bool isPublished)
    {
        IsPublished = isPublished;
    }

    public Result ValidateForPublish()
    {
        if (Questions.Count == 0)
        {
            return Result.Failure(Error.Validation(
                "Quizzes.NoQuestions",
                "A quiz must contain at least one question before it can be published."));
        }

        foreach (var question in Questions.OrderBy(q => q.OrderIndex))
        {
            if (question.Choices.Count < 2)
            {
                return Result.Failure(Error.Validation(
                    "Quizzes.InsufficientChoices",
                    $"Question '{question.QuestionText}' must have at least 2 choices."));
            }

            var correctCount = question.Choices.Count(c => c.IsCorrect);
            if (correctCount == 0)
            {
                return Result.Failure(Error.Validation(
                    "Quizzes.NoCorrectChoice",
                    $"Question '{question.QuestionText}' must define at least one correct choice."));
            }

            if (question.QuestionType == QuizQuestionType.SingleChoice && correctCount != 1)
            {
                return Result.Failure(Error.Validation(
                    "Quizzes.SingleChoiceMustHaveOneCorrect",
                    $"Single-choice question '{question.QuestionText}' must have exactly one correct choice."));
            }
        }

        return Result.Success();
    }
}

public sealed class QuizQuestion : Entity
{
    private QuizQuestion()
    {
    }

    internal QuizQuestion(
        Guid quizId,
        int orderIndex,
        string questionText,
        QuizQuestionType questionType,
        string? explanation)
    {
        QuizId = quizId;
        OrderIndex = orderIndex;
        QuestionText = questionText.Trim();
        QuestionType = questionType;
        Explanation = string.IsNullOrWhiteSpace(explanation) ? null : explanation.Trim();
    }

    public Guid QuizId { get; private set; }
    public OnboardingQuiz Quiz { get; private set; } = null!;

    public int OrderIndex { get; private set; }
    public string QuestionText { get; private set; } = string.Empty;
    public QuizQuestionType QuestionType { get; private set; }
    public string? Explanation { get; private set; }

    public ICollection<QuizChoice> Choices { get; private set; } = [];

    public QuizChoice AddChoice(int orderIndex, string choiceText, bool isCorrect)
    {
        var choice = new QuizChoice(Id, orderIndex, choiceText, isCorrect);
        Choices.Add(choice);
        return choice;
    }
}

public sealed class QuizChoice : Entity
{
    private QuizChoice()
    {
    }

    internal QuizChoice(Guid questionId, int orderIndex, string choiceText, bool isCorrect)
    {
        QuestionId = questionId;
        OrderIndex = orderIndex;
        ChoiceText = choiceText.Trim();
        IsCorrect = isCorrect;
    }

    public Guid QuestionId { get; private set; }
    public QuizQuestion Question { get; private set; } = null!;

    public int OrderIndex { get; private set; }
    public string ChoiceText { get; private set; } = string.Empty;
    public bool IsCorrect { get; private set; }
}

public sealed class QuizAttempt : AuditableEntity
{
    private QuizAttempt()
    {
    }

    public QuizAttempt(
        Guid quizId,
        Guid employeeId,
        int quizVersion,
        DateTime startedAtUtc)
    {
        QuizId = quizId;
        EmployeeId = employeeId;
        QuizVersion = quizVersion;
        Status = QuizAttemptStatus.InProgress;
        StartedAtUtc = NormalizeUtc(startedAtUtc);
    }

    public Guid QuizId { get; private set; }
    public OnboardingQuiz Quiz { get; private set; } = null!;

    public Guid EmployeeId { get; private set; }
    public int QuizVersion { get; private set; }
    public QuizAttemptStatus Status { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public int CorrectAnswersCount { get; private set; }
    public int TotalQuestionsCount { get; private set; }
    public int ScorePercent { get; private set; }
    public bool Passed { get; private set; }

    public ICollection<QuizAttemptAnswer> Answers { get; private set; } = [];

    public void AddAnswer(Guid questionId, string selectedChoiceIdsJson, bool isCorrect)
    {
        Answers.Add(new QuizAttemptAnswer(Id, questionId, selectedChoiceIdsJson, isCorrect));
    }

    public void Complete(
        int correctCount,
        int totalQuestions,
        int passingScorePercent,
        DateTime completedAtUtc)
    {
        CorrectAnswersCount = correctCount;
        TotalQuestionsCount = totalQuestions;
        ScorePercent = totalQuestions > 0
            ? (int)Math.Round(correctCount * 100.0 / totalQuestions, MidpointRounding.AwayFromZero)
            : 0;
        Passed = ScorePercent >= passingScorePercent;
        Status = QuizAttemptStatus.Completed;
        CompletedAtUtc = NormalizeUtc(completedAtUtc);
    }

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}

public sealed class QuizAttemptAnswer : Entity
{
    private QuizAttemptAnswer()
    {
    }

    internal QuizAttemptAnswer(
        Guid attemptId,
        Guid questionId,
        string selectedChoiceIdsJson,
        bool isCorrect)
    {
        AttemptId = attemptId;
        QuestionId = questionId;
        SelectedChoiceIdsJson = selectedChoiceIdsJson;
        IsCorrect = isCorrect;
    }

    public Guid AttemptId { get; private set; }
    public QuizAttempt Attempt { get; private set; } = null!;

    public Guid QuestionId { get; private set; }
    public string SelectedChoiceIdsJson { get; private set; } = "[]";
    public bool IsCorrect { get; private set; }
}
