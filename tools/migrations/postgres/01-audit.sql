-- SensitiveFlow Audit schema for PostgreSQL
-- Idempotent: safe to run multiple times.

CREATE TABLE IF NOT EXISTS "SensitiveFlow_AuditRecords" (
    "Id"             BIGSERIAL PRIMARY KEY,
    "RecordId"       VARCHAR(64)  NOT NULL,
    "DataSubjectId"  VARCHAR(256) NOT NULL,
    "Entity"         VARCHAR(256) NOT NULL,
    "Field"          VARCHAR(256) NOT NULL,
    "Operation"      INTEGER      NOT NULL,
    "Timestamp"      TIMESTAMP    NOT NULL,
    "ActorId"        VARCHAR(256),
    "IpAddressToken" VARCHAR(128),
    "Details"        VARCHAR(2048)
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_SensitiveFlow_AuditRecords_RecordId"
    ON "SensitiveFlow_AuditRecords" ("RecordId");

CREATE INDEX IF NOT EXISTS "IX_SensitiveFlow_AuditRecords_Timestamp"
    ON "SensitiveFlow_AuditRecords" ("Timestamp");

CREATE INDEX IF NOT EXISTS "IX_SensitiveFlow_AuditRecords_DataSubjectId_Timestamp"
    ON "SensitiveFlow_AuditRecords" ("DataSubjectId", "Timestamp");
