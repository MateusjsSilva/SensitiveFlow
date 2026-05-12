-- SensitiveFlow Token Store schema for PostgreSQL

CREATE TABLE IF NOT EXISTS "SensitiveFlow_TokenMappings" (
    "Id"    BIGSERIAL PRIMARY KEY,
    "Value" VARCHAR(512) NOT NULL,
    "Token" VARCHAR(128) NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_SensitiveFlow_TokenMappings_Value"
    ON "SensitiveFlow_TokenMappings" ("Value");

CREATE INDEX IF NOT EXISTS "IX_SensitiveFlow_TokenMappings_Token"
    ON "SensitiveFlow_TokenMappings" ("Token");
