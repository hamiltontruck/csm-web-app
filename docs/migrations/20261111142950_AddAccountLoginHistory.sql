START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111142950_AddAccountLoginHistory') THEN
    CREATE TABLE "LoginHistoryEntries" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "OccurredAtUtc" timestamp with time zone NOT NULL,
        "Succeeded" boolean NOT NULL,
        "IpAddress" character varying(64),
        "DeviceName" character varying(256),
        "FailureReason" character varying(200),
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAtUtc" timestamp with time zone,
        "UpdatedBy" uuid,
        "IsDeleted" boolean NOT NULL,
        "DeletedAtUtc" timestamp with time zone,
        "DeletedBy" uuid,
        CONSTRAINT "PK_LoginHistoryEntries" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_LoginHistoryEntries_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111142950_AddAccountLoginHistory') THEN
    CREATE INDEX "IX_LoginHistoryEntries_UserId_OccurredAtUtc" ON "LoginHistoryEntries" ("UserId", "OccurredAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111142950_AddAccountLoginHistory') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261111142950_AddAccountLoginHistory', '10.0.10');
    END IF;
END $EF$;
COMMIT;

