using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gateway.Shared.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddResponseFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ResponseBody",
                table: "requests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatusCode",
                table: "requests",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResponseBody",
                table: "requests");

            migrationBuilder.DropColumn(
                name: "StatusCode",
                table: "requests");
        }
    }
}
