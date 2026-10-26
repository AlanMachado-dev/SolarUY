using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SolarUY.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class SeriesEquiposUnicas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Solo la sesión de migración usa esta tabla temporal. El CHECK bloquea
            // datos incompatibles antes del primer ALTER; el error no contiene series.
            // MySQL confirma DDL implícitamente: ejecutar sin escritores concurrentes.
            migrationBuilder.Sql("""
                DROP TEMPORARY TABLE IF EXISTS `__mod002_validacion_series`;
                CREATE TEMPORARY TABLE `__mod002_validacion_series` (
                    `Compatible` int NOT NULL,
                    CONSTRAINT `mod002_series_incompatibles_revisar_datos` CHECK (`Compatible` = 1)
                );
                """);
            ComprobarSeries(migrationBuilder, "Inversores");
            ComprobarSeries(migrationBuilder, "Sensores");
            migrationBuilder.Sql("DROP TEMPORARY TABLE `__mod002_validacion_series`;");

            // El proveedor omite COLLATE en AlterColumn: declararlo en el SQL real.
            migrationBuilder.Sql("ALTER TABLE `Sensores` MODIFY `NumeroSerie` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_bin NOT NULL;");
            migrationBuilder.Sql("ALTER TABLE `Inversores` MODIFY `NumeroSerie` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_bin NOT NULL;");

            migrationBuilder.CreateIndex(
                name: "UX_Sensores_NumeroSerie",
                table: "Sensores",
                column: "NumeroSerie",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Inversores_NumeroSerie",
                table: "Inversores",
                column: "NumeroSerie",
                unique: true);
        }

        private static void ComprobarSeries(MigrationBuilder migrationBuilder, string tabla)
        {
            // Conjunto de espacios de String.Trim(), codificado en UTF-8 para no
            // depender del escapado de SQL ni cambiar valores históricos.
            const string espacios = "090A0B0C0D20C285C2A0E19A80E28080E28081E28082E28083E28084E28085E28086E28087E28088E28089E2808AE280A8E280A9E280AFE2819FE38080";
            migrationBuilder.Sql($"""
                INSERT INTO `__mod002_validacion_series` (`Compatible`)
                WITH series AS (
                    SELECT `NumeroSerie`, REGEXP_REPLACE(`NumeroSerie`,
                        CONCAT('^[', CONVERT(0x{espacios} USING utf8mb4), ']+|[',
                            CONVERT(0x{espacios} USING utf8mb4), ']+$'), '')
                        COLLATE utf8mb4_0900_bin AS `Recortada`
                    FROM `{tabla}`
                )
                SELECT IF(
                    EXISTS (SELECT 1 FROM series
                        WHERE OCTET_LENGTH(CONVERT(`Recortada` USING utf16)) > 510
                            OR `Recortada` = ''
                            OR BINARY `NumeroSerie` <> BINARY `Recortada`)
                    OR EXISTS (SELECT 1 FROM series GROUP BY `Recortada` HAVING COUNT(*) > 1),
                    0, 1);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Sensores_NumeroSerie",
                table: "Sensores");

            migrationBuilder.DropIndex(
                name: "UX_Inversores_NumeroSerie",
                table: "Inversores");

            migrationBuilder.Sql("ALTER TABLE `Sensores` MODIFY `NumeroSerie` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL;");
            migrationBuilder.Sql("ALTER TABLE `Inversores` MODIFY `NumeroSerie` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL;");
        }
    }
}
