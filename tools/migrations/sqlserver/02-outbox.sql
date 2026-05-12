-- SensitiveFlow Outbox schema for SQL Server
-- The default deployment uses the default schema (dbo). If you need a dedicated
-- schema, create it via CREATE SCHEMA and update your DbContext model builder
-- to pass it through AuditOutboxEntryConfiguration("AuditOutboxEntries", "your_schema").

IF OBJECT_ID(N'[AuditOutboxEntries]', N'U') IS NULL
BEGIN
    CREATE TABLE [AuditOutboxEntries] (
        [Id]                UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_AuditOutboxEntries] PRIMARY KEY,
        [AuditRecordId]     NVARCHAR(256)    NOT NULL,
        [Payload]           NVARCHAR(MAX)    NOT NULL,
        [Attempts]          INT              NOT NULL CONSTRAINT [DF_AuditOutboxEntries_Attempts] DEFAULT 0,
        [EnqueuedAt]        DATETIMEOFFSET(7) NOT NULL,
        [EnqueuedAtTicks]   BIGINT           NOT NULL,
        [LastAttemptAt]     DATETIMEOFFSET(7) NULL,
        [LastError]         NVARCHAR(512)    NULL,
        [IsProcessed]       BIT              NOT NULL CONSTRAINT [DF_AuditOutboxEntries_IsProcessed] DEFAULT 0,
        [ProcessedAt]       DATETIMEOFFSET(7) NULL,
        [IsDeadLettered]    BIT              NOT NULL CONSTRAINT [DF_AuditOutboxEntries_IsDeadLettered] DEFAULT 0,
        [DeadLetterReason]  NVARCHAR(512)    NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditOutboxEntries_IsProcessed_IsDeadLettered')
    CREATE INDEX [IX_AuditOutboxEntries_IsProcessed_IsDeadLettered]
        ON [AuditOutboxEntries] ([IsProcessed], [IsDeadLettered]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditOutboxEntries_IsDeadLettered')
    CREATE INDEX [IX_AuditOutboxEntries_IsDeadLettered]
        ON [AuditOutboxEntries] ([IsDeadLettered]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditOutboxEntries_EnqueuedAtTicks')
    CREATE INDEX [IX_AuditOutboxEntries_EnqueuedAtTicks]
        ON [AuditOutboxEntries] ([EnqueuedAtTicks]);
GO
