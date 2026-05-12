-- SensitiveFlow Token Store schema for SQL Server

IF OBJECT_ID(N'[SensitiveFlow_TokenMappings]', N'U') IS NULL
BEGIN
    CREATE TABLE [SensitiveFlow_TokenMappings] (
        [Id]    BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_SensitiveFlow_TokenMappings] PRIMARY KEY,
        [Value] NVARCHAR(512) NOT NULL,
        [Token] NVARCHAR(128) NOT NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SensitiveFlow_TokenMappings_Value')
    CREATE UNIQUE INDEX [IX_SensitiveFlow_TokenMappings_Value]
        ON [SensitiveFlow_TokenMappings] ([Value]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SensitiveFlow_TokenMappings_Token')
    CREATE INDEX [IX_SensitiveFlow_TokenMappings_Token]
        ON [SensitiveFlow_TokenMappings] ([Token]);
GO
