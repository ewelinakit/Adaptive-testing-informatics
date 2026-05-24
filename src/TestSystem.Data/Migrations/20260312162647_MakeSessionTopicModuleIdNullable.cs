using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TestSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class MakeSessionTopicModuleIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TestSessions_TopicModules_TopicModuleId",
                table: "TestSessions");

            migrationBuilder.AlterColumn<Guid>(
                name: "TopicModuleId",
                table: "TestSessions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_TestSessions_TopicModules_TopicModuleId",
                table: "TestSessions",
                column: "TopicModuleId",
                principalTable: "TopicModules",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TestSessions_TopicModules_TopicModuleId",
                table: "TestSessions");

            migrationBuilder.AlterColumn<Guid>(
                name: "TopicModuleId",
                table: "TestSessions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TestSessions_TopicModules_TopicModuleId",
                table: "TestSessions",
                column: "TopicModuleId",
                principalTable: "TopicModules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
