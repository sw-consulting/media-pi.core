using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaPi.Core.Migrations
{
    /// <inheritdoc />
    public partial class _0_11_0_SubscriptionAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_subscriptions_account_id",
                table: "subscriptions");

            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT
                        id,
                        ROW_NUMBER() OVER (
                            PARTITION BY account_id, category_id
                            ORDER BY end_time DESC, start_time DESC, id DESC
                        ) AS rn
                    FROM subscriptions
                )
                DELETE FROM subscriptions
                WHERE id IN (SELECT id FROM ranked WHERE rn > 1);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_account_id_category_id",
                table: "subscriptions",
                columns: new[] { "account_id", "category_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_subscriptions_account_id_category_id",
                table: "subscriptions");

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_account_id",
                table: "subscriptions",
                column: "account_id");
        }
    }
}
