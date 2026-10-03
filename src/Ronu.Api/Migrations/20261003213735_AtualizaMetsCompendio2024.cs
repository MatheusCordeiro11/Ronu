using System.Globalization;
using Microsoft.EntityFrameworkCore.Migrations;
using Ronu.Api.Data;

#nullable disable

namespace Ronu.Api.Migrations
{
    /// <summary>
    /// Só dados, sem mudança de esquema: troca o MET de referência das
    /// modalidades pelos valores do Compêndio de Atividades Físicas 2024
    /// (MetsCompendio2024, com a premissa da aula em blocos). Filtra pelo
    /// nome, não pelo id — os ids podem ser diferentes entre os bancos.
    /// Down volta aos valores anteriores (seed de 2026-09-22).
    /// </summary>
    public partial class AtualizaMetsCompendio2024 : Migration
    {
        private static readonly Dictionary<string, decimal> MetsAnteriores = new()
        {
            ["Jiu-jitsu"] = 10.3m,
            ["Muay Thai"] = 10.3m,
            ["Boxe"] = 12.3m,
            ["Musculação"] = 5.0m,
            ["Futebol"] = 10.0m,
            ["Basquete"] = 8.0m,
            ["Natação"] = 7.0m
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            AtualizarMets(migrationBuilder, MetsCompendio2024.PorModalidade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            AtualizarMets(migrationBuilder, MetsAnteriores);
        }

        private static void AtualizarMets(MigrationBuilder migrationBuilder, IReadOnlyDictionary<string, decimal> mets)
        {
            foreach (var (nome, met) in mets)
            {
                migrationBuilder.Sql(
                    $"UPDATE \"Modalidades\" SET \"MetReferencia\" = {met.ToString(CultureInfo.InvariantCulture)} " +
                    $"WHERE \"Nome\" = '{nome.Replace("'", "''")}';");
            }
        }
    }
}
