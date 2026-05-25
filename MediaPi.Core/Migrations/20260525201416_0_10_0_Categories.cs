using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaPi.Core.Migrations
{
    /// <inheritdoc />
    public partial class _0_10_0_Categories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "free",
                table: "categories",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "free",
                table: "categories");
        }
    }
}
