using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MahjongClaw.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoundWindAndRotationCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DealerRotationCount",
                table: "Games",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RoundWind",
                table: "Games",
                type: "TEXT",
                maxLength: 16,
                nullable: true,
                defaultValue: "East");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DealerRotationCount",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "RoundWind",
                table: "Games");
        }
    }
}
