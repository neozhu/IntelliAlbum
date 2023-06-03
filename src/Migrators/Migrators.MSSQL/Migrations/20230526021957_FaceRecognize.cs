using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CleanArchitecture.Blazor.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class FaceRecognize : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "VerifyFaceStatus",
                table: "Images",
                newName: "RecognizeFaceStatus");

            migrationBuilder.RenameColumn(
                name: "FaceVerifyLastUpdated",
                table: "Images",
                newName: "FaceRecognizeLastUpdated");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RecognizeFaceStatus",
                table: "Images",
                newName: "VerifyFaceStatus");

            migrationBuilder.RenameColumn(
                name: "FaceRecognizeLastUpdated",
                table: "Images",
                newName: "FaceVerifyLastUpdated");
        }
    }
}
