using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MahjongClaw.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHandResultsAndBirdRanks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BirdRank",
                table: "Games",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BirdRanks",
                table: "Games",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HandResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameId = table.Column<Guid>(type: "TEXT", nullable: false),
                    HandNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    DealerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoundWind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    IsSelfDraw = table.Column<bool>(type: "INTEGER", nullable: false),
                    DiscarderId = table.Column<Guid>(type: "TEXT", nullable: true),
                    WallExhausted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HandResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HandResults_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HandResultSeatDeltas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HandResultId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlayerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Delta = table.Column<int>(type: "INTEGER", nullable: false),
                    NewScore = table.Column<int>(type: "INTEGER", nullable: false),
                    DeltaCategory = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HandResultSeatDeltas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HandResultSeatDeltas_HandResults_HandResultId",
                        column: x => x.HandResultId,
                        principalTable: "HandResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HandResultWinners",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HandResultId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlayerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WinTypesJson = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    IsBigWin = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDealer = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HandResultWinners", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HandResultWinners_HandResults_HandResultId",
                        column: x => x.HandResultId,
                        principalTable: "HandResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HandResults_GameId_HandNumber",
                table: "HandResults",
                columns: new[] { "GameId", "HandNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_HandResultSeatDeltas_HandResultId",
                table: "HandResultSeatDeltas",
                column: "HandResultId");

            migrationBuilder.CreateIndex(
                name: "IX_HandResultWinners_HandResultId",
                table: "HandResultWinners",
                column: "HandResultId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HandResultSeatDeltas");

            migrationBuilder.DropTable(
                name: "HandResultWinners");

            migrationBuilder.DropTable(
                name: "HandResults");

            migrationBuilder.DropColumn(
                name: "BirdRank",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "BirdRanks",
                table: "Games");
        }
    }
}
