using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MahjongClaw.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMeldKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "Melds",
                type: "TEXT",
                maxLength: 16,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Melds");
        }
    }
}
