using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aperture.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PublishedSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExamVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    PassingPercentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    ShowFeedback = table.Column<bool>(type: "boolean", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamVersions", x => x.Id);
                    table.CheckConstraint("CK_Version_Duration", "\"DurationMinutes\" BETWEEN 1 AND 240");
                    table.CheckConstraint("CK_Version_Pass", "\"PassingPercentage\" BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_ExamVersions_Exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VersionQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: false),
                    Marks = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VersionQuestions", x => x.Id);
                    table.CheckConstraint("CK_VersionQuestion_Marks", "\"Marks\" BETWEEN 1 AND 1000");
                    table.ForeignKey(
                        name: "FK_VersionQuestions_ExamVersions_ExamVersionId",
                        column: x => x.ExamVersionId,
                        principalTable: "ExamVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VersionChoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionQuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VersionChoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VersionChoices_VersionQuestions_VersionQuestionId",
                        column: x => x.VersionQuestionId,
                        principalTable: "VersionQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExamVersions_ExamId_Number",
                table: "ExamVersions",
                columns: new[] { "ExamId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VersionChoices_VersionQuestionId",
                table: "VersionChoices",
                column: "VersionQuestionId",
                unique: true,
                filter: "\"IsCorrect\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_VersionChoices_VersionQuestionId_Position",
                table: "VersionChoices",
                columns: new[] { "VersionQuestionId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VersionQuestions_ExamVersionId_Position",
                table: "VersionQuestions",
                columns: new[] { "ExamVersionId", "Position" },
                unique: true);
            migrationBuilder.Sql("""
                CREATE FUNCTION reject_published_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'Published examination content is immutable'; END;
                $$;
                CREATE TRIGGER immutable_versions BEFORE UPDATE OR DELETE ON "ExamVersions" FOR EACH ROW EXECUTE FUNCTION reject_published_mutation();
                CREATE TRIGGER immutable_questions BEFORE UPDATE OR DELETE ON "VersionQuestions" FOR EACH ROW EXECUTE FUNCTION reject_published_mutation();
                CREATE TRIGGER immutable_choices BEFORE UPDATE OR DELETE ON "VersionChoices" FOR EACH ROW EXECUTE FUNCTION reject_published_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION reject_published_mutation() CASCADE;");
            migrationBuilder.DropTable(
                name: "VersionChoices");

            migrationBuilder.DropTable(
                name: "VersionQuestions");

            migrationBuilder.DropTable(
                name: "ExamVersions");
        }
    }
}
