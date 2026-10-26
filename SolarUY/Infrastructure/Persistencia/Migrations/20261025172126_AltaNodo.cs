using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SolarUY.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AltaNodo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Nodos",
                columns: table => new
                {
                    ID = table.Column<string>(type: "char(36)", fixedLength: true, maxLength: 36, nullable: false),
                    Nombre = table.Column<string>(type: "longtext", nullable: false),
                    Latitud = table.Column<float>(type: "float", nullable: false),
                    Longitud = table.Column<float>(type: "float", nullable: false),
                    IdParque = table.Column<string>(type: "char(36)", fixedLength: true, maxLength: 36, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Nodos", x => x.ID);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Inversores",
                columns: table => new
                {
                    ID = table.Column<string>(type: "char(36)", fixedLength: true, maxLength: 36, nullable: false),
                    Nombre = table.Column<string>(type: "longtext", nullable: false),
                    NumeroSerie = table.Column<string>(type: "longtext", nullable: false),
                    IdNodo = table.Column<string>(type: "char(36)", fixedLength: true, maxLength: 36, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inversores", x => x.ID);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Sensores",
                columns: table => new
                {
                    ID = table.Column<string>(type: "char(36)", fixedLength: true, maxLength: 36, nullable: false),
                    Nombre = table.Column<string>(type: "longtext", nullable: false),
                    NumeroSerie = table.Column<string>(type: "longtext", nullable: false),
                    Endpoint = table.Column<string>(type: "longtext", nullable: true),
                    IdInversor = table.Column<string>(type: "char(36)", fixedLength: true, maxLength: 36, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sensores", x => x.ID);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            // El proveedor 10.0.9 omite charset/colación al crear tablas. La migración
            // inicial usa utf8mb4_0900_ai_ci para el ID del parque; igualar las tablas
            // nuevas antes de incorporar FKs evita heredar la colación de la base.
            migrationBuilder.Sql("ALTER TABLE `Nodos` ENGINE=InnoDB, CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;");
            migrationBuilder.Sql("ALTER TABLE `Inversores` ENGINE=InnoDB, CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;");
            migrationBuilder.Sql("ALTER TABLE `Sensores` ENGINE=InnoDB, CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;");
            migrationBuilder.AddForeignKey("FK_Nodos_ParquesSolares_IdParque", "Nodos", "IdParque",
                "ParquesSolares", principalColumn: "ID", onDelete: ReferentialAction.Cascade);
            migrationBuilder.AddForeignKey("FK_Inversores_Nodos_IdNodo", "Inversores", "IdNodo",
                "Nodos", principalColumn: "ID", onDelete: ReferentialAction.Cascade);
            migrationBuilder.AddForeignKey("FK_Sensores_Inversores_IdInversor", "Sensores", "IdInversor",
                "Inversores", principalColumn: "ID", onDelete: ReferentialAction.Cascade);

            migrationBuilder.CreateIndex(
                name: "IX_Inversores_IdNodo",
                table: "Inversores",
                column: "IdNodo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Nodos_IdParque",
                table: "Nodos",
                column: "IdParque");

            migrationBuilder.CreateIndex(
                name: "IX_Sensores_IdInversor",
                table: "Sensores",
                column: "IdInversor",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Sensores");

            migrationBuilder.DropTable(
                name: "Inversores");

            migrationBuilder.DropTable(
                name: "Nodos");
        }
    }
}
