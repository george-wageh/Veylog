using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Veylog.Migrations
{
    /// <inheritdoc />
    public partial class SqlOperation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SqlOperation",
                schema: "Veylog",
                table: "SqlLogs",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SqlOperation",
                schema: "Veylog",
                table: "SqlLogs");
        }
    }
}
