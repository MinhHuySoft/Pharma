SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

MERGE dbo.Categories WITH (HOLDLOCK) AS target
USING OPENJSON(@snapshot, '$.categories')
    WITH (Slug nvarchar(120), Name nvarchar(200), SourceUrl nvarchar(500), SortOrder int) AS source
ON target.Slug = source.Slug COLLATE Latin1_General_100_BIN2
WHEN MATCHED THEN UPDATE SET Name = source.Name, SourceUrl = source.SourceUrl, SortOrder = source.SortOrder
WHEN NOT MATCHED THEN INSERT (Slug, Name, SourceUrl, SortOrder)
    VALUES (source.Slug, source.Name, source.SourceUrl, source.SortOrder);

MERGE dbo.Products WITH (HOLDLOCK) AS target
USING OPENJSON(@snapshot, '$.products')
    WITH (Slug nvarchar(160), CategorySlug nvarchar(120), Name nvarchar(300), Summary nvarchar(max),
        PriceVnd int, InStock bit, Packaging nvarchar(250), Origin nvarchar(250), Manufacturer nvarchar(250),
        Distributor nvarchar(250), SourceUrl nvarchar(500), SortOrder int, BodyHtml nvarchar(max)) AS source
ON target.Slug = source.Slug COLLATE Latin1_General_100_BIN2
WHEN MATCHED THEN UPDATE SET CategorySlug = source.CategorySlug, Name = source.Name, Summary = source.Summary,
    PriceVnd = source.PriceVnd, InStock = source.InStock, Packaging = source.Packaging, Origin = source.Origin,
    Manufacturer = source.Manufacturer, Distributor = source.Distributor, SourceUrl = source.SourceUrl,
    SortOrder = source.SortOrder, BodyHtml = COALESCE(source.BodyHtml, target.BodyHtml)
WHEN NOT MATCHED THEN INSERT (Slug, CategorySlug, Name, Summary, PriceVnd, InStock, Packaging, Origin,
    Manufacturer, Distributor, SourceUrl, SortOrder, BodyHtml)
    VALUES (source.Slug, source.CategorySlug, source.Name, source.Summary, source.PriceVnd, source.InStock,
        source.Packaging, source.Origin, source.Manufacturer, source.Distributor, source.SourceUrl, source.SortOrder,
        COALESCE(source.BodyHtml, N''));

-- Refresh gallery positions only for products present in the snapshot.
DELETE images
FROM dbo.ProductImages AS images
JOIN OPENJSON(@snapshot, '$.products') WITH (Slug nvarchar(160)) AS source
    ON images.ProductSlug = source.Slug COLLATE Latin1_General_100_BIN2;

INSERT dbo.ProductImages (ProductSlug, SortOrder, Path, SourceUrl)
SELECT ProductSlug, SortOrder, Path, SourceUrl
FROM OPENJSON(@snapshot, '$.images')
    WITH (ProductSlug nvarchar(160), SortOrder int, Path nvarchar(400), SourceUrl nvarchar(500));

MERGE dbo.Articles WITH (HOLDLOCK) AS target
USING OPENJSON(@snapshot, '$.articles')
    WITH (Slug nvarchar(160), Title nvarchar(300), Summary nvarchar(max), Section nvarchar(60),
        ImagePath nvarchar(400), SourceUrl nvarchar(500), PublishedOn date, SortOrder int, BodyHtml nvarchar(max), AuthorName nvarchar(200)) AS source
ON target.Slug = source.Slug COLLATE Latin1_General_100_BIN2
WHEN MATCHED THEN UPDATE SET Title = source.Title, Summary = source.Summary, Section = source.Section,
    ImagePath = source.ImagePath, SourceUrl = source.SourceUrl, PublishedOn = source.PublishedOn, SortOrder = source.SortOrder,
    BodyHtml = COALESCE(source.BodyHtml, target.BodyHtml), AuthorName = COALESCE(source.AuthorName, target.AuthorName)
