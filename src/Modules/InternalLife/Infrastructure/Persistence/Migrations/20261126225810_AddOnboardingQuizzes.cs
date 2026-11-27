using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sirh.Modules.InternalLife.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOnboardingQuizzes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "onboarding_quizzes",
                schema: "internal_life",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PassingScorePercent = table.Column<int>(type: "int", nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_onboarding_quizzes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "quiz_attempts",
                schema: "internal_life",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuizId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuizVersion = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CorrectAnswersCount = table.Column<int>(type: "int", nullable: false),
                    TotalQuestionsCount = table.Column<int>(type: "int", nullable: false),
                    ScorePercent = table.Column<int>(type: "int", nullable: false),
                    Passed = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quiz_attempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_quiz_attempts_onboarding_quizzes_QuizId",
                        column: x => x.QuizId,
                        principalSchema: "internal_life",
                        principalTable: "onboarding_quizzes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quiz_questions",
                schema: "internal_life",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuizId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false),
                    QuestionText = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    QuestionType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Explanation = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quiz_questions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_quiz_questions_onboarding_quizzes_QuizId",
                        column: x => x.QuizId,
                        principalSchema: "internal_life",
                        principalTable: "onboarding_quizzes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quiz_attempt_answers",
                schema: "internal_life",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SelectedChoiceIdsJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    IsCorrect = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quiz_attempt_answers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_quiz_attempt_answers_quiz_attempts_AttemptId",
                        column: x => x.AttemptId,
                        principalSchema: "internal_life",
                        principalTable: "quiz_attempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quiz_choices",
                schema: "internal_life",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false),
                    ChoiceText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsCorrect = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quiz_choices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_quiz_choices_quiz_questions_QuestionId",
                        column: x => x.QuestionId,
                        principalSchema: "internal_life",
                        principalTable: "quiz_questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_onboarding_quizzes_CompanyId_IsPublished",
                schema: "internal_life",
                table: "onboarding_quizzes",
                columns: new[] { "CompanyId", "IsPublished" });

            migrationBuilder.CreateIndex(
                name: "IX_quiz_attempt_answers_AttemptId_QuestionId",
                schema: "internal_life",
                table: "quiz_attempt_answers",
                columns: new[] { "AttemptId", "QuestionId" });

            migrationBuilder.CreateIndex(
                name: "IX_quiz_attempts_QuizId_EmployeeId",
                schema: "internal_life",
                table: "quiz_attempts",
                columns: new[] { "QuizId", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_quiz_choices_QuestionId_OrderIndex",
                schema: "internal_life",
                table: "quiz_choices",
                columns: new[] { "QuestionId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_quiz_questions_QuizId_OrderIndex",
                schema: "internal_life",
                table: "quiz_questions",
                columns: new[] { "QuizId", "OrderIndex" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quiz_attempt_answers",
                schema: "internal_life");

            migrationBuilder.DropTable(
                name: "quiz_choices",
                schema: "internal_life");

            migrationBuilder.DropTable(
                name: "quiz_attempts",
                schema: "internal_life");

            migrationBuilder.DropTable(
                name: "quiz_questions",
                schema: "internal_life");

            migrationBuilder.DropTable(
                name: "onboarding_quizzes",
                schema: "internal_life");
        }
    }
}
