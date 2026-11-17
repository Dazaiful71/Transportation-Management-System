-- =====================================================================
-- Transportation Management System - TMS database schema (MySQL 8)
--
-- Creates every table the app reads and writes. Safe to re-run: it drops
-- and recreates the `tms` database.
--
--   mysql -u root -p < database/01_schema.sql
--   mysql -u root -p < database/02_seed.sql
--   mysql -u root -p < database/03_marketplace.sql   (optional local marketplace)
-- =====================================================================

DROP DATABASE IF EXISTS tms;
CREATE DATABASE tms CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
USE tms;

-- ---------------------------------------------------------------------
-- Users: everyone who can sign in. UserType: 0 = Buyer, 1 = Planner, 2 = Admin
-- ---------------------------------------------------------------------
CREATE TABLE Users (
    UserID       INT          NOT NULL AUTO_INCREMENT,
    FirstName    VARCHAR(50)  NOT NULL,
    LastName     VARCHAR(50)  NOT NULL,
    Username     VARCHAR(50)  NOT NULL,
    PasswordHash VARCHAR(100) NOT NULL,            -- BCrypt hash, never plain text
    Email        VARCHAR(100) NOT NULL,
    IsActive     TINYINT(1)   NOT NULL DEFAULT 1,  -- read as bool by the app
    UserType     TINYINT      NOT NULL,
    PRIMARY KEY (UserID),
    UNIQUE KEY UQ_Users_Username (Username),
    CONSTRAINT CK_Users_UserType CHECK (UserType IN (0, 1, 2))
);

-- ---------------------------------------------------------------------
-- Clients: customers that place orders through the marketplace
-- ---------------------------------------------------------------------
CREATE TABLE Clients (
    ClientID   INT          NOT NULL AUTO_INCREMENT,
    ClientName VARCHAR(100) NOT NULL,
    IsActive   INT          NOT NULL DEFAULT 1,    -- read as int by the app
    PRIMARY KEY (ClientID),
    UNIQUE KEY UQ_Clients_ClientName (ClientName)
);

-- ---------------------------------------------------------------------
-- Orders. Origin/Destination hold city names (e.g. 'Toronto').
-- JobType: 0 = FTL, 1 = LTL.  VanType: 0 = Dry Van, 1 = Reefer.
-- ---------------------------------------------------------------------
CREATE TABLE Orders (
    OrderID            INT         NOT NULL AUTO_INCREMENT,
    ClientID           INT         NOT NULL,
    Origin             VARCHAR(20) NOT NULL,
    Destination        VARCHAR(20) NOT NULL,
    JobType            TINYINT     NOT NULL,
    Quantity           INT         NOT NULL DEFAULT 0,  -- pallets (LTL); 0 for FTL
    VanType            TINYINT     NOT NULL,
    OrderCreationDate  DATETIME    NULL,                -- when the buyer accepted the contract
    OrderDate          DATETIME    NULL,                -- when the planner assigned a carrier
    OrderCompletedDate DATETIME    NULL,
    IsCompleted        TINYINT     NOT NULL DEFAULT 0,
    InvoiceGenerated   TINYINT     NOT NULL DEFAULT 0,
    PRIMARY KEY (OrderID),
    KEY IX_Orders_Status (IsCompleted, InvoiceGenerated),
    CONSTRAINT FK_Orders_Clients FOREIGN KEY (ClientID) REFERENCES Clients (ClientID),
    CONSTRAINT CK_Orders_JobType CHECK (JobType IN (0, 1)),
    CONSTRAINT CK_Orders_VanType CHECK (VanType IN (0, 1)),
    CONSTRAINT CK_Orders_Quantity CHECK (Quantity >= 0)
);

-- ---------------------------------------------------------------------
-- Carriers and their rates.
--   FTLRate    : $ per km for a full truckload
--   LTLRate    : $ per pallet per km for less-than-truckload
--   reefCharge : extra fraction for refrigerated vans (0.08 = +8%)
-- ---------------------------------------------------------------------
CREATE TABLE Carriers (
    CarrierID   INT           NOT NULL AUTO_INCREMENT,
    CarrierName VARCHAR(100)  NOT NULL,
    FTLRate     DECIMAL(10,4) NOT NULL,
    LTLRate     DECIMAL(10,4) NOT NULL,
    reefCharge  DECIMAL(6,4)  NOT NULL DEFAULT 0,
    IsActive    TINYINT(1)    NOT NULL DEFAULT 1,  -- read as bool by the app
    PRIMARY KEY (CarrierID),
    UNIQUE KEY UQ_Carriers_Name (CarrierName),
    CONSTRAINT CK_Carriers_Rates CHECK (FTLRate >= 0 AND LTLRate >= 0 AND reefCharge >= 0)
);

