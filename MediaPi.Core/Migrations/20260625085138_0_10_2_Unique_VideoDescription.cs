using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaPi.Core.Migrations
{
    /// <inheritdoc />
    public partial class _0_10_2_Unique_VideoDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE videos
                SET title = COALESCE(
                    NULLIF(regexp_replace(title, '^[[:space:]]+|[[:space:]]+$', '', 'g'), ''),
                    original_filename
                );

                DO $$
                DECLARE
                    duplicate_video RECORD;
                    base_title text;
                    suffix integer;
                    candidate_title text;
                BEGIN
                    FOR duplicate_video IN
                        WITH ranked_videos AS (
                            SELECT
                                id,
                                account_id,
                                category_id,
                                title,
                                row_number() OVER (
                                    PARTITION BY
                                        CASE
                                            WHEN account_id IS NOT NULL THEN 'account'
                                            WHEN category_id IS NOT NULL THEN 'category'
                                            ELSE 'common'
                                        END,
                                        account_id,
                                        category_id,
                                        title
                                    ORDER BY id
                                ) AS duplicate_ordinal
                            FROM videos
                        )
                        SELECT id, account_id, category_id, title, duplicate_ordinal
                        FROM ranked_videos
                        WHERE duplicate_ordinal > 1
                        ORDER BY account_id, category_id, title, id
                    LOOP
                        base_title := duplicate_video.title;
                        suffix := duplicate_video.duplicate_ordinal;

                        LOOP
                            candidate_title := base_title || ' (' || suffix::text || ')';
                            EXIT WHEN NOT EXISTS (
                                SELECT 1
                                FROM videos v
                                WHERE v.title = candidate_title
                                  AND v.id <> duplicate_video.id
                                  AND (
                                      (
                                          duplicate_video.account_id IS NOT NULL
                                          AND v.account_id = duplicate_video.account_id
                                      )
                                      OR (
                                          duplicate_video.account_id IS NULL
                                          AND duplicate_video.category_id IS NOT NULL
                                          AND v.account_id IS NULL
                                          AND v.category_id = duplicate_video.category_id
                                      )
                                      OR (
                                          duplicate_video.account_id IS NULL
                                          AND duplicate_video.category_id IS NULL
                                          AND v.account_id IS NULL
                                          AND v.category_id IS NULL
                                      )
                                  )
                            );
                            suffix := suffix + 1;
                        END LOOP;

                        UPDATE videos
                        SET title = candidate_title
                        WHERE id = duplicate_video.id;
                    END LOOP;
                END $$;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_videos_account_id_title",
                table: "videos",
                columns: new[] { "account_id", "title" },
                unique: true,
                filter: "\"account_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_videos_category_id_title",
                table: "videos",
                columns: new[] { "category_id", "title" },
                unique: true,
                filter: "\"account_id\" IS NULL AND \"category_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_videos_common_uncategorized_title",
                table: "videos",
                column: "title",
                unique: true,
                filter: "\"account_id\" IS NULL AND \"category_id\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_videos_account_id_title",
                table: "videos");

            migrationBuilder.DropIndex(
                name: "IX_videos_category_id_title",
                table: "videos");

            migrationBuilder.DropIndex(
                name: "IX_videos_common_uncategorized_title",
                table: "videos");
        }
    }
}
