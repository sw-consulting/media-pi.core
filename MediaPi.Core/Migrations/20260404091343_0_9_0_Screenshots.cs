using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaPi.Core.Migrations
{
    /// <inheritdoc />
    public partial class _0_9_0_Screenshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "timestamp",
                table: "screenshots",
                newName: "time_created");

            migrationBuilder.AddColumn<long>(
                name: "file_size_bytes",
                table: "screenshots",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "filename",
                table: "screenshots",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "original_filename",
                table: "screenshots",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_screenshots_filename",
                table: "screenshots",
                column: "filename",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_screenshots_filename",
                table: "screenshots");

            migrationBuilder.DropColumn(
                name: "file_size_bytes",
                table: "screenshots");

            migrationBuilder.DropColumn(
                name: "filename",
                table: "screenshots");

            migrationBuilder.DropColumn(
                name: "original_filename",
                table: "screenshots");

            migrationBuilder.RenameColumn(
                name: "time_created",
                table: "screenshots",
                newName: "timestamp");
        }
    }
}
