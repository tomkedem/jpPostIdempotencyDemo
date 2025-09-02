-- ====================================================================
-- Full Database Setup for Postal Idempotency Demo
-- Deletes all existing tables and recreates the entire schema with rich sample data.
-- Run this script on the 'PostalIdempotencyDemo' database.
-- ====================================================================

-- Step 1: Drop all existing objects to ensure a clean slate
-- Drop triggers first
IF OBJECT_ID('trg_shipments_updated_at', 'TR') IS NOT NULL
    DROP TRIGGER trg_shipments_updated_at;
GO

IF OBJECT_ID('trg_delivery_with_date_updated_at', 'TR') IS NOT NULL
    DROP TRIGGER trg_delivery_with_date_updated_at;
GO

-- Drop tables, respecting foreign key constraints
IF OBJECT_ID('operation_metrics', 'U') IS NOT NULL
    DROP TABLE operation_metrics;
GO

IF OBJECT_ID('delivery_with_date', 'U') IS NOT NULL
    DROP TABLE delivery_with_date;
GO

IF OBJECT_ID('delivery_checks', 'U') IS NOT NULL
    DROP TABLE delivery_checks;
GO

IF OBJECT_ID('signatures', 'U') IS NOT NULL
    DROP TABLE signatures;
GO

IF OBJECT_ID('idempotency_entries', 'U') IS NOT NULL
    DROP TABLE idempotency_entries;
GO

IF OBJECT_ID('deliveries', 'U') IS NOT NULL
    DROP TABLE deliveries;
GO

IF OBJECT_ID('shipment_statuses', 'U') IS NOT NULL
    DROP TABLE shipment_statuses;
GO

IF OBJECT_ID('shipments', 'U') IS NOT NULL
    DROP TABLE shipments;
GO


-- Step 2: Create all tables with the updated schema

-- Shipment Statuses Lookup Table
CREATE TABLE shipment_statuses (
    id INT PRIMARY KEY,
    status_name NVARCHAR(50) NOT NULL UNIQUE,
    status_name_he NVARCHAR(50) NOT NULL UNIQUE,
    description NVARCHAR(255)
);
GO

-- Deliveries Table (Normalized)
CREATE TABLE deliveries (
    id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    barcode NVARCHAR(50) NOT NULL,
    employee_id NVARCHAR(20),
    delivery_date DATETIME2 DEFAULT GETUTCDATE(),
    location_lat DECIMAL(10, 8),
    location_lng DECIMAL(11, 8),
    recipient_name NVARCHAR(100),
    status_id INT NOT NULL,
    notes NVARCHAR(500),
    created_at DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    updated_at DATETIME2,
    CONSTRAINT FK_deliveries_status FOREIGN KEY (status_id) REFERENCES shipment_statuses(id)
);
GO

-- Shipments Table (Legacy structure for demo purposes)
CREATE TABLE shipments (
    id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    barcode NVARCHAR(50) NOT NULL UNIQUE,
    kod_peula INT,
    perut_peula INT,
    atar INT,
    customer_name NVARCHAR(100),
    address NVARCHAR(200),
    weight DECIMAL(8,3),
    price DECIMAL(10,2),
    status_id INT DEFAULT 1,
    created_at DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    updated_at DATETIME2,
    notes NVARCHAR(500)
);
GO

-- Idempotency Entries Table
CREATE TABLE idempotency_entries (
    Id NVARCHAR(128) PRIMARY KEY,
    RequestSignature NVARCHAR(64) NOT NULL,
    ResponseData NVARCHAR(MAX),
    Endpoint NVARCHAR(255),
    HttpMethod NVARCHAR(10),
    StatusCode INT,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
GO

-- Operation Metrics Table
CREATE TABLE operation_metrics (
    id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    operation_type NVARCHAR(50) NOT NULL,
    endpoint NVARCHAR(100) NOT NULL,
    execution_time_ms BIGINT NOT NULL,
    is_idempotent_hit BIT NOT NULL DEFAULT 0,
    idempotency_key NVARCHAR(128),
    is_error BIT NOT NULL DEFAULT 0,
    created_at DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
GO



-- Step 3: Create Indexes and Triggers

CREATE INDEX idx_deliveries_barcode ON deliveries(barcode);
CREATE INDEX idx_shipments_barcode ON shipments(barcode);

CREATE INDEX idx_operation_metrics_created_at ON operation_metrics(created_at);
GO

CREATE TRIGGER trg_shipments_updated_at
ON shipments
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE shipments
    SET updated_at = GETUTCDATE()
    FROM shipments s
    INNER JOIN inserted i ON s.id = i.id;
END;
GO


-- Step 4: Populate tables with rich sample data

-- Populate shipment_statuses
INSERT INTO shipment_statuses (id, status_name, status_name_he, description)
VALUES
(1, 'Created', N'נוצר', 'The shipment has been created in the system but not yet processed.'),
(2, 'Delivered', N'נמסר', 'The shipment has been successfully delivered to the recipient.'),
(3, 'Failed', N'נכשל', 'The delivery attempt failed.'),
(4, 'Partial', N'נמסר חלקית', 'The shipment was partially delivered.'),
(5, 'In Transit', N'בדרך', 'The shipment is on its way.'),
(6, 'Out for Delivery', N'בדרך לחלוקה', 'The shipment is out for delivery with a courier.'),
(7, 'Exception', N'חריגה', 'An unexpected issue occurred with the shipment.');
GO

-- Populate deliveries
DECLARE @i INT = 1;
WHILE @i <= 200
BEGIN
    INSERT INTO deliveries (barcode, employee_id, location_lat, location_lng, recipient_name, status_id, notes)
    SELECT TOP 1
        'DEMO' + RIGHT('000000' + CAST((ABS(CHECKSUM(NEWID())) % 150) + 1 AS VARCHAR), 6),
        'EMP' + RIGHT('000' + CAST((ABS(CHECKSUM(NEWID())) % 50) + 1 AS VARCHAR), 3),
        32.0 + (RAND() * 2),
        34.7 + (RAND() * 1.5),
        (SELECT TOP 1 name FROM (VALUES (N'יוסי כהן'),(N'שרה לוי'),(N'דוד אברהם')) AS T(name) ORDER BY NEWID()),
        CASE WHEN @i % 10 = 0 THEN 3 WHEN @i % 15 = 0 THEN 4 ELSE 2 END, -- Mix of Delivered, Failed, Partial
        CASE WHEN @i % 10 = 0 THEN N'לא נמצא בבית' WHEN @i % 8 = 0 THEN N'נמסר לשכן' ELSE N'נמסר בהצלחה' END;
    SET @i = @i + 1;
END;
GO

PRINT 'Database setup complete. All tables created and populated with sample data.';
GO
