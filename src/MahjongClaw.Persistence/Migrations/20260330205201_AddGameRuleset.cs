using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MahjongClaw.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGameRuleset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Ruleset",
                table: "Games",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "ChineseOfficial");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Ruleset",
                table: "Games");
        }
    }
}
