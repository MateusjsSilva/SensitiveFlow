-- SensitiveFlow Audit Snapshots schema for SQLite

CREATE TABLE IF NOT EXISTS "SensitiveFlow_AuditSnapshots" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_SensitiveFlow_AuditSnapshots" PRIMARY KEY AUTOINCREMENT,
    "SnapshotId" TEXT NOT NULL,
    "DataSubjectId" TEXT NOT NULL,
    "Aggregate" TEXT NOT NULL,
    "AggregateId" TEXT NOT NULL,
    "Timestamp" TEXT NOT NULL,
    "ActorId" TEXT NULL,
    "IpAddressToken" TEXT NULL,
    "BeforeJson" TEXT NULL,
    "AfterJson" TEXT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_SensitiveFlow_AuditSnapshots_SnapshotId"
    ON "SensitiveFlow_AuditSnapshots" ("SnapshotId");

CREATE INDEX IF NOT EXISTS "IX_SensitiveFlow_AuditSnapshots_Aggregate_Timestamp"
    ON "SensitiveFlow_AuditSnapshots" ("Aggregate", "AggregateId", "Timestamp");

CREATE INDEX IF NOT EXISTS "IX_SensitiveFlow_AuditSnapshots_DataSubjectId_Timestamp"
    ON "SensitiveFlow_AuditSnapshots" ("DataSubjectId", "Timestamp");
