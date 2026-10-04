using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Ronu.Api.Migrations
{
    /// <inheritdoc />
    public partial class CriaMetasDieta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MetasDieta",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    DietaIAId = table.Column<int>(type: "integer", nullable: true),
                    DataGeracao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VersaoFormula = table.Column<int>(type: "integer", nullable: false),
                    MetasPorDia = table.Column<decimal[]>(type: "numeric[]", nullable: false),
                    ManutencoesPorDia = table.Column<decimal[]>(type: "numeric[]", nullable: true),
                    PercentualAjusteAdaptativo = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetasDieta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetasDieta_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MetasDieta_UsuarioId_DataGeracao",
                table: "MetasDieta",
                columns: new[] { "UsuarioId", "DataGeracao" });

            // Carga inicial: uma linha por DietaIA existente. As metas vêm da
            // MetaCalculada de cada dia, ordenadas pelo NOME do dia (o JSON
            // guarda os dias na ordem em que a IA devolveu). Dietas sem as 7
            // metas válidas ficam de fora (em 2026-10-03, nenhuma em produção).
            // Versão 2 a partir do deploy da revisão da manutenção (2026-10-03
            // 22:09:26 UTC); antes, 1. Nenhuma dieta guardou a manutenção, então
            // ela fica nula; o percentual vem do AjusteAdaptativo, se houver.
            migrationBuilder.Sql("""
                WITH base AS (
                    SELECT d."Id", d."UsuarioId", d."DataGeracao", d."ConteudoJson"::jsonb AS c
                    FROM "DietasIA" d
                ),
                dias AS (
                    SELECT b."Id",
                           CASE lower(trim(e->>'DiaSemana'))
                               WHEN 'segunda-feira' THEN 1 WHEN 'terça-feira' THEN 2
                               WHEN 'quarta-feira' THEN 3 WHEN 'quinta-feira' THEN 4
                               WHEN 'sexta-feira' THEN 5 WHEN 'sábado' THEN 6
                               WHEN 'domingo' THEN 7
                           END AS dia,
                           (e->'MetaCalculada'->>'Calorias')::numeric AS meta
                    FROM base b,
                         jsonb_array_elements(CASE WHEN jsonb_typeof(b.c->'Dias') = 'array' THEN b.c->'Dias' ELSE '[]'::jsonb END) e
                    WHERE jsonb_typeof(e->'MetaCalculada'->'Calorias') = 'number'
                ),
                validas AS (
                    SELECT "Id", array_agg(meta ORDER BY dia) AS metas
                    FROM dias
                    WHERE dia IS NOT NULL
                    GROUP BY "Id"
                    HAVING count(*) = 7 AND count(DISTINCT dia) = 7 AND min(meta) > 0
                )
                INSERT INTO "MetasDieta"
                    ("UsuarioId", "DietaIAId", "DataGeracao", "VersaoFormula", "MetasPorDia", "ManutencoesPorDia", "PercentualAjusteAdaptativo")
                SELECT b."UsuarioId", b."Id", b."DataGeracao",
                       CASE WHEN b."DataGeracao" >= TIMESTAMPTZ '2026-10-03 22:09:26+00' THEN 2 ELSE 1 END,
                       v.metas,
                       NULL,
                       CASE WHEN jsonb_typeof(b.c->'AjusteAdaptativo'->'Percentual') = 'number'
                            THEN (b.c->'AjusteAdaptativo'->>'Percentual')::numeric END
                FROM base b
                JOIN validas v ON v."Id" = b."Id";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MetasDieta");
        }
    }
}
