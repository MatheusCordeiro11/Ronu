using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ronu.Api.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaHorariosEstruturados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "HorarioAcordar",
                table: "Usuarios",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "HorarioDormir",
                table: "Usuarios",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "HorarioInicio",
                table: "UsuarioModalidades",
                type: "time without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HorarioAcordar",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "HorarioDormir",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "HorarioInicio",
                table: "UsuarioModalidades");
        }
    }
}
