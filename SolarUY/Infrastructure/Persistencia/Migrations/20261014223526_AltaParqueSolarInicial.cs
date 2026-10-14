using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SolarUY.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AltaParqueSolarInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ParquesSolares",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "TEXT", nullable: false),
                    Nombre = table.Column<string>(type: "TEXT", nullable: false),
                    ClaveNombre = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    Ubicacion = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParquesSolares", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "UX_ParquesSolares_ClaveNombre",
                table: "ParquesSolares",
                column: "ClaveNombre",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParquesSolares");
        }
    }
}
