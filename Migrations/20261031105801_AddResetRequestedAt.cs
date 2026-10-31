using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArchtApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddResetRequestedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ResetRequestedAt",
                table: "Users",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResetRequestedAt",
                table: "Users");
        }
    }
}
