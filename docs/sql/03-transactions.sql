SET NOCOUNT ON;

PRINT '[1] Before';
SELECT s.Title, s.IsActive, o.Status
FROM Services s JOIN Orders o ON o.ServiceId = s.Id
WHERE s.Title LIKE N'Proofreading%';
GO

PRINT '[2] Inside a transaction: deactivate the service and cancel its pending orders';
BEGIN TRANSACTION;

UPDATE Services
SET IsActive = 0, UpdatedAt = SYSUTCDATETIME()
WHERE Title LIKE N'Proofreading%';

UPDATE Orders
SET Status = N'Cancelled', UpdatedAt = SYSUTCDATETIME()
WHERE Status = N'Pending'
  AND ServiceId IN (SELECT Id FROM Services WHERE Title LIKE N'Proofreading%');

SELECT s.Title, s.IsActive, o.Status
FROM Services s JOIN Orders o ON o.ServiceId = s.Id
WHERE s.Title LIKE N'Proofreading%';

ROLLBACK TRANSACTION;
GO

PRINT '[3] After ROLLBACK: nothing changed';
SELECT s.Title, s.IsActive, o.Status
FROM Services s JOIN Orders o ON o.ServiceId = s.Id
WHERE s.Title LIKE N'Proofreading%';
GO

PRINT '[4] A transaction where the second statement breaks a CHECK constraint';
SET XACT_ABORT ON;
BEGIN TRANSACTION;

UPDATE Services SET Price = 30 WHERE Title LIKE N'Professional logo%';
UPDATE Services SET Price = -1 WHERE Title LIKE N'Article translation%';

COMMIT TRANSACTION;
GO

SET XACT_ABORT OFF;

PRINT '[5] After the failure: both prices are unchanged';
SELECT Title, Price FROM Services
WHERE Title LIKE N'Professional logo%' OR Title LIKE N'Article translation%';
GO