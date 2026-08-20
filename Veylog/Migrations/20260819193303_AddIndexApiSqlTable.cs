using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Veylog.Migrations
{
    /// <inheritdoc />
    public partial class AddIndexApiSqlTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TraceId",
                schema: "Veylog",
                table: "SqlLogs",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TraceId",
                schema: "Veylog",
                table: "ApiLogs",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Path",
                schema: "Veylog",
                table: "ApiLogs",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_SqlLogs_CreatedAt",
                schema: "Veylog",
                table: "SqlLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SqlLogs_TraceId",
                schema: "Veylog",
                table: "SqlLogs",
                column: "TraceId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiLogs_CreatedAt",
                schema: "Veylog",
                table: "ApiLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ApiLogs_Path",
                schema: "Veylog",
                table: "ApiLogs",
                column: "Path");

            migrationBuilder.CreateIndex(
                name: "IX_ApiLogs_Path_CreatedAt",
                schema: "Veylog",
                table: "ApiLogs",
                columns: new[] { "Path", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ApiLogs_TraceId",
                schema: "Veylog",
                table: "ApiLogs",
                column: "TraceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SqlLogs_CreatedAt",
                schema: "Veylog",
                table: "SqlLogs");

            migrationBuilder.DropIndex(
                name: "IX_SqlLogs_TraceId",
                schema: "Veylog",
                table: "SqlLogs");

            migrationBuilder.DropIndex(
                name: "IX_ApiLogs_CreatedAt",
                schema: "Veylog",
                table: "ApiLogs");

            migrationBuilder.DropIndex(
                name: "IX_ApiLogs_Path",
                schema: "Veylog",
                table: "ApiLogs");

            migrationBuilder.DropIndex(
                name: "IX_ApiLogs_Path_CreatedAt",
                schema: "Veylog",
                table: "ApiLogs");

            migrationBuilder.DropIndex(
                name: "IX_ApiLogs_TraceId",
                schema: "Veylog",
                table: "ApiLogs");

            migrationBuilder.AlterColumn<string>(
                name: "TraceId",
                schema: "Veylog",
                table: "SqlLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TraceId",
                schema: "Veylog",
                table: "ApiLogs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Path",
                schema: "Veylog",
                table: "ApiLogs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
