using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SolarUY.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class MantenimientoPanel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "RevisionControl",
                table: "Paneles",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "Mantenimientos",
                columns: table => new
                {
                    ID = table.Column<string>(type: "char(36)", fixedLength: true, maxLength: 36, nullable: false),
                    IdPanel = table.Column<string>(type: "char(36)", fixedLength: true, maxLength: 36, nullable: false),
                    Tipo = table.Column<string>(type: "longtext", nullable: false),
                    FechaProgramada = table.Column<long>(type: "bigint", nullable: false),
                    Responsable = table.Column<string>(type: "longtext", nullable: false),
                    InicioProcesadoUtc = table.Column<long>(type: "bigint", nullable: true),
                    FechaRealizada = table.Column<long>(type: "bigint", nullable: true),
                    Observaciones = table.Column<string>(type: "longtext", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mantenimientos", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Mantenimientos_Paneles_IdPanel",
                        column: x => x.IdPanel,
                        principalTable: "Paneles",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Mantenimientos_IdPanel",
                table: "Mantenimientos",
                column: "IdPanel");

            migrationBuilder.CreateIndex(
                name: "IX_Mantenimientos_InicioProcesadoUtc_FechaRealizada_FechaProgra~",
                table: "Mantenimientos",
                columns: new[] { "InicioProcesadoUtc", "FechaRealizada", "FechaProgramada" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Mantenimientos");

            migrationBuilder.DropColumn(
                name: "RevisionControl",
                table: "Paneles");
        }
    }
}
