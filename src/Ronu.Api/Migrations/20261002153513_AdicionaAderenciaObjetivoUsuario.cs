using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ronu.Api.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaAderenciaObjetivoUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Aderencia",
                table: "ObjetivosUsuario",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Aderencia",
                table: "ObjetivosUsuario");
        }
    }
}
