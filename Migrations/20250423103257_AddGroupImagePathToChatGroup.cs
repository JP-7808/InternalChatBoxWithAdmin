using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InternalChatbox.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupImagePathToChatGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GroupImagePath",
                table: "Groups",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GroupImagePath",
                table: "Groups");
        }
    }
}
