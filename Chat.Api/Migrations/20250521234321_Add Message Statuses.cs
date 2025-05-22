using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chat.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageStatuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsReceived",
                table: "Messages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSent",
                table: "Messages",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsReceived",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "IsSent",
                table: "Messages");
        }
    }
}
