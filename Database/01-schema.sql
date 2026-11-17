USE master;
GO
IF DB_ID(N'Pharma') IS NULL
    CREATE DATABASE [Pharma] COLLATE Vietnamese_100_CI_AI;
GO
USE [Pharma];
GO
IF OBJECT_ID(N'dbo.Categories', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Categories (
        Slug nvarchar(120) COLLATE Latin1_General_100_BIN2 NOT NULL PRIMARY KEY,
        Name nvarchar(200) NOT NULL,
        SourceUrl nvarchar(500) NOT NULL,
        SortOrder int NOT NULL
    );
    CREATE TABLE dbo.Products (
        Slug nvarchar(160) COLLATE Latin1_General_100_BIN2 NOT NULL PRIMARY KEY,
        CategorySlug nvarchar(120) COLLATE Latin1_General_100_BIN2 NOT NULL REFERENCES dbo.Categories(Slug),
        Name nvarchar(300) NOT NULL,
        Summary nvarchar(max) NOT NULL,
        PriceVnd int NULL CHECK (PriceVnd > 0),
        InStock bit NOT NULL,
        Packaging nvarchar(250) NULL,
        Origin nvarchar(250) NULL,
        Manufacturer nvarchar(250) NULL,
        Distributor nvarchar(250) NULL,
        SourceUrl nvarchar(500) NOT NULL,
        SortOrder int NOT NULL
    );
    CREATE INDEX IX_Products_CategorySort ON dbo.Products(CategorySlug, SortOrder);
    CREATE TABLE dbo.ProductImages (
        ProductSlug nvarchar(160) COLLATE Latin1_General_100_BIN2 NOT NULL REFERENCES dbo.Products(Slug),
        SortOrder int NOT NULL,
        Path nvarchar(400) NOT NULL,
        SourceUrl nvarchar(500) NOT NULL,
        CONSTRAINT PK_ProductImages PRIMARY KEY (ProductSlug, SortOrder)
    );
    CREATE TABLE dbo.Articles (
        Slug nvarchar(160) COLLATE Latin1_General_100_BIN2 NOT NULL PRIMARY KEY,
        Title nvarchar(300) NOT NULL,
        Summary nvarchar(max) NOT NULL,
        Section nvarchar(60) NOT NULL,
        ImagePath nvarchar(400) NOT NULL,
        SourceUrl nvarchar(500) NOT NULL,
        PublishedOn date NULL,
        SortOrder int NOT NULL
    );
    CREATE TABLE dbo.SiteAssets (
        AssetKey nvarchar(80) COLLATE Latin1_General_100_BIN2 NOT NULL PRIMARY KEY,
        Path nvarchar(400) NOT NULL,
        AltText nvarchar(300) NOT NULL,
        SourceUrl nvarchar(500) NOT NULL,
        LinkUrl nvarchar(500) NULL
    );
END;
GO
IF COL_LENGTH('dbo.Products', 'BodyHtml') IS NULL
    ALTER TABLE dbo.Products ADD BodyHtml nvarchar(max) NOT NULL CONSTRAINT DF_Products_BodyHtml DEFAULT N'';
IF COL_LENGTH('dbo.Articles', 'BodyHtml') IS NULL
    ALTER TABLE dbo.Articles ADD BodyHtml nvarchar(max) NOT NULL CONSTRAINT DF_Articles_BodyHtml DEFAULT N'';
IF COL_LENGTH('dbo.Articles', 'AuthorName') IS NULL
    ALTER TABLE dbo.Articles ADD AuthorName nvarchar(200) NOT NULL CONSTRAINT DF_Articles_AuthorName DEFAULT N'Trí Bình Pharma';
GO
IF OBJECT_ID(N'dbo.SitePages', N'U') IS NULL
    CREATE TABLE dbo.SitePages (
        Slug nvarchar(160) COLLATE Latin1_General_100_BIN2 NOT NULL CONSTRAINT PK_SitePages PRIMARY KEY,
        Title nvarchar(300) NOT NULL,
        BodyHtml nvarchar(max) NOT NULL,
        SourceUrl nvarchar(500) NOT NULL
    );
IF OBJECT_ID(N'dbo.ProductGroups', N'U') IS NULL
    CREATE TABLE dbo.ProductGroups (
        Path nvarchar(260) COLLATE Latin1_General_100_BIN2 NOT NULL CONSTRAINT PK_ProductGroups PRIMARY KEY,
        Name nvarchar(200) NOT NULL,
        SourceUrl nvarchar(500) NOT NULL
    );
IF OBJECT_ID(N'dbo.ProductGroupItems', N'U') IS NULL
    CREATE TABLE dbo.ProductGroupItems (
        GroupPath nvarchar(260) COLLATE Latin1_General_100_BIN2 NOT NULL CONSTRAINT FK_GroupItems_Groups REFERENCES dbo.ProductGroups(Path),
        ProductSlug nvarchar(160) COLLATE Latin1_General_100_BIN2 NOT NULL CONSTRAINT FK_GroupItems_Products REFERENCES dbo.Products(Slug),
        CONSTRAINT PK_ProductGroupItems PRIMARY KEY (GroupPath, ProductSlug)
    );
GO
IF OBJECT_ID(N'dbo.Consultations', N'U') IS NULL
    CREATE TABLE dbo.Consultations (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_Consultations PRIMARY KEY,
        CustomerName nvarchar(120) NOT NULL,
        Phone nvarchar(25) NOT NULL,
        Address nvarchar(350) NOT NULL,
        ProductSlug nvarchar(160) COLLATE Latin1_General_100_BIN2 NULL,
        CreatedUtc datetime2 NOT NULL CONSTRAINT DF_Consultations_CreatedUtc DEFAULT SYSUTCDATETIME()
    );
IF OBJECT_ID(N'dbo.NewsletterSubscriptions', N'U') IS NULL
    CREATE TABLE dbo.NewsletterSubscriptions (
        Email nvarchar(254) NOT NULL CONSTRAINT PK_NewsletterSubscriptions PRIMARY KEY,
        CreatedUtc datetime2 NOT NULL CONSTRAINT DF_Newsletter_CreatedUtc DEFAULT SYSUTCDATETIME()
    );
IF OBJECT_ID(N'dbo.CustomerAccounts', N'U') IS NULL
    CREATE TABLE dbo.CustomerAccounts (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_CustomerAccounts PRIMARY KEY,
        Email nvarchar(254) NOT NULL,
        NormalizedEmail nvarchar(254) COLLATE Latin1_General_100_BIN2 NOT NULL,
        PasswordHash nvarchar(500) NOT NULL,
        CONSTRAINT UQ_CustomerAccounts_Email UNIQUE (NormalizedEmail)
    );
IF OBJECT_ID(N'dbo.StoreOrders', N'U') IS NULL
    CREATE TABLE dbo.StoreOrders (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_StoreOrders PRIMARY KEY,
        AccountId uniqueidentifier NULL CONSTRAINT FK_StoreOrders_Account REFERENCES dbo.CustomerAccounts(Id),
        CustomerName nvarchar(120) NOT NULL,
        Phone nvarchar(25) NOT NULL,
        Address nvarchar(350) NOT NULL,
        Note nvarchar(600) NULL,
        SubtotalVnd bigint NOT NULL CHECK (SubtotalVnd > 0),
        CreatedUtc datetime2 NOT NULL CONSTRAINT DF_StoreOrders_CreatedUtc DEFAULT SYSUTCDATETIME()
    );
IF OBJECT_ID(N'dbo.StoreOrderItems', N'U') IS NULL
    CREATE TABLE dbo.StoreOrderItems (
        OrderId uniqueidentifier NOT NULL CONSTRAINT FK_StoreOrderItems_Order REFERENCES dbo.StoreOrders(Id),
        ProductSlug nvarchar(160) COLLATE Latin1_General_100_BIN2 NOT NULL,
        ProductName nvarchar(300) NOT NULL,
        Quantity int NOT NULL CHECK (Quantity BETWEEN 1 AND 99),
        UnitPriceVnd int NOT NULL CHECK (UnitPriceVnd > 0),
        CONSTRAINT PK_StoreOrderItems PRIMARY KEY (OrderId, ProductSlug)
    );
