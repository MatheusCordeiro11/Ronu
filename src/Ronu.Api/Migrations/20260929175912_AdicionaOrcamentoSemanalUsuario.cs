using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ronu.Api.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaOrcamentoSemanalUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OrcamentoSemanal",
                table: "Usuarios",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrcamentoSemanal",
                table: "Usuarios");
        }
    }
}
