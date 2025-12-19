using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MediaPi.Core.Migrations
{
    /// <inheritdoc />
    public partial class _0_6_0_Extend_Video_and_Playlist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_videos_accounts_account_id",
                table: "videos");

            migrationBuilder.DropPrimaryKey(
                name: "PK_video_playlists",
                table: "video_playlists");

            migrationBuilder.DropIndex(
                name: "IX_playlists_account_id",
                table: "playlists");

            migrationBuilder.AlterColumn<int>(
                name: "account_id",
                table: "videos",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<long>(
                name: "duration_seconds",
                table: "videos",
                type: "bigint",
                nullable: true,
                comment: "Stores uint values (0 to 4,294,967,295) in bigint column for EF Core compatibility");

            migrationBuilder.AddColumn<long>(
                name: "file_size_bytes",
                table: "videos",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                comment: "Stores uint values (0 to 4,294,967,295) in bigint column for EF Core compatibility");

            migrationBuilder.AddColumn<string>(
                name: "original_filename",
                table: "videos",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "id",
                table: "video_playlists",
                type: "integer",
                nullable: false,
                defaultValue: 0)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<int>(
                name: "position",
                table: "video_playlists",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_video_playlists",
                table: "video_playlists",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "IX_videos_filename",
                table: "videos",
                column: "filename",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_video_playlists_video_id",
                table: "video_playlists",
                column: "video_id");

            migrationBuilder.CreateIndex(
                name: "IX_playlists_account_id_filename",
                table: "playlists",
                columns: new[] { "account_id", "filename" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_videos_accounts_account_id",
                table: "videos",
                column: "account_id",
                principalTable: "accounts",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_videos_accounts_account_id",
                table: "videos");

            migrationBuilder.DropIndex(
                name: "IX_videos_filename",
                table: "videos");

            migrationBuilder.DropPrimaryKey(
                name: "PK_video_playlists",
                table: "video_playlists");

            migrationBuilder.DropIndex(
                name: "IX_video_playlists_video_id",
                table: "video_playlists");

            migrationBuilder.DropIndex(
                name: "IX_playlists_account_id_filename",
                table: "playlists");

            migrationBuilder.DropColumn(
                name: "duration_seconds",
                table: "videos");

            migrationBuilder.DropColumn(
                name: "file_size_bytes",
                table: "videos");

            migrationBuilder.DropColumn(
                name: "original_filename",
                table: "videos");

            migrationBuilder.DropColumn(
                name: "id",
                table: "video_playlists");

            migrationBuilder.DropColumn(
                name: "position",
                table: "video_playlists");

            migrationBuilder.AlterColumn<int>(
                name: "account_id",
                table: "videos",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_video_playlists",
                table: "video_playlists",
                columns: new[] { "video_id", "playlist_id" });

            migrationBuilder.CreateIndex(
                name: "IX_playlists_account_id",
                table: "playlists",
                column: "account_id");

            migrationBuilder.AddForeignKey(
                name: "FK_videos_accounts_account_id",
                table: "videos",
                column: "account_id",
                principalTable: "accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
