SET NOCOUNT ON;

CREATE TABLE #Svc (
    Id int IDENTITY PRIMARY KEY,
    IsActive bit NOT NULL,
    Price decimal(10,2) NOT NULL,
    Title nvarchar(80) NOT NULL
);

WITH n AS (
    SELECT TOP (200000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS i
    FROM sys.all_objects a CROSS JOIN sys.all_objects b
)
INSERT INTO #Svc (IsActive, Price, Title)
SELECT CASE WHEN i % 10 = 0 THEN 0 ELSE 1 END,
       CAST((i % 500) + 1 AS decimal(10,2)),
       CONCAT(N'Service ', i)
FROM n;
GO

SET STATISTICS IO ON;

PRINT '[BEFORE] count: active services with price <= 5';
SELECT COUNT(*) AS Matches FROM #Svc WHERE IsActive = 1 AND Price <= 5;

PRINT '[BEFORE] first page sorted by price';
SELECT Id, Title, Price FROM #Svc
WHERE IsActive = 1
ORDER BY Price, Id
OFFSET 0 ROWS FETCH NEXT 10 ROWS ONLY;
GO

CREATE INDEX IX_Svc_IsActive_Price ON #Svc (IsActive, Price);
GO

PRINT '[AFTER] count: active services with price <= 5';
SELECT COUNT(*) AS Matches FROM #Svc WHERE IsActive = 1 AND Price <= 5;

PRINT '[AFTER] first page sorted by price';
SELECT Id, Title, Price FROM #Svc
WHERE IsActive = 1
ORDER BY Price, Id
OFFSET 0 ROWS FETCH NEXT 10 ROWS ONLY;
GO

SET STATISTICS IO OFF;
DROP TABLE #Svc;