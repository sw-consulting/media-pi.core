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
            migrationBuilder.DropForeignKey(
                name: "FK_subscriptions_categories_category_id",
                table: "subscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_videos_categories_category_id",
                table: "videos");

            migrationBuilder.DropColumn(
                name: "name",
                table: "subscriptions");

            migrationBuilder.AddColumn<bool>(
                name: "free",
                table: "categories",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "IX_categories_title",
                table: "categories",
                column: "title",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_subscriptions_categories_category_id",
                table: "subscriptions",
                column: "category_id",
                principalTable: "categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_videos_categories_category_id",
                table: "videos",
                column: "category_id",
                principalTable: "categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_subscriptions_categories_category_id",
                table: "subscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_videos_categories_category_id",
                table: "videos");

            migrationBuilder.DropIndex(
                name: "IX_categories_title",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "free",
                table: "categories");

            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "subscriptions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddForeignKey(
                name: "FK_subscriptions_categories_category_id",
                table: "subscriptions",
                column: "category_id",
                principalTable: "categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_videos_categories_category_id",
                table: "videos",
                column: "category_id",
                principalTable: "categories",
                principalColumn: "id");
        }
    }
}
