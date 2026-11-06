using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Remotely.Server.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class Add_ScreenshotPauseWindows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScreenshotPauseEndHour",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "ScreenshotPauseStartHour",
                table: "Devices");

            migrationBuilder.AddColumn<string>(
                name: "ScreenshotPauseWindows",
                table: "Devices",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScreenshotPauseWindows",
                table: "Devices");

            migrationBuilder.AddColumn<int>(
                name: "ScreenshotPauseEndHour",
                table: "Devices",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ScreenshotPauseStartHour",
                table: "Devices",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
