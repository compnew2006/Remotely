using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Remotely.Server.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class AddAudioClips : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AudioDirectory",
                table: "Devices",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AudioPauseWindows",
                table: "Devices",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AudioRetentionCount",
                table: "Devices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AudioRetentionHours",
                table: "Devices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AudioClips",
                columns: table => new
                {
                    ID = table.Column<string>(type: "text", nullable: false),
                    DeviceID = table.Column<string>(type: "text", nullable: false),
                    TimeStamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Quality = table.Column<string>(type: "text", nullable: false),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioClips", x => x.ID);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AudioClips");

            migrationBuilder.DropColumn(
                name: "AudioDirectory",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "AudioPauseWindows",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "AudioRetentionCount",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "AudioRetentionHours",
                table: "Devices");
        }
    }
}
