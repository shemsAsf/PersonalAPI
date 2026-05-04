using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddedAdminLogins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Id",
                table: "EkaterinaDesignChat",
                newName: "UserId");

            migrationBuilder.CreateTable(
                name: "EkaterinaDesignAdminLogin",
                columns: table => new
                {
                    Username = table.Column<string>(type: "text", nullable: false),
                    Password = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EkaterinaDesignAdminLogin", x => x.Username);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EkaterinaDesignAdminLogin");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "EkaterinaDesignChat",
                newName: "Id");
        }
    }
}
