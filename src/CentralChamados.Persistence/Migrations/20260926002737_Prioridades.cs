using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentralChamados.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Prioridades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Prioridade",
                table: "Chamados",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_Chamados_Prioridade",
                table: "Chamados",
                column: "Prioridade");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Chamados_Prioridade",
                table: "Chamados",
                sql: "Prioridade BETWEEN 0 AND 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Chamados_Prioridade",
                table: "Chamados");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Chamados_Prioridade",
                table: "Chamados");

            migrationBuilder.DropColumn(
                name: "Prioridade",
                table: "Chamados");
        }
    }
}
