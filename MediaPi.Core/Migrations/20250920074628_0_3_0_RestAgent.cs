using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaPi.Core.Migrations
{
    /// <inheritdoc />
    public partial class _0_3_0_RestAgent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "port",
                table: "devices",
                type: "text",
                nullable: false,
                defaultValue: "8080");

            migrationBuilder.AddColumn<string>(
                name: "server_key",
                table: "devices",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "port",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "server_key",
                table: "devices");
        }
    }
}
