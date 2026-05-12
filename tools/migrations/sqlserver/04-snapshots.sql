-- SensitiveFlow Audit Snapshots schema for SQL Server

IF OBJECT_ID(N'[SensitiveFlow_AuditSnapshots]', N'U') IS NULL
BEGIN
    CREATE TABLE [SensitiveFlow_AuditSnapshots] (
        [Id]             BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_SensitiveFlow_AuditSnapshots] PRIMARY KEY,
        [SnapshotId]     NVARCHAR(64)  NOT NULL,
        [DataSubjectId]  NVARCHAR(256) NOT NULL,
        [Aggregate]      NVARCHAR(256) NOT NULL,
        [AggregateId]    NVARCHAR(256) NOT NULL,
        [Timestamp]      DATETIME2(7)  NOT NULL,
        [ActorId]        NVARCHAR(256) NULL,
        [IpAddressToken] NVARCHAR(128) NULL,
        [BeforeJson]     NVARCHAR(MAX) NULL,
        [AfterJson]      NVARCHAR(MAX) NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SensitiveFlow_AuditSnapshots_SnapshotId')
    CREATE UNIQUE INDEX [IX_SensitiveFlow_AuditSnapshots_SnapshotId]
        ON [SensitiveFlow_AuditSnapshots] ([SnapshotId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SensitiveFlow_AuditSnapshots_Aggregate_Timestamp')
    CREATE INDEX [IX_SensitiveFlow_AuditSnapshots_Aggregate_Timestamp]
        ON [SensitiveFlow_AuditSnapshots] ([Aggregate], [AggregateId], [Timestamp]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SensitiveFlow_AuditSnapshots_DataSubjectId_Timestamp')
    CREATE INDEX [IX_SensitiveFlow_AuditSnapshots_DataSubjectId_Timestamp]
        ON [SensitiveFlow_AuditSnapshots] ([DataSubjectId], [Timestamp]);
GO
