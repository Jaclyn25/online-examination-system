using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Aperture.Api.Data.Migrations;

[DbContext(typeof(AppDb))]
[Migration("20261107183000_HistoryGuards")]
public class HistoryGuards : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE FUNCTION guard_attempt_history() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF OLD."Status" <> 'Active' OR NEW."StudentId" <> OLD."StudentId" OR NEW."ExamId" <> OLD."ExamId"
                OR NEW."ExamVersionId" <> OLD."ExamVersionId" OR NEW."StartedAt" <> OLD."StartedAt" OR NEW."Deadline" <> OLD."Deadline" THEN
                RAISE EXCEPTION 'Attempt identity and finalized history cannot change';
            END IF;
            RETURN NEW;
        END;
        $$;
        CREATE TRIGGER protect_attempt_history BEFORE UPDATE ON "Attempts" FOR EACH ROW EXECUTE FUNCTION guard_attempt_history();
        CREATE FUNCTION guard_student_answer() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE target "Attempts"%ROWTYPE;
        BEGIN
            SELECT * INTO target FROM "Attempts" WHERE "Id" = NEW."AttemptId" FOR UPDATE;
            IF target."Status" <> 'Active' OR NEW."SavedAt" > target."Deadline" OR NEW."SavedAt" < target."StartedAt"
                OR NOT EXISTS (SELECT 1 FROM "VersionQuestions" WHERE "Id" = NEW."VersionQuestionId" AND "ExamVersionId" = target."ExamVersionId")
                OR (NEW."VersionChoiceId" IS NOT NULL AND NOT EXISTS (SELECT 1 FROM "VersionChoices" WHERE "Id" = NEW."VersionChoiceId" AND "VersionQuestionId" = NEW."VersionQuestionId")) THEN
                RAISE EXCEPTION 'Answer violates attempt state, time or question membership';
            END IF;
            RETURN NEW;
        END;
        $$;
        CREATE TRIGGER protect_student_answers BEFORE INSERT OR UPDATE ON "StudentAnswers" FOR EACH ROW EXECUTE FUNCTION guard_student_answer();
        """);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("DROP FUNCTION guard_student_answer() CASCADE; DROP FUNCTION guard_attempt_history() CASCADE;");
}
