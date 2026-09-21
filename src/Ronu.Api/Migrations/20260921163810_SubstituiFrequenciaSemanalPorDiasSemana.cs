using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ronu.Api.Migrations
{
    /// <inheritdoc />
    public partial class SubstituiFrequenciaSemanalPorDiasSemana : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Projeto ainda em fase de teste/portfólio, sem usuários reais —
            // resetar os vínculos existentes é decisão consciente, não um
            // acidente de migration. Precisa vir antes do DropColumn porque a
            // nova coluna DiasSemana é NOT NULL sem valor padrão significativo.
            migrationBuilder.Sql("TRUNCATE TABLE \"UsuarioModalidades\";");

            migrationBuilder.DropColumn(
                name: "FrequenciaSemanal",
                table: "UsuarioModalidades");

            migrationBuilder.AddColumn<int[]>(
                name: "DiasSemana",
                table: "UsuarioModalidades",
                type: "integer[]",
                nullable: false,
                defaultValue: new int[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiasSemana",
                table: "UsuarioModalidades");

            migrationBuilder.AddColumn<int>(
                name: "FrequenciaSemanal",
                table: "UsuarioModalidades",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
