CREATE TABLE IF NOT EXISTS "Customers" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Customers" PRIMARY KEY AUTOINCREMENT,
    "DataSubjectId" TEXT NOT NULL,
    "Name" TEXT NOT NULL,
    "Email" TEXT NOT NULL,
    "TaxId" TEXT NOT NULL,
    "Phone" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS "IX_Customers_DataSubjectId"
    ON "Customers" ("DataSubjectId");
