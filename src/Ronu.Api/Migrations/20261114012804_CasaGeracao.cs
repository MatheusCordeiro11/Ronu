using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Ronu.Api.Migrations
{
    /// <inheritdoc />
    public partial class CasaGeracao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Origem",
                table: "DietasIA",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "pessoa");

            migrationBuilder.AddColumn<int>(
                name: "SemanaCasaId",
                table: "DietasIA",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SemanasCasa",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CasaId = table.Column<int>(type: "integer", nullable: false),
                    GeradaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Revisao = table.Column<int>(type: "integer", nullable: false),
                    ConteudoJson = table.Column<string>(type: "text", nullable: false),
                    VersaoBase = table.Column<int>(type: "integer", nullable: false),
                    RefeicaoIndividualEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SemanasCasa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SemanasCasa_Casas_CasaId",
                        column: x => x.CasaId,
                        principalTable: "Casas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SemanasCasa_CasaId_GeradaEm",
                table: "SemanasCasa",
                columns: new[] { "CasaId", "GeradaEm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SemanasCasa");

            migrationBuilder.DropColumn(
                name: "Origem",
                table: "DietasIA");

            migrationBuilder.DropColumn(
                name: "SemanaCasaId",
                table: "DietasIA");
        }
    }
}
