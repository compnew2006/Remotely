using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Remotely.Server.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddMediaStorageDirectory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StorageDirectory",
                table: "Screenshots",
                type: "TEXT",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StorageDirectory",
                table: "CameraClips",
                type: "TEXT",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StorageDirectory",
                table: "AudioClips",
                type: "TEXT",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StorageDirectory",
                table: "Screenshots");

            migrationBuilder.DropColumn(
                name: "StorageDirectory",
                table: "CameraClips");

            migrationBuilder.DropColumn(
                name: "StorageDirectory",
                table: "AudioClips");
        }
    }
}
