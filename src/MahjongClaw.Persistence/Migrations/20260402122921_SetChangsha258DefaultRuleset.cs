using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MahjongClaw.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SetChangsha258DefaultRuleset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Ruleset",
                table: "Games",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "Changsha258",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 32);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Ruleset",
                table: "Games",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "ChineseOfficial",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 32,
                oldDefaultValue: "Changsha258");
        }
    }
}
