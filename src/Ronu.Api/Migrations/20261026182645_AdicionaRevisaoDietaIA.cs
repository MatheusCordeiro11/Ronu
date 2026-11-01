using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ronu.Api.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaRevisaoDietaIA : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Revisao",
                table: "DietasIA",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Revisao",
                table: "DietasIA");
        }
    }
}
