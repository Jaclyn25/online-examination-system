using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aperture.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AtomicResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Results",
                columns: table => new
                {
                    AttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    EarnedMarks = table.Column<int>(type: "integer", nullable: false),
                    TotalMarks = table.Column<int>(type: "integer", nullable: false),
                    Percentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Passed = table.Column<bool>(type: "boolean", nullable: false),
                    CorrectCount = table.Column<int>(type: "integer", nullable: false),
                    IncorrectCount = table.Column<int>(type: "integer", nullable: false),
                    UnansweredCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Results", x => x.AttemptId);
                    table.CheckConstraint("CK_Result_Marks", "\"TotalMarks\" > 0 AND \"EarnedMarks\" BETWEEN 0 AND \"TotalMarks\"");
                    table.CheckConstraint("CK_Result_Percentage", "\"Percentage\" BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_Results_Attempts_AttemptId",
                        column: x => x.AttemptId,
                        principalTable: "Attempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });
            migrationBuilder.Sql("CREATE TRIGGER immutable_results BEFORE UPDATE OR DELETE ON \"Results\" FOR EACH ROW EXECUTE FUNCTION reject_published_mutation();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Results");
        }
    }
}
