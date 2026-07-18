using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialCalc.Web.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDataMaxLength : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sheets_UserId",
                table: "Sheets");

            migrationBuilder.AlterColumn<string>(
                name: "Data",
                table: "Sheets",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(5242880)",
                oldMaxLength: 5242880);

            migrationBuilder.CreateIndex(
                name: "IX_Sheets_UserId_UpdatedAt",
                table: "Sheets",
                columns: new[] { "UserId", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sheets_UserId_UpdatedAt",
                table: "Sheets");

            migrationBuilder.AlterColumn<string>(
                name: "Data",
                table: "Sheets",
                type: "character varying(5242880)",
                maxLength: 5242880,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "IX_Sheets_UserId",
                table: "Sheets",
                column: "UserId");
        }
    }
}
