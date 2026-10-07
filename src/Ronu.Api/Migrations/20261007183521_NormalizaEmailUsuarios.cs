using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ronu.Api.Migrations
{
    /// <summary>
    /// Normaliza os emails já gravados (minúsculas, sem espaços nas pontas,
    /// como NormalizacaoEmail) e cria o índice único de "Usuarios"."Email".
    /// Se houver dois emails que só diferem em maiúsculas ou espaços, a
    /// criação do índice falha e a migration inteira é desfeita (roda numa
    /// transação): conferir duplicatas antes de aplicar em produção.
    /// Down só remove o índice; a normalização não tem como ser desfeita.
    /// </summary>
    public partial class NormalizaEmailUsuarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """UPDATE "Usuarios" SET "Email" = lower(trim("Email")) WHERE "Email" <> lower(trim("Email"));""");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Email",
                table: "Usuarios",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Usuarios_Email",
                table: "Usuarios");
        }
    }
}
