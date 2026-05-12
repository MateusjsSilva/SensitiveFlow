-- SensitiveFlow Audit schema for SQL Server
-- Idempotent: safe to run multiple times.

IF OBJECT_ID(N'[SensitiveFlow_AuditRecords]', N'U') IS NULL
BEGIN
    CREATE TABLE [SensitiveFlow_AuditRecords] (
        [Id]             BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_SensitiveFlow_AuditRecords] PRIMARY KEY,
        [RecordId]       NVARCHAR(64)  NOT NULL,
        [DataSubjectId]  NVARCHAR(256) NOT NULL,
        [Entity]         NVARCHAR(256) NOT NULL,
        [Field]          NVARCHAR(256) NOT NULL,
        [Operation]      INT           NOT NULL,
        [Timestamp]      DATETIME2(7)  NOT NULL,
        [ActorId]        NVARCHAR(256) NULL,
        [IpAddressToken] NVARCHAR(128) NULL,
        [Details]        NVARCHAR(2048) NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SensitiveFlow_AuditRecords_RecordId')
    CREATE UNIQUE INDEX [IX_SensitiveFlow_AuditRecords_RecordId]
        ON [SensitiveFlow_AuditRecords] ([RecordId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SensitiveFlow_AuditRecords_Timestamp')
    CREATE INDEX [IX_SensitiveFlow_AuditRecords_Timestamp]
        ON [SensitiveFlow_AuditRecords] ([Timestamp]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SensitiveFlow_AuditRecords_DataSubjectId_Timestamp')
    CREATE INDEX [IX_SensitiveFlow_AuditRecords_DataSubjectId_Timestamp]
        ON [SensitiveFlow_AuditRecords] ([DataSubjectId], [Timestamp]);
GO
