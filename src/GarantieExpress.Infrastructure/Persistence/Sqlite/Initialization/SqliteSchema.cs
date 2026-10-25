namespace GarantieExpress.Infrastructure.Persistence.Sqlite.Initialization;

internal static class SqliteSchema
{
    internal const string Create = """
        CREATE TABLE ApplicationMetadata (
            Id INTEGER PRIMARY KEY CHECK (Id = 1),
            ApplicationId TEXT NOT NULL CHECK (ApplicationId = 'GarantieExpress')
        );
        INSERT INTO ApplicationMetadata (Id, ApplicationId)
        VALUES (1, 'GarantieExpress');

        CREATE TABLE WarrantySnapshots (
            Id TEXT PRIMARY KEY CHECK (length(Id) = 64),
            PayloadJson TEXT NOT NULL CHECK (json_valid(PayloadJson))
        );

        CREATE TABLE Warranties (
            Number TEXT PRIMARY KEY,
            Year INTEGER NOT NULL CHECK (Year BETWEEN 2000 AND 9999),
            Sequence INTEGER NOT NULL CHECK (Sequence > 0),
            CategoryKey TEXT NOT NULL CHECK (length(CategoryKey) BETWEEN 1 AND 80),
            BrandModel TEXT NOT NULL CHECK (length(trim(BrandModel)) BETWEEN 1 AND 180),
            Serial TEXT NOT NULL CHECK (length(trim(Serial)) BETWEEN 1 AND 120),
            PurchaseDate TEXT NOT NULL CHECK (date(PurchaseDate) = PurchaseDate),
            PriceCents INTEGER NOT NULL CHECK (PriceCents BETWEEN 1 AND 9999999900),
            DurationMonths INTEGER NOT NULL CHECK (DurationMonths BETWEEN 1 AND 120),
            IssuedAt TEXT NOT NULL
                CHECK (julianday(IssuedAt) IS NOT NULL)
                CHECK (CAST(substr(IssuedAt, 1, 4) AS INTEGER) = Year),
            Status INTEGER NOT NULL CHECK (Status IN (0, 1)),
            LastTemplateKey TEXT NOT NULL CHECK (length(LastTemplateKey) BETWEEN 1 AND 80),
            SnapshotId TEXT NOT NULL REFERENCES WarrantySnapshots(Id),
            ValidFrom TEXT NULL CHECK (ValidFrom IS NULL OR date(ValidFrom) = ValidFrom),
            ValidUntil TEXT NULL CHECK (ValidUntil IS NULL OR date(ValidUntil) = ValidUntil),
            ServiceType INTEGER NULL CHECK (ServiceType IS NULL OR ServiceType IN (0, 1)),
            RepairNote TEXT NULL,
            Version INTEGER NOT NULL DEFAULT 1 CHECK (Version >= 1),
            UNIQUE (Year, Sequence),
            CHECK ((ValidFrom IS NULL) = (ValidUntil IS NULL))
        );
        CREATE INDEX IX_Warranties_IssuedAt
        ON Warranties(julianday(IssuedAt) DESC, Number DESC);
        CREATE INDEX IX_Warranties_Search
        ON Warranties(CategoryKey, BrandModel, Serial);

        CREATE TABLE IssuanceRequests (
            RequestId TEXT PRIMARY KEY CHECK (length(RequestId) = 32),
            InputHash TEXT NOT NULL CHECK (length(InputHash) = 64),
            WarrantyNumber TEXT NOT NULL
                REFERENCES Warranties(Number) ON DELETE CASCADE
        );

        CREATE TABLE Categories (
            Key TEXT PRIMARY KEY CHECK (length(Key) BETWEEN 1 AND 80),
            PayloadJson TEXT NULL CHECK (PayloadJson IS NULL OR json_valid(PayloadJson)),
            IsArchived INTEGER NOT NULL CHECK (IsArchived IN (0, 1)),
            ShowInBar INTEGER NOT NULL CHECK (ShowInBar IN (0, 1)),
            SortOrder INTEGER NOT NULL CHECK (SortOrder >= 0),
            Version INTEGER NOT NULL DEFAULT 1 CHECK (Version >= 1),
            CHECK (IsArchived = 0 OR ShowInBar = 0)
        );
        CREATE INDEX IX_Categories_Order
        ON Categories(IsArchived, SortOrder, Key);

        CREATE TABLE StoreSettings (
            Id INTEGER PRIMARY KEY CHECK (Id = 1),
            PayloadJson TEXT NOT NULL CHECK (json_valid(PayloadJson)),
            Version INTEGER NOT NULL DEFAULT 1 CHECK (Version >= 1)
        );

        CREATE TABLE Drafts (
            Id INTEGER PRIMARY KEY CHECK (Id = 1),
            PayloadJson TEXT NOT NULL CHECK (json_valid(PayloadJson)),
            Version INTEGER NOT NULL DEFAULT 1 CHECK (Version >= 1),
            UpdatedAtUtc TEXT NOT NULL CHECK (julianday(UpdatedAtUtc) IS NOT NULL)
        );

        CREATE TABLE NumberCounters (
            Year INTEGER PRIMARY KEY CHECK (Year BETWEEN 2000 AND 9999),
            HighestSequence INTEGER NOT NULL CHECK (HighestSequence >= 0)
        );

        CREATE TABLE Products (
            Id TEXT PRIMARY KEY CHECK (length(Id) = 32),
            BrandModel TEXT NOT NULL CHECK (length(trim(BrandModel)) BETWEEN 1 AND 180),
            BrandModelKey TEXT NOT NULL UNIQUE
                CHECK (BrandModelKey = garantie_express_product_key(BrandModel)),
            CategoryKey TEXT NOT NULL REFERENCES Categories(Key),
            PriceCents INTEGER NOT NULL CHECK (PriceCents BETWEEN 1 AND 9999999900),
            DurationMonths INTEGER NOT NULL CHECK (DurationMonths BETWEEN 1 AND 120)
        );
        CREATE INDEX IX_Products_Category
        ON Products(CategoryKey, BrandModel COLLATE NOCASE);

        CREATE TRIGGER TR_WarrantySnapshots_Immutable
        BEFORE UPDATE ON WarrantySnapshots
        BEGIN
            SELECT RAISE(ABORT, 'warranty snapshots are immutable');
        END;

        CREATE TRIGGER TR_IssuanceRequests_Immutable
        BEFORE UPDATE ON IssuanceRequests
        BEGIN
            SELECT RAISE(ABORT, 'issuance requests are immutable');
        END;
        """;

    internal static readonly IReadOnlySet<string> TableNames = new HashSet<string>(
        [
            "ApplicationMetadata",
            "Categories",
            "Drafts",
            "IssuanceRequests",
            "NumberCounters",
            "Products",
            "StoreSettings",
            "Warranties",
            "WarrantySnapshots"
        ],
        StringComparer.Ordinal);

    internal static readonly IReadOnlySet<string> IndexNames = new HashSet<string>(
        [
            "IX_Categories_Order",
            "IX_Products_Category",
            "IX_Warranties_IssuedAt",
            "IX_Warranties_Search"
        ],
        StringComparer.Ordinal);

    internal static readonly IReadOnlySet<string> TriggerNames = new HashSet<string>(
        [
            "TR_IssuanceRequests_Immutable",
            "TR_WarrantySnapshots_Immutable"
        ],
        StringComparer.Ordinal);
}
