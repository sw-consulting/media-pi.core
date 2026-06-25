using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaPi.Core.Migrations
{
    /// <inheritdoc />
    public partial class _0_10_2_Unique_PlaylistDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE playlists
                SET title = regexp_replace(title, '^[[:space:]]+|[[:space:]]+$', '', 'g');

                DO $$
                DECLARE
                    duplicate_playlist RECORD;
                    base_title text;
                    suffix integer;
                    candidate_title text;
                BEGIN
                    FOR duplicate_playlist IN
                        WITH ranked_playlists AS (
                            SELECT
                                id,
                                account_id,
                                title,
                                row_number() OVER (
                                    PARTITION BY account_id, title
                                    ORDER BY id
                                ) AS duplicate_ordinal
                            FROM playlists
                        )
                        SELECT id, account_id, title, duplicate_ordinal
                        FROM ranked_playlists
                        WHERE duplicate_ordinal > 1
                        ORDER BY account_id, title, id
                    LOOP
                        base_title := duplicate_playlist.title;
                        suffix := duplicate_playlist.duplicate_ordinal;

                        LOOP
                            candidate_title := base_title || ' (' || suffix::text || ')';
                            EXIT WHEN NOT EXISTS (
                                SELECT 1
                                FROM playlists p
                                WHERE p.account_id = duplicate_playlist.account_id
                                  AND p.title = candidate_title
                                  AND p.id <> duplicate_playlist.id
                            );
                            suffix := suffix + 1;
                        END LOOP;

                        UPDATE playlists
                        SET title = candidate_title
                        WHERE id = duplicate_playlist.id;
                    END LOOP;
                END $$;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_playlists_account_id_title",
                table: "playlists",
                columns: new[] { "account_id", "title" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_playlists_account_id_title",
                table: "playlists");
        }
    }
}
