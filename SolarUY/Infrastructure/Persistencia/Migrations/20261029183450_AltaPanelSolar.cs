using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SolarUY.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AltaPanelSolar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Paneles",
                columns: table => new
                {
                    ID = table.Column<string>(type: "char(36)", fixedLength: true, maxLength: 36, nullable: false),
                    Nombre = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    ClaveNombre = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, collation: "utf8mb4_0900_bin"),
                    IdNodo = table.Column<string>(type: "char(36)", fixedLength: true, maxLength: 36, nullable: false),
                    Modelo = table.Column<string>(type: "longtext", nullable: false),
                    PotenciaNominalW = table.Column<float>(type: "float", nullable: false),
                    FechaInstalacion = table.Column<DateTime>(type: "datetime(0)", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Paneles", x => x.ID);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            // El servidor puede usar MyISAM por defecto y el proveedor omite charset/collation.
            // Convertir antes de agregar la FK garantiza integridad y rollback con InnoDB.
            migrationBuilder.Sql("ALTER TABLE `Paneles` ENGINE=InnoDB, CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;");
            migrationBuilder.Sql("ALTER TABLE `Paneles` MODIFY `ClaveNombre` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_bin NOT NULL;");
            migrationBuilder.AddForeignKey("FK_Paneles_Nodos_IdNodo", "Paneles", "IdNodo",
                "Nodos", principalColumn: "ID", onDelete: ReferentialAction.Cascade);

            migrationBuilder.CreateIndex(
                name: "IX_Paneles_IdNodo",
                table: "Paneles",
                column: "IdNodo");

            migrationBuilder.CreateIndex(
                name: "UX_Paneles_IdNodo_ClaveNombre",
                table: "Paneles",
                columns: new[] { "IdNodo", "ClaveNombre" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Paneles");
        }
    }
}
