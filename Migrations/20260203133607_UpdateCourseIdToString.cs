using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication_Credit_hour_system.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCourseIdToString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "COURSEcode",
                table: "Courses",
                newName: "CourseCode");

            migrationBuilder.AddColumn<string>(
                name: "CourseID",
                table: "CourseOffering",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_CourseRegistration_OfferingID",
                table: "CourseRegistration",
                column: "OfferingID");

            migrationBuilder.AddForeignKey(
                name: "FK_CourseRegistration_CourseOffering_OfferingID",
                table: "CourseRegistration",
                column: "OfferingID",
                principalTable: "CourseOffering",
                principalColumn: "OfferingID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CourseRegistration_CourseOffering_OfferingID",
                table: "CourseRegistration");

            migrationBuilder.DropIndex(
                name: "IX_CourseRegistration_OfferingID",
                table: "CourseRegistration");

            migrationBuilder.DropColumn(
                name: "CourseID",
                table: "CourseOffering");

            migrationBuilder.RenameColumn(
                name: "CourseCode",
                table: "Courses",
                newName: "COURSEcode");
        }
    }
}
