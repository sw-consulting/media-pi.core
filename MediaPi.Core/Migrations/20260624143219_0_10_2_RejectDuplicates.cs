using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaPi.Core.Migrations
{
    /// <inheritdoc />
    public partial class _0_10_2_RejectDuplicates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_videos_account_id",
                table: "videos");

            migrationBuilder.DropIndex(
                name: "IX_videos_category_id",
                table: "videos");

            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    duplicate_video RECORD;
                    extension_start integer;
                    filename_stem text;
                    filename_extension text;
                    suffix integer;
                    candidate_filename text;
                BEGIN
                    FOR duplicate_video IN
                        WITH scoped_videos AS (
                            SELECT
                                id,
                                original_filename,
                                account_id,
                                category_id,
                                CASE
                                    WHEN account_id IS NOT NULL THEN 'account'
                                    WHEN category_id IS NOT NULL THEN 'category'
                                    ELSE 'common'
                                END AS container_type,
                                CASE
                                    WHEN account_id IS NOT NULL THEN account_id
                                    WHEN category_id IS NOT NULL THEN category_id
                                    ELSE 0
                                END AS container_id,
                                row_number() OVER (
                                    PARTITION BY
                                        CASE
                                            WHEN account_id IS NOT NULL THEN 'account'
                                            WHEN category_id IS NOT NULL THEN 'category'
                                            ELSE 'common'
                                        END,
                                        CASE
                                            WHEN account_id IS NOT NULL THEN account_id
                                            WHEN category_id IS NOT NULL THEN category_id
                                            ELSE 0
                                        END,
                                        original_filename
                                    ORDER BY id
                                ) AS duplicate_ordinal
                            FROM videos
                            WHERE original_filename IS NOT NULL
                        )
                        SELECT id, original_filename, account_id, category_id, container_type, container_id
                        FROM scoped_videos
                        WHERE duplicate_ordinal > 1
                        ORDER BY container_type, container_id, original_filename, id
                    LOOP
                        extension_start := length(duplicate_video.original_filename) - strpos(reverse(duplicate_video.original_filename), '.') + 1;
                        IF extension_start > 1 THEN
                            filename_stem := substring(duplicate_video.original_filename from 1 for extension_start - 1);
                            filename_extension := substring(duplicate_video.original_filename from extension_start);
                        ELSE
                            filename_stem := duplicate_video.original_filename;
                            filename_extension := '';
                        END IF;

                        suffix := 1;
                        LOOP
                            candidate_filename := filename_stem || '-' || suffix::text || filename_extension;
                            EXIT WHEN NOT EXISTS (
                                SELECT 1
                                FROM videos v
                                WHERE v.original_filename = candidate_filename
                                  AND (
                                      (duplicate_video.account_id IS NOT NULL AND v.account_id = duplicate_video.account_id)
                                      OR (duplicate_video.account_id IS NULL AND duplicate_video.category_id IS NOT NULL AND v.account_id IS NULL AND v.category_id = duplicate_video.category_id)
                                      OR (duplicate_video.account_id IS NULL AND duplicate_video.category_id IS NULL AND v.account_id IS NULL AND v.category_id IS NULL)
                                  )
                            );
                            suffix := suffix + 1;
                        END LOOP;

                        UPDATE videos
                        SET original_filename = candidate_filename
                        WHERE id = duplicate_video.id;
                    END LOOP;
                END $$;
                """);
            migrationBuilder.CreateIndex(
                name: "IX_videos_account_id_original_filename",
                table: "videos",
                columns: new[] { "account_id", "original_filename" },
                unique: true,
                filter: "\"account_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_videos_category_id_original_filename",
                table: "videos",
                columns: new[] { "category_id", "original_filename" },
                unique: true,
                filter: "\"account_id\" IS NULL AND \"category_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_videos_common_uncategorized_original_filename",
                table: "videos",
                column: "original_filename",
                unique: true,
                filter: "\"account_id\" IS NULL AND \"category_id\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_videos_account_id_original_filename",
                table: "videos");

            migrationBuilder.DropIndex(
                name: "IX_videos_category_id_original_filename",
                table: "videos");

            migrationBuilder.DropIndex(
                name: "IX_videos_common_uncategorized_original_filename",
                table: "videos");

            migrationBuilder.CreateIndex(
                name: "IX_videos_account_id",
                table: "videos",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_videos_category_id",
                table: "videos",
                column: "category_id");
        }
    }
}
