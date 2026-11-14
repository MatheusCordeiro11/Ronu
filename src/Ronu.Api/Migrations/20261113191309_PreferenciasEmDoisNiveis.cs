using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ronu.Api.Migrations
{
    /// <summary>
    /// Restrições em dois níveis (plano família, etapa 3, decisão D2). Só dados, sem mudança de esquema: o Tipo
    /// continua texto livre na coluna, e quem confere os valores é o controller (TiposPreferencia).
    ///
    /// Antes daqui, "evitar" era o bloqueio (o item saía da lista e do enum do Gemini). Agora o bloqueio se chama
    /// "nao_como", e "evitar" passa a ser o nível leve. Todo "evitar" que existe vira "nao_como": o que bloqueava
    /// continua bloqueando, e nenhuma dieta individual muda.
    /// </summary>
    public partial class PreferenciasEmDoisNiveis : Migration
    {
        /// <summary>O UPDATE do Up, exposto para o teste conferir que a migration é só isso.</summary>
        public const string SqlUp = """UPDATE "PreferenciasAlimentares" SET "Tipo" = 'nao_como' WHERE "Tipo" = 'evitar';""";

        /// <summary>
        /// O Down. Os "evitar" leves criados depois do Up continuam "evitar" e, com o código antigo, voltariam a
        /// bloquear: é o lado seguro (nada que a pessoa quis tirar volta ao cardápio), e o Down não tem como saber
        /// quais "evitar" eram leves.
        /// </summary>
        public const string SqlDown = """UPDATE "PreferenciasAlimentares" SET "Tipo" = 'evitar' WHERE "Tipo" = 'nao_como';""";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlUp);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlDown);
        }
    }
}
