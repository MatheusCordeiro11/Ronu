using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ronu.Api.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaNivelAtividade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NivelAtividade",
                table: "Usuarios",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FatorAtividade",
                table: "MetasDieta",
                type: "numeric(3,2)",
                precision: 3,
                scale: 2,
                nullable: false,
                defaultValue: 1.40m); // dietas anteriores à pergunta usaram o fator único de 1,4
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NivelAtividade",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "FatorAtividade",
                table: "MetasDieta");
        }
    }
}
