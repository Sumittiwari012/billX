using MySql.Data.MySqlClient;

namespace MyWPFCRUDApp.Services
{
    public static class DatabaseInitializer
    {
        public static void InitializeAllTables()
        {
            using var conn = new MySqlConnection(DatabaseHelper.ConnectionString);
            conn.Open();

            // All CREATE TABLE statements in one place
            // Order matters — parent tables before child tables
            string[] tables = {
                @"CREATE TABLE IF NOT EXISTS MCategory (
                    Id           BIGINT AUTO_INCREMENT PRIMARY KEY,
                    CategoryName VARCHAR(100) NOT NULL UNIQUE,
                    CreatedBy    VARCHAR(100) DEFAULT 'System',
                    CreatedDate  DATETIME     DEFAULT CURRENT_TIMESTAMP,
                    ModifiedBy   VARCHAR(100) DEFAULT 'System',
                    ModifiedDate DATETIME     DEFAULT CURRENT_TIMESTAMP
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;",

                @"CREATE TABLE IF NOT EXISTS MCompanyInfo (
                    -- BaseEntity Columns
                    Id BIGINT AUTO_INCREMENT PRIMARY KEY,
                    CreatedBy VARCHAR(100) DEFAULT 'System',
                    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                    ModifiedBy VARCHAR(100) DEFAULT 'System',
                    ModifiedDate DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,

                    -- Basic Company Details
                    CompanyName VARCHAR(200) NOT NULL,
                    OwnerName VARCHAR(200),

                    -- Contact Details
                    Phone VARCHAR(20),
                    Mobile VARCHAR(20),
                    Email VARCHAR(200),
                    Website VARCHAR(200),

                    -- Address
                    AddressLine1 VARCHAR(300),
                    AddressLine2 VARCHAR(300),
                    City VARCHAR(100),
                    State VARCHAR(100),
                    Pincode VARCHAR(20),

                    -- Registration Numbers
                    GSTNumber VARCHAR(50),
                    PANNumber VARCHAR(20),
                    CINNumber VARCHAR(50),
                    IECCode VARCHAR(50),

                    -- Branding
                    LogoPath TEXT,

                    -- Invoice Settings
                    InvoiceStartNumber INT NOT NULL DEFAULT 0,
                    ShowLogoOnInvoice TINYINT(1) NOT NULL DEFAULT 0,
                    InvoiceFooterNote VARCHAR(300),

                    -- Bank Details
                    BankName VARCHAR(200),
                    Branch VARCHAR(200),
                    AccountNumber VARCHAR(50),
                    IFSCCode VARCHAR(20)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;",

                @"CREATE TABLE IF NOT EXISTS MSubCategory (
                -- BaseEntity Columns
                Id BIGINT AUTO_INCREMENT PRIMARY KEY,
                CreatedBy VARCHAR(100) DEFAULT 'System',
                CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                ModifiedBy VARCHAR(100) DEFAULT 'System',
                ModifiedDate DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,

                -- SubCategory Specifics
                SubCategoryName VARCHAR(100) NOT NULL,
    
                -- Foreign Key Column (Matching BIGINT type of MCategory.Id)
                CategoryId BIGINT NOT NULL,

                -- Relationship constraint
                CONSTRAINT FK_SubCategory_Category FOREIGN KEY (CategoryId) 
                REFERENCES MCategory(Id) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;",
                @"CREATE TABLE IF NOT EXISTS MUnit (
                    Id           BIGINT AUTO_INCREMENT PRIMARY KEY,
                    UnitName VARCHAR(100) NOT NULL UNIQUE,
                    CreatedBy    VARCHAR(100) DEFAULT 'System',
                    CreatedDate  DATETIME     DEFAULT CURRENT_TIMESTAMP,
                    ModifiedBy   VARCHAR(100) DEFAULT 'System',
                    ModifiedDate DATETIME     DEFAULT CURRENT_TIMESTAMP
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;",
                @"CREATE TABLE IF NOT EXISTS MProducts (
    -- BaseEntity Columns
    Id BIGINT AUTO_INCREMENT PRIMARY KEY,
    CreatedBy VARCHAR(100) DEFAULT 'System',
    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
    ModifiedBy VARCHAR(100) DEFAULT 'System',
    ModifiedDate DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,

    -- Identity
    ProductCode VARCHAR(50) UNIQUE,
    ProductName VARCHAR(200) NOT NULL,
    Barcode VARCHAR(100) NOT NULL UNIQUE,

    -- Links (Using BIGINT to match your other tables)
    CategoryId BIGINT NOT NULL,
    SubCategoryId BIGINT NOT NULL,

    -- Item Details
    HSNCode VARCHAR(50),
    PartGroup VARCHAR(100),
    Description TEXT,

    -- Pricing (Decimal 18,2 is industry standard)
    PurchasePrice DECIMAL(18,2) DEFAULT 0.00,
    RetailSalePrice DECIMAL(18,2) DEFAULT 0.00,
    WholesalePrice DECIMAL(18,2) DEFAULT 0.00,
    DiscountPercentage DOUBLE DEFAULT 0.0,
    CGST DOUBLE DEFAULT 0.0,
    SGST DOUBLE DEFAULT 0.0,
    CESS DOUBLE DEFAULT 0.0,
    MRP DECIMAL(18,2) DEFAULT 0.00,

    -- Inventory/Attributes
    Godown VARCHAR(100),
    Rack VARCHAR(50),
    Batch VARCHAR(50),
    MfgDate DATETIME,
    ExpDate DATETIME,
    Size VARCHAR(50),
    Colour VARCHAR(50),
    IMEI1 VARCHAR(50),
    IMEI2 VARCHAR(50),
    UnitId BIGINT NOT NULL,

    -- Foreign Key Constraints
    CONSTRAINT FK_Product_Category FOREIGN KEY (CategoryId) REFERENCES MCategory(Id),
    CONSTRAINT FK_Product_SubCategory FOREIGN KEY (SubCategoryId) REFERENCES MSubCategory(Id)
    CONSTRAINT FK_Product_Unit FOREIGN KEY (UnitId) REFERENCES MUnit(Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;",

@"CREATE TABLE IF NOT EXISTS ProductQuantity (
    Id BIGINT AUTO_INCREMENT PRIMARY KEY,
    CreatedBy VARCHAR(100) DEFAULT 'System',
    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
    ModifiedBy VARCHAR(100) DEFAULT 'System',
    ModifiedDate DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,

    ProductCode VARCHAR(50) NOT NULL,
    Barcode VARCHAR(100) NOT NULL,
    MinimumSellingQuantity BIGINT DEFAULT 1,
    Quantity BIGINT DEFAULT 0,

    -- Link back to Products
    CONSTRAINT FK_Quantity_Product FOREIGN KEY (Barcode) REFERENCES MProducts(Barcode) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;"


                

                // Add all your other tables here...
            };

            foreach (var sql in tables)
                new MySqlCommand(sql, conn).ExecuteNonQuery();
        }
    }
}