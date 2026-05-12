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

CREATE TABLE IF NOT EXISTS "AuditOutboxEntries" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_AuditOutboxEntries" PRIMARY KEY,
    "AuditRecordId" TEXT NOT NULL,
    "Payload" TEXT NOT NULL,
    "Attempts" INTEGER NOT NULL DEFAULT 0,
    "EnqueuedAt" TEXT NOT NULL,
    "LastAttemptAt" TEXT NULL,
    "LastError" TEXT NULL,
    "IsProcessed" INTEGER NOT NULL DEFAULT 0,
    "ProcessedAt" TEXT NULL,
    "IsDeadLettered" INTEGER NOT NULL DEFAULT 0,
    "DeadLetterReason" TEXT NULL
);

CREATE INDEX IF NOT EXISTS "IX_AuditOutboxEntries_IsProcessed_IsDeadLettered"
    ON "AuditOutboxEntries" ("IsProcessed", "IsDeadLettered");

CREATE INDEX IF NOT EXISTS "IX_AuditOutboxEntries_IsDeadLettered"
    ON "AuditOutboxEntries" ("IsDeadLettered");

CREATE INDEX IF NOT EXISTS "IX_AuditOutboxEntries_EnqueuedAt"
    ON "AuditOutboxEntries" ("EnqueuedAt");
