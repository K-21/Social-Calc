using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialCalc.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddSheetDataMaxLength : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Data",
                table: "Sheets",
                type: "character varying(5242880)",
                maxLength: 5242880,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Data",
                table: "Sheets",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(5242880)",
                oldMaxLength: 5242880);
        }
    }
}
