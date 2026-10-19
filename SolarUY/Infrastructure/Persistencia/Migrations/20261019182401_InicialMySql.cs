using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SolarUY.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class InicialMySql : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ParquesSolares",
                columns: table => new
                {
                    ID = table.Column<string>(type: "char(36)", fixedLength: true, maxLength: 36, nullable: false),
                    Nombre = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    ClaveNombre = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, collation: "utf8mb4_0900_bin"),
                    Ubicacion = table.Column<string>(type: "longtext", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParquesSolares", x => x.ID);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            // 10.0.9 omite motor, charset y colación al generar CreateTable.
            // Aplicarlos explícitamente antes de crear el índice evita heredar ai_ci.
            migrationBuilder.Sql("""
                ALTER TABLE `ParquesSolares`
                    ENGINE=InnoDB,
                    DEFAULT CHARACTER SET utf8mb4,
                    MODIFY `ID` char(36) CHARACTER SET utf8mb4 NOT NULL,
                    MODIFY `Nombre` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
                    MODIFY `ClaveNombre` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_bin NOT NULL,
                    MODIFY `Ubicacion` longtext CHARACTER SET utf8mb4 NOT NULL;
                """);

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
