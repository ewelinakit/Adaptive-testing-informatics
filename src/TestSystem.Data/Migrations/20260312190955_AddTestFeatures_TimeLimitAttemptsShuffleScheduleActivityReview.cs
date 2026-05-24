using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TestSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTestFeatures_TimeLimitAttemptsShuffleScheduleActivityReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeadlineAt",
                table: "TestSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastActivityAt",
                table: "TestSessions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "OverriddenPoints",
                table: "TestSessionAnswers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewFeedback",
                table: "TestSessionAnswers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewStatus",
                table: "TestSessionAnswers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "TestSessionAnswers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedByUserId",
                table: "TestSessionAnswers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AvailableFrom",
                table: "Tests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AvailableTo",
                table: "Tests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxAttempts",
                table: "Tests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShuffleAnswers",
                table: "Tests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShuffleQuestions",
                table: "Tests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TimeLimitMinutes",
                table: "Tests",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestSessionAnswers_ReviewedByUserId",
                table: "TestSessionAnswers",
                column: "ReviewedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_TestSessionAnswers_Users_ReviewedByUserId",
                table: "TestSessionAnswers",
                column: "ReviewedByUserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TestSessionAnswers_Users_ReviewedByUserId",
                table: "TestSessionAnswers");

            migrationBuilder.DropIndex(
                name: "IX_TestSessionAnswers_ReviewedByUserId",
                table: "TestSessionAnswers");

            migrationBuilder.DropColumn(
                name: "DeadlineAt",
                table: "TestSessions");

            migrationBuilder.DropColumn(
                name: "LastActivityAt",
                table: "TestSessions");

            migrationBuilder.DropColumn(
                name: "OverriddenPoints",
                table: "TestSessionAnswers");

            migrationBuilder.DropColumn(
                name: "ReviewFeedback",
                table: "TestSessionAnswers");

            migrationBuilder.DropColumn(
                name: "ReviewStatus",
                table: "TestSessionAnswers");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "TestSessionAnswers");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "TestSessionAnswers");

            migrationBuilder.DropColumn(
                name: "AvailableFrom",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "AvailableTo",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "MaxAttempts",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "ShuffleAnswers",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "ShuffleQuestions",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "TimeLimitMinutes",
                table: "Tests");
        }
    }
}
