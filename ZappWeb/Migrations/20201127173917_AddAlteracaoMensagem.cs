using Microsoft.EntityFrameworkCore.Migrations;

namespace ZappWeb.Migrations
{
    public partial class AddAlteracaoMensagem : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Usuario",
                table: "Mensagens",
                newName: "UsuarioJson");

            migrationBuilder.AddColumn<string>(
                name: "UsuarioId",
                table: "Mensagens",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UsuarioId",
                table: "Mensagens");

            migrationBuilder.RenameColumn(
                name: "UsuarioJson",
                table: "Mensagens",
                newName: "Usuario");
        }
    }
}
