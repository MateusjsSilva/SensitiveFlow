CREATE TABLE IF NOT EXISTS "SensitiveFlow_TokenMappings" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_SensitiveFlow_TokenMappings" PRIMARY KEY AUTOINCREMENT,
    "Value" TEXT NOT NULL,
    "Token" TEXT NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_SensitiveFlow_TokenMappings_Value"
    ON "SensitiveFlow_TokenMappings" ("Value");

CREATE INDEX IF NOT EXISTS "IX_SensitiveFlow_TokenMappings_Token"
    ON "SensitiveFlow_TokenMappings" ("Token");
