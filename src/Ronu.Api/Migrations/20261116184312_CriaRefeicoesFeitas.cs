using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Ronu.Api.Migrations
{
    /// <inheritdoc />
    public partial class CriaRefeicoesFeitas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RefeicoesFeitas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    DietaIAId = table.Column<int>(type: "integer", nullable: false),
                    Data = table.Column<DateOnly>(type: "date", nullable: false),
                    RefeicaoIndice = table.Column<int>(type: "integer", nullable: false),
                    Kcal = table.Column<decimal>(type: "numeric(7,1)", precision: 7, scale: 1, nullable: false),
                    RefeicoesNoDia = table.Column<int>(type: "integer", nullable: false),
                    MarcadaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefeicoesFeitas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefeicoesFeitas_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RefeicoesFeitas_UsuarioId_Data_DietaIAId_RefeicaoIndice",
                table: "RefeicoesFeitas",
                columns: new[] { "UsuarioId", "Data", "DietaIAId", "RefeicaoIndice" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RefeicoesFeitas");
        }
    }
}
