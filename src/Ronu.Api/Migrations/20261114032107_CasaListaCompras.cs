using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Ronu.Api.Migrations
{
    /// <inheritdoc />
    public partial class CasaListaCompras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ListaCasaMarcacoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CasaId = table.Column<int>(type: "integer", nullable: false),
                    SemanaCasaId = table.Column<int>(type: "integer", nullable: false),
                    Chave = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Quantidade = table.Column<decimal>(type: "numeric", nullable: false),
                    MarcadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListaCasaMarcacoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListaCasaMarcacoes_Casas_CasaId",
                        column: x => x.CasaId,
                        principalTable: "Casas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ListaCasaMarcacoes_SemanasCasa_SemanaCasaId",
                        column: x => x.SemanaCasaId,
                        principalTable: "SemanasCasa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ListaCasaMarcacoes_CasaId",
                table: "ListaCasaMarcacoes",
                column: "CasaId");

            migrationBuilder.CreateIndex(
                name: "IX_ListaCasaMarcacoes_SemanaCasaId_Chave",
                table: "ListaCasaMarcacoes",
                columns: new[] { "SemanaCasaId", "Chave" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ListaCasaMarcacoes");
        }
    }
}
