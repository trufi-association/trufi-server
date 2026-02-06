using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gateway.Shared.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHeadersAndContentType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "StatusCode",
                table: "requests",
                newName: "status_code");

            migrationBuilder.RenameColumn(
                name: "ResponseBody",
                table: "requests",
                newName: "response_body");

            migrationBuilder.AddColumn<string>(
                name: "request_content_type",
                table: "requests",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "request_headers",
                table: "requests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "response_content_type",
                table: "requests",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "response_headers",
                table: "requests",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_requests_request_content_type",
                table: "requests",
                column: "request_content_type");

            migrationBuilder.CreateIndex(
                name: "IX_requests_response_content_type",
                table: "requests",
                column: "response_content_type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_requests_request_content_type",
                table: "requests");

            migrationBuilder.DropIndex(
                name: "IX_requests_response_content_type",
                table: "requests");

            migrationBuilder.DropColumn(
                name: "request_content_type",
                table: "requests");

            migrationBuilder.DropColumn(
                name: "request_headers",
                table: "requests");

            migrationBuilder.DropColumn(
                name: "response_content_type",
                table: "requests");

            migrationBuilder.DropColumn(
                name: "response_headers",
                table: "requests");

            migrationBuilder.RenameColumn(
                name: "status_code",
                table: "requests",
                newName: "StatusCode");

            migrationBuilder.RenameColumn(
                name: "response_body",
                table: "requests",
                newName: "ResponseBody");
        }
    }
}
