using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TestSystem.Data.Data;

#nullable disable

namespace TestSystem.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260323170000_AddOpenAnswerComparisonSettings")]
    public partial class AddOpenAnswerComparisonSettings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IgnoreCase",
                table: "Questions",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IgnoreSimilarLetters",
                table: "Questions",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IgnoreCase",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "IgnoreSimilarLetters",
                table: "Questions");
        }
    }
}
