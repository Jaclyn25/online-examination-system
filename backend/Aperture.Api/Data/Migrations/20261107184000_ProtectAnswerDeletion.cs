using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Aperture.Api.Data.Migrations;
[DbContext(typeof(AppDb))]
[Migration("20261107184000_ProtectAnswerDeletion")]
public class ProtectAnswerDeletion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE FUNCTION guard_answer_deletion() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            PERFORM 1 FROM "Attempts" WHERE "Id" = OLD."AttemptId" AND "Status" = 'Active' FOR UPDATE;
            IF NOT FOUND THEN RAISE EXCEPTION 'Finalized answers cannot be deleted'; END IF;
            RETURN OLD;
        END;
        $$;
        CREATE TRIGGER protect_answer_deletion BEFORE DELETE ON "StudentAnswers" FOR EACH ROW EXECUTE FUNCTION guard_answer_deletion();
        """);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("DROP FUNCTION guard_answer_deletion() CASCADE;");
}
