using System.Globalization;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ronu.Api.Migrations
{
    /// <summary>
    /// Amplia o catálogo de 7 para 26 modalidades e agrupa todas por categoria
    /// (só para a tela). As 7 que já existem não mudam de id, nome nem MET:
    /// recebem só a categoria, pelo nome. As 19 novas entram pelo nome, com
    /// WHERE NOT EXISTS, e o id é gerado pelo banco, como no seed (os ids podem
    /// ser diferentes entre os bancos).
    ///
    /// Os valores ficam escritos aqui, e não lidos de MetsCompendio2024, para a
    /// migration fazer sempre a mesma coisa mesmo que o código mude depois; um
    /// teste confere que batem com MetsCompendio2024 e CategoriasModalidade.
    ///
    /// Down: tira a coluna e apaga as 19 novas pelo nome. Quem tiver vinculado
    /// uma delas perde o vínculo (cascata da FK). Serve só para o banco local.
    /// </summary>
    public partial class AmpliaCatalogoModalidades : Migration
    {
        public static readonly IReadOnlyList<(string Nome, string Categoria)> Existentes =
        [
            ("Jiu-jitsu", "Lutas"),
            ("Muay Thai", "Lutas"),
            ("Boxe", "Lutas"),
            ("Futebol", "Esportes"),
            ("Basquete", "Esportes"),
            ("Musculação", "Condicionamento"),
            ("Natação", "Condicionamento")
        ];

        public static readonly IReadOnlyList<(string Nome, decimal Met, string Categoria)> Novas =
        [
            ("Judô", 7.8m, "Lutas"),
            ("Karatê", 7.8m, "Lutas"),
            ("MMA", 7.8m, "Lutas"),
            ("Wrestling", 7.8m, "Lutas"),
            ("Taekwondo", 7.8m, "Lutas"),
            ("Kickboxing", 7.8m, "Lutas"),
            ("Capoeira", 7.8m, "Lutas"),
            ("Vôlei", 7.0m, "Esportes"),
            ("Futsal", 7.8m, "Esportes"),
            ("Handebol", 8.0m, "Esportes"),
            ("Tênis", 6.8m, "Esportes"),
            ("Beach tennis", 6.0m, "Esportes"),
            ("Corrida", 7.5m, "Condicionamento"),
            ("Ciclismo", 8.0m, "Condicionamento"),
            ("Caminhada", 4.8m, "Condicionamento"),
            ("Treino funcional", 5.5m, "Condicionamento"),
            ("Dança", 5.0m, "Corpo e movimento"),
            ("Yoga", 2.3m, "Corpo e movimento"),
            ("Pilates", 2.8m, "Corpo e movimento")
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Categoria",
                table: "Modalidades",
                type: "text",
                nullable: false,
                defaultValue: "");

            foreach (var (nome, categoria) in Existentes)
            {
                migrationBuilder.Sql(
                    $"UPDATE \"Modalidades\" SET \"Categoria\" = {Texto(categoria)} WHERE \"Nome\" = {Texto(nome)};");
            }

            foreach (var (nome, met, categoria) in Novas)
            {
                migrationBuilder.Sql(
                    $"INSERT INTO \"Modalidades\" (\"Nome\", \"MetReferencia\", \"Categoria\") " +
                    $"SELECT {Texto(nome)}, {met.ToString(CultureInfo.InvariantCulture)}, {Texto(categoria)} " +
                    $"WHERE NOT EXISTS (SELECT 1 FROM \"Modalidades\" WHERE \"Nome\" = {Texto(nome)});");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (nome, _, _) in Novas)
            {
                migrationBuilder.Sql($"DELETE FROM \"Modalidades\" WHERE \"Nome\" = {Texto(nome)};");
            }

            migrationBuilder.DropColumn(
                name: "Categoria",
                table: "Modalidades");
        }

        private static string Texto(string valor) => $"'{valor.Replace("'", "''")}'";
    }
}
