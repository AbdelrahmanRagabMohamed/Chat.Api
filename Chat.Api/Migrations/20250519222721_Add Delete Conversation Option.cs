using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chat.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDeleteConversationOption : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted_ForUser_1",
                table: "Conversations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted_ForUser_2",
                table: "Conversations",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDeleted_ForUser_1",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "IsDeleted_ForUser_2",
                table: "Conversations");
        }
    }
}
