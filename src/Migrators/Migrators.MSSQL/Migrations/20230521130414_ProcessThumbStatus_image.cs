using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CleanArchitecture.Blazor.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class ProcessThumbStatus_image : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DetectObjectStatus",
                table: "Images",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProcessThumbStatus",
                table: "Images",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VerifyFaceStatus",
                table: "Images",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DetectObjectStatus",
                table: "Images");

            migrationBuilder.DropColumn(
                name: "ProcessThumbStatus",
                table: "Images");

            migrationBuilder.DropColumn(
                name: "VerifyFaceStatus",
                table: "Images");
        }
    }
}
