using Sirh.Modules.InternalLife.Domain.Enums;

namespace Sirh.Modules.InternalLife.Application.DTOs;

public sealed record QuizChoiceDto(
    Guid Id,
    int OrderIndex,
    string ChoiceText,
    bool? IsCorrect);

public sealed record QuizQuestionDto(
    Guid Id,
    int OrderIndex,
    string QuestionText,
    QuizQuestionType QuestionType,
    string? Explanation,
    IReadOnlyList<QuizChoiceDto> Choices);

public sealed record OnboardingQuizSummaryDto(
    Guid Id,
    Guid? CompanyId,
    string Title,
    string? Description,
    int PassingScorePercent,
    bool IsPublished,
    int Version,
    int QuestionCount,
    string? MyStatus,
    int? MyBestScorePercent,
    bool? MyHasPassed,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string RowVersion);

public sealed record OnboardingQuizDetailDto(
    Guid Id,
    Guid? CompanyId,
    string Title,
    string? Description,
    int PassingScorePercent,
    bool IsPublished,
    int Version,
    IReadOnlyList<QuizQuestionDto> Questions,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string RowVersion);

public sealed record QuizChoiceInput(
    string ChoiceText,
    bool IsCorrect,
    int? OrderIndex = null);

public sealed record QuizQuestionInput(
    string QuestionText,
    QuizQuestionType QuestionType,
    string? Explanation,
    IReadOnlyList<QuizChoiceInput> Choices,
    int? OrderIndex = null);

public sealed record CreateOnboardingQuizRequest(
    Guid? CompanyId,
    string Title,
    string? Description,
    int PassingScorePercent,
    IReadOnlyList<QuizQuestionInput> Questions);

public sealed record UpdateOnboardingQuizRequest(
    Guid? CompanyId,
    string Title,
    string? Description,
    int PassingScorePercent,
    IReadOnlyList<QuizQuestionInput> Questions,
    string? RowVersion = null);

public sealed record PublishOnboardingQuizRequest(
    bool IsPublished,
    string? RowVersion = null);

public sealed record QuizAttemptChoiceDto(
    Guid Id,
    int OrderIndex,
    string ChoiceText);

public sealed record QuizAttemptQuestionDto(
    Guid Id,
    int OrderIndex,
    string QuestionText,
    QuizQuestionType QuestionType,
    IReadOnlyList<QuizAttemptChoiceDto> Choices);

public sealed record StartedQuizAttemptDto(
    Guid AttemptId,
    Guid QuizId,
    string QuizTitle,
    int QuizVersion,
    int PassingScorePercent,
    DateTime StartedAtUtc,
    IReadOnlyList<QuizAttemptQuestionDto> Questions);

public sealed record SubmitQuestionAnswerRequest(
    Guid QuestionId,
    IReadOnlyCollection<Guid> SelectedChoiceIds);

public sealed record SubmitQuizAttemptRequest(
    IReadOnlyList<SubmitQuestionAnswerRequest> Answers);

public sealed record QuizQuestionCorrectionDto(
    Guid QuestionId,
    string QuestionText,
    IReadOnlyList<Guid> SelectedChoiceIds,
    IReadOnlyList<Guid> CorrectChoiceIds,
    bool IsCorrect,
    string? Explanation);

public sealed record QuizAttemptResultDto(
    Guid AttemptId,
    Guid QuizId,
    Guid EmployeeId,
    int QuizVersion,
    QuizAttemptStatus Status,
    int CorrectAnswersCount,
    int TotalQuestionsCount,
    int ScorePercent,
    bool Passed,
    DateTime StartedAtUtc,
    DateTime CompletedAtUtc,
    IReadOnlyList<QuizQuestionCorrectionDto> Corrections);

public sealed record QuizAttemptSummaryDto(
    Guid AttemptId,
    Guid QuizId,
    Guid EmployeeId,
    int QuizVersion,
    QuizAttemptStatus Status,
    int CorrectAnswersCount,
    int TotalQuestionsCount,
    int ScorePercent,
    bool Passed,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc);
