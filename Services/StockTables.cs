namespace MyWPFCRUDApp.Services
{
    /// <summary>
    /// New tables for the Godown -> Store stock flow.
    /// Hook into DatabaseInitializer: change
    ///     foreach (var sql in tables)
    /// to
    ///     foreach (var sql in tables.Concat(StockTables.Statements))   // using System.Linq;
    /// Requires MySQL 8.0.16+ for CHECK constraints to be enforced.
    /// </summary>
    public static class StockTables
    {
        public static readonly string[] Statements =
        {
@"CREATE TABLE IF NOT EXISTS GodownStock (
    Id           BIGINT AUTO_INCREMENT PRIMARY KEY,
    GodownId     BIGINT NOT NULL,
    ProductId    BIGINT NOT NULL,
    Barcode      VARCHAR(100) NOT NULL,
    Quantity     DECIMAL(18,3) NOT NULL DEFAULT 0,
    CreatedBy    VARCHAR(100) DEFAULT 'System',
    CreatedDate  DATETIME DEFAULT CURRENT_TIMESTAMP,
    ModifiedBy   VARCHAR(100) DEFAULT 'System',
    ModifiedDate DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY UQ_GodownStock_GodownBarcode (GodownId, Barcode),
    KEY IX_GodownStock_Product (ProductId),
    CONSTRAINT CK_GodownStock_Qty CHECK (Quantity >= 0),
    CONSTRAINT FK_GodownStock_Godown  FOREIGN KEY (GodownId)  REFERENCES MGodown(Id),
    CONSTRAINT FK_GodownStock_Product FOREIGN KEY (ProductId) REFERENCES MProducts(Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;",

@"CREATE TABLE IF NOT EXISTS StoreStock (
    Id           BIGINT AUTO_INCREMENT PRIMARY KEY,
    StoreId      BIGINT NOT NULL,
    ProductId    BIGINT NOT NULL,
    Barcode      VARCHAR(100) NOT NULL,
    Quantity     DECIMAL(18,3) NOT NULL DEFAULT 0,
    CreatedBy    VARCHAR(100) DEFAULT 'System',
    CreatedDate  DATETIME DEFAULT CURRENT_TIMESTAMP,
    ModifiedBy   VARCHAR(100) DEFAULT 'System',
    ModifiedDate DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY UQ_StoreStock_StoreBarcode (StoreId, Barcode),
    KEY IX_StoreStock_Product (ProductId),
    CONSTRAINT CK_StoreStock_Qty CHECK (Quantity >= 0),
    CONSTRAINT FK_StoreStock_Store   FOREIGN KEY (StoreId)   REFERENCES MStore(Id),
    CONSTRAINT FK_StoreStock_Product FOREIGN KEY (ProductId) REFERENCES MProducts(Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;",

@"CREATE TABLE IF NOT EXISTS StockTransfer (
    Id           BIGINT AUTO_INCREMENT PRIMARY KEY,
    TransferNo   VARCHAR(50) NOT NULL UNIQUE,
    FromType     VARCHAR(10) NOT NULL,
    FromId       BIGINT NOT NULL,
    ToType       VARCHAR(10) NOT NULL,
    ToId         BIGINT NOT NULL,
    Status       VARCHAR(20) NOT NULL DEFAULT 'Draft',
    Remarks      TEXT NULL,
    DispatchedBy VARCHAR(100) NULL,
    DispatchedAt DATETIME NULL,
    ReceivedBy   VARCHAR(100) NULL,
    ReceivedAt   DATETIME NULL,
    CreatedBy    VARCHAR(100) DEFAULT 'System',
    CreatedDate  DATETIME DEFAULT CURRENT_TIMESTAMP,
    ModifiedBy   VARCHAR(100) DEFAULT 'System',
    ModifiedDate DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    KEY IX_StockTransfer_Status (Status),
    KEY IX_StockTransfer_To (ToType, ToId, Status),
    KEY IX_StockTransfer_From (FromType, FromId, Status),
    CONSTRAINT CK_StockTransfer_Type   CHECK (FromType IN ('Godown','Store') AND ToType IN ('Godown','Store')),
    CONSTRAINT CK_StockTransfer_Differ CHECK (NOT (FromType = ToType AND FromId = ToId))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;",

@"CREATE TABLE IF NOT EXISTS StockTransferItem (
    Id            BIGINT AUTO_INCREMENT PRIMARY KEY,
    TransferId    BIGINT NOT NULL,
    ProductId     BIGINT NOT NULL,
    Barcode       VARCHAR(100) NOT NULL,
    DispatchedQty DECIMAL(18,3) NOT NULL,
    ReceivedQty   DECIMAL(18,3) NULL,
    Remarks       VARCHAR(300) NULL,
    UNIQUE KEY UQ_TransferItem_Barcode (TransferId, Barcode),
    KEY IX_TransferItem_Barcode (Barcode),
    CONSTRAINT CK_TransferItem_Disp CHECK (DispatchedQty > 0),
    CONSTRAINT CK_TransferItem_Recv CHECK (ReceivedQty IS NULL OR (ReceivedQty >= 0 AND ReceivedQty <= DispatchedQty)),
    CONSTRAINT FK_TransferItem_Transfer FOREIGN KEY (TransferId) REFERENCES StockTransfer(Id) ON DELETE CASCADE,
    CONSTRAINT FK_TransferItem_Product  FOREIGN KEY (ProductId)  REFERENCES MProducts(Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;",

@"CREATE TABLE IF NOT EXISTS StockLedger (
    Id            BIGINT AUTO_INCREMENT PRIMARY KEY,
    ProductId     BIGINT NOT NULL,
    Barcode       VARCHAR(100) NOT NULL,
    LocationType  VARCHAR(10) NOT NULL,
    LocationId    BIGINT NOT NULL,
    QtyChange     DECIMAL(18,3) NOT NULL,
    BalanceAfter  DECIMAL(18,3) NOT NULL,
    MovementType  VARCHAR(30) NOT NULL,
    ReferenceType VARCHAR(30) NULL,
    ReferenceId   BIGINT NULL,
    Remarks       VARCHAR(300) NULL,
    CreatedBy     VARCHAR(100) DEFAULT 'System',
    CreatedDate   DATETIME DEFAULT CURRENT_TIMESTAMP,
    KEY IX_Ledger_Barcode (Barcode, CreatedDate),
    KEY IX_Ledger_Location (LocationType, LocationId, CreatedDate),
    KEY IX_Ledger_Reference (ReferenceType, ReferenceId),
    CONSTRAINT FK_Ledger_Product FOREIGN KEY (ProductId) REFERENCES MProducts(Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;"
        };
    }
}
