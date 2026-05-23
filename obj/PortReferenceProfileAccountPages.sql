START TRANSACTION;
ALTER TABLE "Announcements" DROP CONSTRAINT "FK_Announcements_AspNetUsers_InstructorId";

ALTER TABLE "Announcements" DROP CONSTRAINT "FK_Announcements_Courses_CourseId";

ALTER TABLE "AspNetUsers" ADD "Bio" text;

ALTER TABLE "AspNetUsers" ADD "City" text;

ALTER TABLE "AspNetUsers" ADD "Country" text;

ALTER TABLE "AspNetUsers" ADD "Department" text;

ALTER TABLE "AspNetUsers" ADD "Designation" text;

ALTER TABLE "AspNetUsers" ADD "FirstName" text NOT NULL DEFAULT '';

ALTER TABLE "AspNetUsers" ADD "LastName" text NOT NULL DEFAULT '';

ALTER TABLE "AspNetUsers" ADD "University" text;

UPDATE "AspNetUsers"
SET "FirstName" = split_part(trim("FullName"), ' ', 1),
    "LastName" = CASE
        WHEN position(' ' in trim("FullName")) > 0
        THEN substr(trim("FullName"), position(' ' in trim("FullName")) + 1)
        ELSE ''
    END
WHERE "FullName" IS NOT NULL AND trim("FullName") <> '';

ALTER TABLE "Announcements" ALTER COLUMN "InstructorId" DROP NOT NULL;

ALTER TABLE "Announcements" ALTER COLUMN "CourseId" DROP NOT NULL;

ALTER TABLE "Announcements" ADD CONSTRAINT "FK_Announcements_AspNetUsers_InstructorId" FOREIGN KEY ("InstructorId") REFERENCES "AspNetUsers" ("Id") ON DELETE SET NULL;

ALTER TABLE "Announcements" ADD CONSTRAINT "FK_Announcements_Courses_CourseId" FOREIGN KEY ("CourseId") REFERENCES "Courses" ("Id") ON DELETE SET NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260523142211_PortReferenceProfileAccountPages', '9.0.4');

COMMIT;

