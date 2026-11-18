using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ArchtApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class ProcessModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "WorkItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "WorkItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GoogleEventId",
                table: "WorkItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VisitNumber",
                table: "WorkItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DesignApprovedOn",
                table: "Projects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DesignDueDate",
                table: "Projects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DesignWorkingDays",
                table: "Projects",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PermitCollectedOn",
                table: "Projects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PermitSubmittedOn",
                table: "Projects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PmcOutcome",
                table: "Projects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProjectInterest",
                table: "Projects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reference",
                table: "Projects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SiteAddress",
                table: "Projects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptNumber",
                table: "Payments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WorkItemId",
                table: "Documents",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Leads",
                columns: table => new
                {
                    LeadId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Phone = table.Column<string>(type: "TEXT", nullable: true),
                    Email = table.Column<string>(type: "TEXT", nullable: true),
                    SiteAddress = table.Column<string>(type: "TEXT", nullable: false),
                    ProjectInterest = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ConvertedProjectId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leads", x => x.LeadId);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Key);
                });

            migrationBuilder.InsertData(
                table: "Settings",
                columns: new[] { "Key", "Value" },
                values: new object[,]
                {
                    { "BusinessAddress", "" },
                    { "BusinessEmail", "" },
                    { "BusinessName", "BDL" },
                    { "BusinessPhone", "" },
                    { "ConsultationFee", "0" },
                    { "ReferencePrefix", "BDL" }
                });

            migrationBuilder.UpdateData(
                table: "Stages",
                keyColumn: "StageId",
                keyValue: 1,
                column: "Name",
                value: "Consultation");

            migrationBuilder.UpdateData(
                table: "Stages",
                keyColumn: "StageId",
                keyValue: 2,
                column: "Name",
                value: "Site Visit");

            migrationBuilder.UpdateData(
                table: "Stages",
                keyColumn: "StageId",
                keyValue: 3,
                column: "Name",
                value: "Preliminary Design");

            migrationBuilder.UpdateData(
                table: "Stages",
                keyColumn: "StageId",
                keyValue: 4,
                column: "Name",
                value: "Design Presentation");

            migrationBuilder.UpdateData(
                table: "Stages",
                keyColumn: "StageId",
                keyValue: 5,
                column: "Name",
                value: "Design Approval");

            migrationBuilder.InsertData(
                table: "Stages",
                columns: new[] { "StageId", "Name", "SortOrder" },
                values: new object[,]
                {
                    { 6, "Building Plans", 6 },
                    { 7, "PMC Review", 7 },
                    { 8, "Permit Submission", 8 },
                    { 9, "Permit Collection", 9 },
                    { 10, "Project Completion", 10 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Reference",
                table: "Projects",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_WorkItemId",
                table: "Documents",
                column: "WorkItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_WorkItems_WorkItemId",
                table: "Documents",
                column: "WorkItemId",
                principalTable: "WorkItems",
                principalColumn: "WorkItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_WorkItems_WorkItemId",
                table: "Documents");

            migrationBuilder.DropTable(
                name: "Leads");

            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropIndex(
                name: "IX_Projects_Reference",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Documents_WorkItemId",
                table: "Documents");

            migrationBuilder.DeleteData(
                table: "Stages",
                keyColumn: "StageId",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Stages",
                keyColumn: "StageId",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Stages",
                keyColumn: "StageId",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "Stages",
                keyColumn: "StageId",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "Stages",
                keyColumn: "StageId",
                keyValue: 10);

            migrationBuilder.DropColumn(
                name: "Address",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "GoogleEventId",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "VisitNumber",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "DesignApprovedOn",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "DesignDueDate",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "DesignWorkingDays",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "PermitCollectedOn",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "PermitSubmittedOn",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "PmcOutcome",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ProjectInterest",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "Reference",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "SiteAddress",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ReceiptNumber",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "WorkItemId",
                table: "Documents");

            migrationBuilder.UpdateData(
                table: "Stages",
                keyColumn: "StageId",
                keyValue: 1,
                column: "Name",
                value: "Intake");

            migrationBuilder.UpdateData(
                table: "Stages",
                keyColumn: "StageId",
                keyValue: 2,
                column: "Name",
                value: "Design");

            migrationBuilder.UpdateData(
                table: "Stages",
                keyColumn: "StageId",
                keyValue: 3,
                column: "Name",
                value: "Client Review");

            migrationBuilder.UpdateData(
                table: "Stages",
                keyColumn: "StageId",
                keyValue: 4,
                column: "Name",
                value: "Permitting");

            migrationBuilder.UpdateData(
                table: "Stages",
                keyColumn: "StageId",
                keyValue: 5,
                column: "Name",
                value: "Construction");
        }
    }
}
