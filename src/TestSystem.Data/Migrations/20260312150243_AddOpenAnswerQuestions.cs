using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TestSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOpenAnswerQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TextAnswer",
                table: "TestSessionAnswers",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrectAnswerText",
                table: "Questions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOpenAnswer",
                table: "Questions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TextAnswer",
                table: "TestSessionAnswers");

            migrationBuilder.DropColumn(
                name: "CorrectAnswerText",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "IsOpenAnswer",
                table: "Questions");
        }
    }
}
