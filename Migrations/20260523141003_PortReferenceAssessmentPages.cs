using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LMS.Migrations
{
    /// <inheritdoc />
    public partial class PortReferenceAssessmentPages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_QuizAttempts_QuizId",
                table: "QuizAttempts");

            migrationBuilder.DropIndex(
                name: "IX_AssignmentSubmissions_AssignmentId",
                table: "AssignmentSubmissions");

            migrationBuilder.AddColumn<bool>(
                name: "IsPublished",
                table: "Quizzes",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalMarks",
                table: "Quizzes",
                type: "integer",
                nullable: false,
                defaultValue: 100);

            migrationBuilder.AddColumn<string>(
                name: "Comment",
                table: "AssignmentSubmissions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "AssignmentSubmissions",
                type: "text",
                nullable: false,
                defaultValue: "Submitted");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "AssignmentSubmissions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<string>(
                name: "AttachmentFileName",
                table: "Assignments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentPath",
                table: "Assignments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxScore",
                table: "Assignments",
                type: "integer",
                nullable: false,
                defaultValue: 100);

            migrationBuilder.Sql("""
                UPDATE "AssignmentSubmissions"
                SET "Status" = CASE
                    WHEN "Grade" IS NOT NULL THEN 'Graded'
                    WHEN "SubmittedAt" > (SELECT "Deadline" FROM "Assignments" WHERE "Assignments"."Id" = "AssignmentSubmissions"."AssignmentId") THEN 'Late'
                    ELSE 'Submitted'
                END;
                """);

            migrationBuilder.CreateTable(
                name: "QuizResponses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuizAttemptId = table.Column<int>(type: "integer", nullable: false),
                    QuizQuestionId = table.Column<int>(type: "integer", nullable: false),
                    SelectedOptionId = table.Column<int>(type: "integer", nullable: true),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    AwardedMarks = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuizResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuizResponses_QuizAttempts_QuizAttemptId",
                        column: x => x.QuizAttemptId,
                        principalTable: "QuizAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QuizResponses_QuizOptions_SelectedOptionId",
                        column: x => x.SelectedOptionId,
                        principalTable: "QuizOptions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_QuizResponses_QuizQuestions_QuizQuestionId",
                        column: x => x.QuizQuestionId,
                        principalTable: "QuizQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuizAttempts_QuizId_StudentId",
                table: "QuizAttempts",
                columns: new[] { "QuizId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentSubmissions_AssignmentId_StudentId",
                table: "AssignmentSubmissions",
                columns: new[] { "AssignmentId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuizResponses_QuizAttemptId_QuizQuestionId",
                table: "QuizResponses",
                columns: new[] { "QuizAttemptId", "QuizQuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuizResponses_QuizQuestionId",
                table: "QuizResponses",
                column: "QuizQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuizResponses_SelectedOptionId",
                table: "QuizResponses",
                column: "SelectedOptionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuizResponses");

            migrationBuilder.DropIndex(
                name: "IX_QuizAttempts_QuizId_StudentId",
                table: "QuizAttempts");

            migrationBuilder.DropIndex(
                name: "IX_AssignmentSubmissions_AssignmentId_StudentId",
                table: "AssignmentSubmissions");

            migrationBuilder.DropColumn(
                name: "IsPublished",
                table: "Quizzes");

            migrationBuilder.DropColumn(
                name: "TotalMarks",
                table: "Quizzes");

            migrationBuilder.DropColumn(
                name: "Comment",
                table: "AssignmentSubmissions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "AssignmentSubmissions");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "AssignmentSubmissions");

            migrationBuilder.DropColumn(
                name: "AttachmentFileName",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "AttachmentPath",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "MaxScore",
                table: "Assignments");

            migrationBuilder.CreateIndex(
                name: "IX_QuizAttempts_QuizId",
                table: "QuizAttempts",
                column: "QuizId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentSubmissions_AssignmentId",
                table: "AssignmentSubmissions",
                column: "AssignmentId");
        }
    }
}
