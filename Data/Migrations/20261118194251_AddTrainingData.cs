using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BubbleSortMvc.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainingData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrainingDatasets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", nullable: false),
                    CsvContent = table.Column<string>(type: "TEXT", nullable: false),
                    ColumnsJson = table.Column<string>(type: "TEXT", nullable: false),
                    RowCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingDatasets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainingDatasets_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrainingRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    TrainingDatasetId = table.Column<int>(type: "INTEGER", nullable: false),
                    ClassificationTarget = table.Column<string>(type: "TEXT", nullable: false),
                    RegressionTarget = table.Column<string>(type: "TEXT", nullable: false),
                    ResultsJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainingRuns_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TrainingRuns_TrainingDatasets_TrainingDatasetId",
                        column: x => x.TrainingDatasetId,
                        principalTable: "TrainingDatasets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrainingDatasets_UserId",
                table: "TrainingDatasets",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingRuns_TrainingDatasetId",
                table: "TrainingRuns",
                column: "TrainingDatasetId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingRuns_UserId",
                table: "TrainingRuns",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrainingRuns");

            migrationBuilder.DropTable(
                name: "TrainingDatasets");
        }
    }
}
