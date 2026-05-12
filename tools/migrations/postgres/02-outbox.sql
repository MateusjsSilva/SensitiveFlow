-- SensitiveFlow Outbox schema for PostgreSQL
-- Default deployment uses the "public" schema. If you need a dedicated schema,
-- create it (CREATE SCHEMA sensitiveflow;) and pass the schema name to
-- AuditOutboxEntryConfiguration("AuditOutboxEntries", "sensitiveflow").

CREATE TABLE IF NOT EXISTS "AuditOutboxEntries" (
    "Id"               UUID         NOT NULL PRIMARY KEY,
    "AuditRecordId"    VARCHAR(256) NOT NULL,
    "Payload"          TEXT         NOT NULL,
    "Attempts"         INTEGER      NOT NULL DEFAULT 0,
    "EnqueuedAt"       TIMESTAMPTZ  NOT NULL,
    "EnqueuedAtTicks"  BIGINT       NOT NULL,
    "LastAttemptAt"    TIMESTAMPTZ,
    "LastError"        VARCHAR(512),
    "IsProcessed"      BOOLEAN      NOT NULL DEFAULT FALSE,
    "ProcessedAt"      TIMESTAMPTZ,
    "IsDeadLettered"   BOOLEAN      NOT NULL DEFAULT FALSE,
    "DeadLetterReason" VARCHAR(512)
);

CREATE INDEX IF NOT EXISTS "IX_AuditOutboxEntries_IsProcessed_IsDeadLettered"
    ON "AuditOutboxEntries" ("IsProcessed", "IsDeadLettered");

CREATE INDEX IF NOT EXISTS "IX_AuditOutboxEntries_IsDeadLettered"
    ON "AuditOutboxEntries" ("IsDeadLettered");

CREATE INDEX IF NOT EXISTS "IX_AuditOutboxEntries_EnqueuedAtTicks"
    ON "AuditOutboxEntries" ("EnqueuedAtTicks");
