-- SensitiveFlow Audit Snapshots schema for PostgreSQL

CREATE TABLE IF NOT EXISTS "SensitiveFlow_AuditSnapshots" (
    "Id"             BIGSERIAL PRIMARY KEY,
    "SnapshotId"     VARCHAR(64)  NOT NULL,
    "DataSubjectId"  VARCHAR(256) NOT NULL,
    "Aggregate"      VARCHAR(256) NOT NULL,
    "AggregateId"   VARCHAR(256) NOT NULL,
    "Timestamp"      TIMESTAMP    NOT NULL,
    "ActorId"        VARCHAR(256),
    "IpAddressToken" VARCHAR(128),
    "BeforeJson"     TEXT,
    "AfterJson"      TEXT
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_SensitiveFlow_AuditSnapshots_SnapshotId"
    ON "SensitiveFlow_AuditSnapshots" ("SnapshotId");

CREATE INDEX IF NOT EXISTS "IX_SensitiveFlow_AuditSnapshots_Aggregate_Timestamp"
    ON "SensitiveFlow_AuditSnapshots" ("Aggregate", "AggregateId", "Timestamp");

CREATE INDEX IF NOT EXISTS "IX_SensitiveFlow_AuditSnapshots_DataSubjectId_Timestamp"
    ON "SensitiveFlow_AuditSnapshots" ("DataSubjectId", "Timestamp");
