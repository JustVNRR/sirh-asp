using Shouldly;
using Sirh.Modules.InternalLife.Domain.Entities;
using Sirh.Modules.InternalLife.Domain.Enums;
using Sirh.Modules.InternalLife.Domain.Services;
using Xunit;

namespace Sirh.UnitTests.InternalLife;

public sealed class QuizScoringAndPublishTests
{
    [Fact]
    public void ValidateForPublish_Should_Reject_Empty_Questions_Insufficient_Choices_Or_Invalid_Correct_Count()
    {
        var emptyQuiz = new OnboardingQuiz(null, "Empty", null, 70);
        emptyQuiz.ValidateForPublish().IsFailure.ShouldBeTrue();

        var fewChoicesQuiz = new OnboardingQuiz(null, "One Choice", null, 70);
        var q1 = fewChoicesQuiz.AddQuestion(1, "Q1", QuizQuestionType.SingleChoice, null);
        q1.AddChoice(1, "Only", true);
        fewChoicesQuiz.ValidateForPublish().IsFailure.ShouldBeTrue();

        var noCorrectQuiz = new OnboardingQuiz(null, "No Correct", null, 70);
        var q2 = noCorrectQuiz.AddQuestion(1, "Q1", QuizQuestionType.MultipleChoice, null);
        q2.AddChoice(1, "A", false);
        q2.AddChoice(2, "B", false);
        noCorrectQuiz.ValidateForPublish().IsFailure.ShouldBeTrue();

        var twoCorrectSingleQuiz = new OnboardingQuiz(null, "Single With Two Correct", null, 70);
        var q3 = twoCorrectSingleQuiz.AddQuestion(1, "Q1", QuizQuestionType.SingleChoice, null);
        q3.AddChoice(1, "A", true);
        q3.AddChoice(2, "B", true);
        twoCorrectSingleQuiz.ValidateForPublish().IsFailure.ShouldBeTrue();

        var validQuiz = new OnboardingQuiz(null, "Valid Quiz", null, 70);
        var qValid1 = validQuiz.AddQuestion(1, "Q1", QuizQuestionType.SingleChoice, "Exp");
        qValid1.AddChoice(1, "Yes", true);
        qValid1.AddChoice(2, "No", false);
        var qValid2 = validQuiz.AddQuestion(2, "Q2", QuizQuestionType.MultipleChoice, null);
        qValid2.AddChoice(1, "A", true);
        qValid2.AddChoice(2, "B", true);
        qValid2.AddChoice(3, "C", false);
        validQuiz.ValidateForPublish().IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void QuizScoringCalculator_Should_Score_Single_And_Multiple_Choice_And_Reject_Invalid_Selections()
    {
        var quiz = new OnboardingQuiz(null, "Sécurité", null, 50);
        var q1 = quiz.AddQuestion(1, "Badge obligatoire ?", QuizQuestionType.SingleChoice, "Port du badge.");
        var q1Yes = q1.AddChoice(1, "Oui", true);
        q1.AddChoice(2, "Non", false);

        var q2 = quiz.AddQuestion(2, "Issues de secours ?", QuizQuestionType.MultipleChoice, "Nord et Sud.");
        var q2A = q2.AddChoice(1, "Nord", true);
        var q2B = q2.AddChoice(2, "Sud", true);
        var q2C = q2.AddChoice(3, "Sous-sol", false);

        // Partial multiple-choice selection counts as incorrect (1/2 = 50% -> Passed)
        var partialResult = QuizScoringCalculator.Evaluate(
            quiz.Questions.ToArray(),
            quiz.PassingScorePercent,
            new Dictionary<Guid, IReadOnlyCollection<Guid>>
            {
                [q1.Id] = [q1Yes.Id],
                [q2.Id] = [q2A.Id]
            });

        partialResult.IsSuccess.ShouldBeTrue();
        partialResult.Value.CorrectCount.ShouldBe(1);
        partialResult.Value.TotalQuestions.ShouldBe(2);
        partialResult.Value.ScorePercent.ShouldBe(50);
        partialResult.Value.Passed.ShouldBeTrue();

        // Full correct selections (2/2 = 100%)
        var fullResult = QuizScoringCalculator.Evaluate(
            quiz.Questions.ToArray(),
            quiz.PassingScorePercent,
            new Dictionary<Guid, IReadOnlyCollection<Guid>>
            {
                [q1.Id] = [q1Yes.Id],
                [q2.Id] = [q2A.Id, q2B.Id]
            });

        fullResult.IsSuccess.ShouldBeTrue();
        fullResult.Value.ScorePercent.ShouldBe(100);
        fullResult.Value.Passed.ShouldBeTrue();

        // Multiple selections on SingleChoice -> Validation failure
        var tooManySingle = QuizScoringCalculator.Evaluate(
            quiz.Questions.ToArray(),
            quiz.PassingScorePercent,
            new Dictionary<Guid, IReadOnlyCollection<Guid>>
            {
                [q1.Id] = [q1Yes.Id, q2C.Id]
            });
        tooManySingle.IsFailure.ShouldBeTrue();
    }
}
