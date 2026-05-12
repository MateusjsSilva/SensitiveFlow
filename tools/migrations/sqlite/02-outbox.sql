-- SensitiveFlow Outbox schema for SQLite
-- Note: no schema prefix — SQLite does not support schemas.

CREATE TABLE IF NOT EXISTS "AuditOutboxEntries" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_AuditOutboxEntries" PRIMARY KEY,
    "AuditRecordId" TEXT NOT NULL,
    "Payload" TEXT NOT NULL,
    "Attempts" INTEGER NOT NULL DEFAULT 0,
    "EnqueuedAt" TEXT NOT NULL,
    "EnqueuedAtTicks" INTEGER NOT NULL,
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

CREATE INDEX IF NOT EXISTS "IX_AuditOutboxEntries_EnqueuedAtTicks"
    ON "AuditOutboxEntries" ("EnqueuedAtTicks");
