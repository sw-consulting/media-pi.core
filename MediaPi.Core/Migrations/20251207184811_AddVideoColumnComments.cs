using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaPi.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoColumnComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "file_size_bytes",
                table: "videos",
                type: "bigint",
                nullable: false,
                comment: "Stores uint values (0 to 4,294,967,295) in bigint column for EF Core compatibility",
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<long>(
                name: "duration_seconds",
                table: "videos",
                type: "bigint",
                nullable: true,
                comment: "Stores uint values (0 to 4,294,967,295) in bigint column for EF Core compatibility",
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "file_size_bytes",
                table: "videos",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldComment: "Stores uint values (0 to 4,294,967,295) in bigint column for EF Core compatibility");

            migrationBuilder.AlterColumn<long>(
                name: "duration_seconds",
                table: "videos",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true,
                oldComment: "Stores uint values (0 to 4,294,967,295) in bigint column for EF Core compatibility");
        }
    }
}
