using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Remotely.Server.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class AddCameraSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CameraDirectory",
                table: "Devices",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CameraPauseWindows",
                table: "Devices",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CameraRetentionCount",
                table: "Devices",
                type: "integer",
                nullable: false,
                defaultValue: 200);

            migrationBuilder.AddColumn<int>(
                name: "CameraRetentionHours",
                table: "Devices",
                type: "integer",
                nullable: false,
                defaultValue: 24);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CameraDirectory",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "CameraPauseWindows",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "CameraRetentionCount",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "CameraRetentionHours",
                table: "Devices");
        }
    }
}
