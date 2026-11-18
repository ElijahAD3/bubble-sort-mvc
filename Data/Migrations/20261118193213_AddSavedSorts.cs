using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BubbleSortMvc.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSavedSorts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SavedSorts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    DataType = table.Column<string>(type: "TEXT", nullable: false),
                    OriginalJson = table.Column<string>(type: "TEXT", nullable: false),
                    SortedJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedSorts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedSorts_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SavedSorts_UserId",
                table: "SavedSorts",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SavedSorts");
        }
    }
}
