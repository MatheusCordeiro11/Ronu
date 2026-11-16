using System.Globalization;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ronu.Api.Migrations
{
    /// <summary>
    /// "Ritmo da aula" (intensidade do treino): a coluna Intensidade nos vínculos
    /// (default 'normal', então as linhas de antes e o insert de um backend que
    /// não conhece o campo ficam em "normal" e a meta de ninguém muda) e os METs
    /// dos dois blocos nas 11 modalidades modeladas como aula em blocos. As
    /// outras 15 (Vôlei incluído) ficam com as colunas nulas.
    ///
    /// Os valores ficam escritos aqui, e não lidos de MetsCompendio2024, para a
    /// migration fazer sempre a mesma coisa mesmo que o código mude depois; um
    /// teste confere que batem com MetsCompendio2024.BlocosPorModalidade. Filtra
    /// pelo nome, não pelo id (os ids podem ser diferentes entre os bancos).
    ///
    /// Down: tira as três colunas (o ritmo escolhido por cada pessoa se perde).
    /// </summary>
    public partial class AdicionaIntensidadeTreino : Migration
    {
        public static readonly IReadOnlyList<(string Nome, decimal Leve, decimal Intenso)> Blocos =
        [
            ("Jiu-jitsu", 5.3m, 10.3m),
            ("Muay Thai", 5.3m, 10.3m),
            ("Boxe", 5.8m, 7.8m),
            ("Judô", 5.3m, 10.3m),
            ("Karatê", 5.3m, 10.3m),
            ("MMA", 5.3m, 10.3m),
            ("Wrestling", 5.3m, 10.3m),
            ("Taekwondo", 5.3m, 10.3m),
            ("Kickboxing", 5.3m, 10.3m),
            ("Capoeira", 5.3m, 10.3m),
            ("Treino funcional", 3.5m, 7.5m)
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Intensidade",
                table: "UsuarioModalidades",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "normal");

            migrationBuilder.AddColumn<decimal>(
                name: "MetBlocoIntenso",
                table: "Modalidades",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MetBlocoLeve",
                table: "Modalidades",
                type: "numeric",
                nullable: true);

            foreach (var (nome, leve, intenso) in Blocos)
            {
                migrationBuilder.Sql(
                    $"UPDATE \"Modalidades\" SET \"MetBlocoLeve\" = {Numero(leve)}, \"MetBlocoIntenso\" = {Numero(intenso)} " +
                    $"WHERE \"Nome\" = '{nome.Replace("'", "''")}';");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Intensidade",
                table: "UsuarioModalidades");

            migrationBuilder.DropColumn(
                name: "MetBlocoIntenso",
                table: "Modalidades");

            migrationBuilder.DropColumn(
                name: "MetBlocoLeve",
                table: "Modalidades");
        }

        private static string Numero(decimal valor) => valor.ToString(CultureInfo.InvariantCulture);
    }
}
