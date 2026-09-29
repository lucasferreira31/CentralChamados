using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentralChamados.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                    table.CheckConstraint("CK_Usuarios_Nome", "length(trim(Nome)) BETWEEN 1 AND 100");
                });

            migrationBuilder.CreateTable(
                name: "Chamados",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Titulo = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Descricao = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    SolicitanteId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Solicitante = table.Column<string>(type: "TEXT", nullable: false),
                    TecnicoId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Tecnico = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Solucao = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chamados", x => x.Id);
                    table.CheckConstraint("CK_Chamados_Descricao", "length(trim(Descricao)) BETWEEN 1 AND 2000");
                    table.CheckConstraint("CK_Chamados_Estado", "(Status = 'Aberto' AND TecnicoId IS NULL AND Tecnico IS NULL AND Solucao IS NULL) OR (Status = 'EmAtendimento' AND TecnicoId IS NOT NULL AND Tecnico IS NOT NULL AND Solucao IS NULL) OR (Status = 'Resolvido' AND TecnicoId IS NOT NULL AND Tecnico IS NOT NULL AND Solucao IS NOT NULL AND length(trim(Solucao)) BETWEEN 1 AND 2000)");
                    table.CheckConstraint("CK_Chamados_Titulo", "length(trim(Titulo)) BETWEEN 1 AND 100");
                    table.ForeignKey(
                        name: "FK_Chamados_Usuarios_SolicitanteId",
                        column: x => x.SolicitanteId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Chamados_Usuarios_TecnicoId",
                        column: x => x.TecnicoId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Eventos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sequencia = table.Column<int>(type: "INTEGER", nullable: false),
                    Em = table.Column<long>(type: "INTEGER", nullable: false),
                    AutorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Autor = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Descricao = table.Column<string>(type: "TEXT", maxLength: 2100, nullable: false),
                    ChamadoId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Eventos", x => x.Id);
                    table.CheckConstraint("CK_Eventos_Sequencia", "Sequencia >= 0");
                    table.ForeignKey(
                        name: "FK_Eventos_Chamados_ChamadoId",
                        column: x => x.ChamadoId,
                        principalTable: "Chamados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Eventos_Usuarios_AutorId",
                        column: x => x.AutorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Chamados_SolicitanteId",
                table: "Chamados",
                column: "SolicitanteId");

            migrationBuilder.CreateIndex(
                name: "IX_Chamados_Status",
                table: "Chamados",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Chamados_TecnicoId",
                table: "Chamados",
                column: "TecnicoId");

            migrationBuilder.CreateIndex(
                name: "IX_Eventos_AutorId",
                table: "Eventos",
                column: "AutorId");

            migrationBuilder.CreateIndex(
                name: "IX_Eventos_ChamadoId_Sequencia",
                table: "Eventos",
                columns: new[] { "ChamadoId", "Sequencia" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Eventos");

            migrationBuilder.DropTable(
                name: "Chamados");

            migrationBuilder.DropTable(
                name: "Usuarios");
        }
    }
}
