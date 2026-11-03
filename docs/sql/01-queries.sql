SET NOCOUNT ON;

PRINT '1) Orders with the price paid and the current price';
SELECT o.Id AS OrderId,
       s.Title,
       b.DisplayName AS Buyer,
       o.Status,
       o.PriceAtOrder,
       s.Price AS CurrentPrice
FROM Orders o
JOIN Services s ON s.Id = o.ServiceId
JOIN Users b ON b.Id = o.BuyerId
ORDER BY o.Id;

PRINT '2) Orders per status';
SELECT Status, COUNT(*) AS Total
FROM Orders
GROUP BY Status
ORDER BY Total DESC, Status;

PRINT '3) Seller ratings (the seller is reached through the service)';
SELECT u.DisplayName AS Seller,
       COUNT(r.Id) AS ReviewCount,
       AVG(CAST(r.Rating AS decimal(3,1))) AS AverageRating
FROM Users u
JOIN Services s ON s.SellerId = u.Id
LEFT JOIN Orders o ON o.ServiceId = s.Id
LEFT JOIN Reviews r ON r.OrderId = o.Id
WHERE u.Role = N'Seller'
GROUP BY u.DisplayName
ORDER BY AverageRating DESC;

PRINT '4) Services nobody has ordered yet';
SELECT s.Id, s.Title, s.IsActive
FROM Services s
LEFT JOIN Orders o ON o.ServiceId = s.Id
WHERE o.Id IS NULL;