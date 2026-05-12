-- SensitiveFlow Audit schema for SQLite
-- Idempotent: safe to run multiple times.

CREATE TABLE IF NOT EXISTS "SensitiveFlow_AuditRecords" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_SensitiveFlow_AuditRecords" PRIMARY KEY AUTOINCREMENT,
    "RecordId" TEXT NOT NULL,
    "DataSubjectId" TEXT NOT NULL,
    "Entity" TEXT NOT NULL,
    "Field" TEXT NOT NULL,
    "Operation" INTEGER NOT NULL,
    "Timestamp" TEXT NOT NULL,
    "ActorId" TEXT NULL,
    "IpAddressToken" TEXT NULL,
    "Details" TEXT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_SensitiveFlow_AuditRecords_RecordId"
    ON "SensitiveFlow_AuditRecords" ("RecordId");

CREATE INDEX IF NOT EXISTS "IX_SensitiveFlow_AuditRecords_Timestamp"
    ON "SensitiveFlow_AuditRecords" ("Timestamp");

CREATE INDEX IF NOT EXISTS "IX_SensitiveFlow_AuditRecords_DataSubjectId_Timestamp"
    ON "SensitiveFlow_AuditRecords" ("DataSubjectId", "Timestamp");
