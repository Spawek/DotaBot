using Microsoft.EntityFrameworkCore.Migrations;

namespace DotaBot.Migrations
{
    public partial class PlayerNameFix : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "players",
                table: "DotaBotGames",
                newName: "Players");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Players",
                table: "DotaBotGames",
                newName: "players");
        }
    }
}
