using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Remotely.Server.Migrations.PostgreSql
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
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StorageDirectory",
                table: "CameraClips",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StorageDirectory",
                table: "AudioClips",
                type: "character varying(2048)",
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
