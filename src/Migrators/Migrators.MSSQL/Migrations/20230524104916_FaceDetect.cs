using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CleanArchitecture.Blazor.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class FaceDetect : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DetectFaceStatus",
                table: "Images",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "FaceDetectLastUpdated",
                table: "Images",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FaceDetections",
                table: "Images",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FaceVerifyLastUpdated",
                table: "Images",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DetectFaceStatus",
                table: "Images");

            migrationBuilder.DropColumn(
                name: "FaceDetectLastUpdated",
                table: "Images");

            migrationBuilder.DropColumn(
                name: "FaceDetections",
                table: "Images");

            migrationBuilder.DropColumn(
                name: "FaceVerifyLastUpdated",
                table: "Images");
        }
    }
}
