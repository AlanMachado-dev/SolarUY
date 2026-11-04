using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SolarUY.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class IncorporarMedicion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Medicion",
                columns: table => new
                {
                    ID = table.Column<string>(type: "char(36)", fixedLength: true, maxLength: 36, nullable: false),
                    IdPanel = table.Column<string>(type: "char(36)", fixedLength: true, maxLength: 36, nullable: false),
                    FechaUtc = table.Column<long>(type: "bigint", nullable: false),
                    GeneracionActualW = table.Column<float>(type: "float", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Medicion", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Medicion_Paneles_IdPanel",
                        column: x => x.IdPanel,
                        principalTable: "Paneles",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Medicion_IdPanel_FechaUtc_ID",
                table: "Medicion",
                columns: new[] { "IdPanel", "FechaUtc", "ID" },
                descending: new[] { false, true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Medicion");
        }
    }
}