WHEN NOT MATCHED THEN INSERT (Slug, Title, Summary, Section, ImagePath, SourceUrl, PublishedOn, SortOrder, BodyHtml, AuthorName)
    VALUES (source.Slug, source.Title, source.Summary, source.Section, source.ImagePath, source.SourceUrl, source.PublishedOn,
        source.SortOrder, COALESCE(source.BodyHtml, N''), COALESCE(source.AuthorName, N'Trí Bình Pharma'));

MERGE dbo.SitePages WITH (HOLDLOCK) AS target
USING OPENJSON(@snapshot, '$.pages')
    WITH (Slug nvarchar(160), Title nvarchar(300), BodyHtml nvarchar(max), SourceUrl nvarchar(500)) AS source
ON target.Slug = source.Slug COLLATE Latin1_General_100_BIN2
WHEN MATCHED THEN UPDATE SET Title = source.Title, BodyHtml = source.BodyHtml, SourceUrl = source.SourceUrl
WHEN NOT MATCHED THEN INSERT (Slug, Title, BodyHtml, SourceUrl)
    VALUES (source.Slug, source.Title, source.BodyHtml, source.SourceUrl);

MERGE dbo.ProductGroups WITH (HOLDLOCK) AS target
USING OPENJSON(@snapshot, '$.productGroups')
    WITH (Path nvarchar(260), Name nvarchar(200), SourceUrl nvarchar(500)) AS source
ON target.Path = source.Path COLLATE Latin1_General_100_BIN2
WHEN MATCHED THEN UPDATE SET Name = source.Name, SourceUrl = source.SourceUrl
WHEN NOT MATCHED THEN INSERT (Path, Name, SourceUrl)
    VALUES (source.Path, source.Name, source.SourceUrl);

IF JSON_QUERY(@snapshot, '$.productGroupItems') IS NOT NULL
BEGIN
    DELETE groupItems
    FROM dbo.ProductGroupItems AS groupItems
    JOIN OPENJSON(@snapshot, '$.products') WITH (Slug nvarchar(160)) AS source
        ON groupItems.ProductSlug = source.Slug COLLATE Latin1_General_100_BIN2;

    INSERT dbo.ProductGroupItems (GroupPath, ProductSlug)
    SELECT DISTINCT GroupPath, ProductSlug
    FROM OPENJSON(@snapshot, '$.productGroupItems')
        WITH (GroupPath nvarchar(260), ProductSlug nvarchar(160));
END;

MERGE dbo.SiteAssets WITH (HOLDLOCK) AS target
USING OPENJSON(@snapshot, '$.assets')
    WITH (AssetKey nvarchar(80), Path nvarchar(400), AltText nvarchar(300), SourceUrl nvarchar(500), LinkUrl nvarchar(500)) AS source
ON target.AssetKey = source.AssetKey COLLATE Latin1_General_100_BIN2
WHEN MATCHED THEN UPDATE SET Path = source.Path, AltText = source.AltText, SourceUrl = source.SourceUrl, LinkUrl = source.LinkUrl
WHEN NOT MATCHED THEN INSERT (AssetKey, Path, AltText, SourceUrl, LinkUrl)
    VALUES (source.AssetKey, source.Path, source.AltText, source.SourceUrl, source.LinkUrl);

COMMIT;
SELECT (SELECT COUNT(*) FROM dbo.Categories) AS Categories,
       (SELECT COUNT(*) FROM dbo.Products) AS Products,
       (SELECT COUNT(*) FROM dbo.ProductImages) AS Images,
       (SELECT COUNT(*) FROM dbo.Articles) AS Articles,
       (SELECT COUNT(*) FROM dbo.SiteAssets) AS SiteAssets,
       (SELECT COUNT(*) FROM dbo.SitePages) AS Pages,
       (SELECT COUNT(*) FROM dbo.ProductGroups) AS ProductGroups;
