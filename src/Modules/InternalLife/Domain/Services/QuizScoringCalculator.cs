using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.InternalLife.Domain.Entities;
using Sirh.Modules.InternalLife.Domain.Enums;

namespace Sirh.Modules.InternalLife.Domain.Services;

public sealed record EvaluatedQuestionOutcome(
    Guid QuestionId,
    string QuestionText,
    IReadOnlyList<Guid> SelectedChoiceIds,
    IReadOnlyList<Guid> CorrectChoiceIds,
    bool IsCorrect,
    string? Explanation);

public sealed record QuizEvaluationOutcome(
    int CorrectCount,
    int TotalQuestions,
    int ScorePercent,
    bool Passed,
    IReadOnlyList<EvaluatedQuestionOutcome> Questions);

public static class QuizScoringCalculator
{
    public static Result<QuizEvaluationOutcome> Evaluate(
        IReadOnlyCollection<QuizQuestion> questions,
        int passingScorePercent,
        IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>> submittedAnswersByQuestionId)
    {
        var orderedQuestions = questions.OrderBy(q => q.OrderIndex).ToArray();
        var validQuestionIds = orderedQuestions.Select(q => q.Id).ToHashSet();

        foreach (var submittedQuestionId in submittedAnswersByQuestionId.Keys)
        {
            if (!validQuestionIds.Contains(submittedQuestionId))
            {
                return Error.Validation(
                    "Quizzes.InvalidQuestionReference",
                    $"Question '{submittedQuestionId}' does not belong to this quiz.");
            }
        }

        var outcomes = new List<EvaluatedQuestionOutcome>(orderedQuestions.Length);
        var correctCount = 0;

        foreach (var question in orderedQuestions)
        {
            var validChoiceIds = question.Choices.Select(c => c.Id).ToHashSet();
            var selectedList = submittedAnswersByQuestionId.TryGetValue(question.Id, out var rawSelected)
                ? rawSelected.Distinct().ToArray()
                : [];

            foreach (var choiceId in selectedList)
            {
                if (!validChoiceIds.Contains(choiceId))
                {
                    return Error.Validation(
                        "Quizzes.InvalidChoiceReference",
                        $"Choice '{choiceId}' does not belong to question '{question.Id}'.");
                }
            }

            if (question.QuestionType == QuizQuestionType.SingleChoice && selectedList.Length > 1)
            {
                return Error.Validation(
                    "Quizzes.TooManyChoicesSelected",
                    $"Single-choice question '{question.QuestionText}' accepts at most one selected choice.");
            }

            var correctChoiceIds = question.Choices
                .Where(c => c.IsCorrect)
                .OrderBy(c => c.OrderIndex)
                .Select(c => c.Id)
                .ToArray();

            var isCorrect = selectedList.Length == correctChoiceIds.Length &&
                            selectedList.ToHashSet().SetEquals(correctChoiceIds);

            if (isCorrect)
            {
                correctCount++;
            }

            outcomes.Add(new EvaluatedQuestionOutcome(
                question.Id,
                question.QuestionText,
                selectedList,
                correctChoiceIds,
                isCorrect,
                question.Explanation));
        }

        var totalQuestions = orderedQuestions.Length;
        var scorePercent = totalQuestions > 0
            ? (int)Math.Round(correctCount * 100.0 / totalQuestions, MidpointRounding.AwayFromZero)
            : 0;
        var passed = scorePercent >= passingScorePercent;

        return new QuizEvaluationOutcome(correctCount, totalQuestions, scorePercent, passed, outcomes);
    }
}