-- ---------------------------------------------------------------------
-- Depot cities for each carrier with available FTL trucks / LTL pallet space
-- ---------------------------------------------------------------------
CREATE TABLE CarrierCity (
    CarrierID INT         NOT NULL,
    DepotCity VARCHAR(20) NOT NULL,
    FTLAval   INT         NOT NULL DEFAULT 0,
    LTLAval   INT         NOT NULL DEFAULT 0,
    PRIMARY KEY (CarrierID, DepotCity),
    KEY IX_CarrierCity_Depot (DepotCity),
    CONSTRAINT FK_CarrierCity_Carriers FOREIGN KEY (CarrierID) REFERENCES Carriers (CarrierID) ON DELETE CASCADE
);

-- ---------------------------------------------------------------------
-- Routes along the Windsor - Ottawa corridor.
-- Cities are stored as numbers: 0 Windsor, 1 London, 2 Hamilton, 3 Toronto,
-- 4 Oshawa, 5 Belleville, 6 Kingston, 7 Ottawa.
-- Each row is the leg from `Destination` to the city on its East;
-- Distance (km) and Time (hours of driving) describe that leg.
-- West/East are NULL at the ends of the line.
-- ---------------------------------------------------------------------
CREATE TABLE Routes (
    Destination TINYINT       NOT NULL,
    Distance    INT           NOT NULL DEFAULT 0,
    Time        DECIMAL(5,2)  NOT NULL DEFAULT 0,
    West        TINYINT       NULL,
    East        TINYINT       NULL,
    PRIMARY KEY (Destination)
);

-- ---------------------------------------------------------------------
-- Mark-up rates. Name = 'OSHT'; RateType 0 = FTL, 1 = LTL; Rate is a fraction.
-- ---------------------------------------------------------------------
CREATE TABLE Rates (
    Name     VARCHAR(20)  NOT NULL,
    RateType TINYINT      NOT NULL,
    Rate     DECIMAL(6,4) NOT NULL,
    PRIMARY KEY (Name, RateType)
);

-- ---------------------------------------------------------------------
-- Trips: one per carrier assigned to an order
-- ---------------------------------------------------------------------
CREATE TABLE Trips (
    TripID          INT          NOT NULL AUTO_INCREMENT,
    OrderID         INT          NOT NULL,
    CarrierID       INT          NOT NULL,
    OriginCity      VARCHAR(20)  NOT NULL,
    DestinationCity VARCHAR(20)  NOT NULL,
    JobType         TINYINT      NOT NULL,
    VanType         TINYINT      NOT NULL,
    TotalDistance   INT          NOT NULL DEFAULT 0,
    TotalTime       DECIMAL(8,2) NOT NULL DEFAULT 0,  -- hours, including stops and rest
    PRIMARY KEY (TripID),
    KEY IX_Trips_Order (OrderID),
    CONSTRAINT FK_Trips_Orders   FOREIGN KEY (OrderID)   REFERENCES Orders (OrderID) ON DELETE CASCADE,
    CONSTRAINT FK_Trips_Carriers FOREIGN KEY (CarrierID) REFERENCES Carriers (CarrierID)
);

-- ---------------------------------------------------------------------
-- Invoices. Column order matters: the app inserts with positional VALUES.
-- ---------------------------------------------------------------------
CREATE TABLE Invoices (
    OrderID       INT           NOT NULL,
    ClientName    VARCHAR(100)  NOT NULL,
    Origin        VARCHAR(20)   NOT NULL,
    Destination   VARCHAR(20)   NOT NULL,
    TotalAmount   DECIMAL(12,2) NOT NULL,
    TotalDistance INT           NOT NULL,
    TotalDays     DECIMAL(6,1)  NOT NULL,
    CompletedDate DATETIME      NOT NULL,
    PRIMARY KEY (OrderID),
    KEY IX_Invoices_CompletedDate (CompletedDate),
    CONSTRAINT FK_Invoices_Orders FOREIGN KEY (OrderID) REFERENCES Orders (OrderID)
);
