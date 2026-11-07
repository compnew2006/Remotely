using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Remotely.Server.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddKeyboardLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "KeyboardEnabled",
                table: "Devices",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "KeyboardPauseWindows",
                table: "Devices",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KeyboardRetentionCount",
                table: "Devices",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "KeyboardRetentionHours",
                table: "Devices",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "KeyboardLogs",
                columns: table => new
                {
                    ID = table.Column<string>(type: "TEXT", nullable: false),
                    DeviceID = table.Column<string>(type: "TEXT", nullable: false),
                    TimeStamp = table.Column<string>(type: "TEXT", nullable: false),
                    WindowTitle = table.Column<string>(type: "TEXT", nullable: false),
                    ProcessName = table.Column<string>(type: "TEXT", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    IsPasswordField = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KeyboardLogs", x => x.ID);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KeyboardLogs");

            migrationBuilder.DropColumn(
                name: "KeyboardEnabled",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "KeyboardPauseWindows",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "KeyboardRetentionCount",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "KeyboardRetentionHours",
                table: "Devices");
        }
    }
}
