using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TestSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShowCorrectAnswers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShowCorrectAnswers",
                table: "Tests",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShowCorrectAnswers",
                table: "Tests");
        }
    }
}
