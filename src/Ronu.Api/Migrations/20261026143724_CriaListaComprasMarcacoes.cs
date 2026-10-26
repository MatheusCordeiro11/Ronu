using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Ronu.Api.Migrations
{
    /// <inheritdoc />
    public partial class CriaListaComprasMarcacoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ListaComprasMarcacoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    DietaIAId = table.Column<int>(type: "integer", nullable: false),
                    Chave = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MarcadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListaComprasMarcacoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListaComprasMarcacoes_DietasIA_DietaIAId",
                        column: x => x.DietaIAId,
                        principalTable: "DietasIA",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ListaComprasMarcacoes_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ListaComprasMarcacoes_DietaIAId_Chave",
                table: "ListaComprasMarcacoes",
                columns: new[] { "DietaIAId", "Chave" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ListaComprasMarcacoes_UsuarioId",
                table: "ListaComprasMarcacoes",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ListaComprasMarcacoes");
        }
    }
}
