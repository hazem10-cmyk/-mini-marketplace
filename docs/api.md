
## 5. As built (Phase 3)

This section records what the code does today. Where it differs from the design above, this section wins.
The exact request and response shapes are always available from the running API at /openapi/v1.json (Development only), with a browsable view at /scalar/v1.

### Identity until Phase 4

- Real login is not built yet. In the Development environment only, a request can send the header X-Dev-User with a user id, and the API treats the caller as that user.
- Seed users: 1 Admin, 2 Sara (Seller), 3 Omar (Seller), 4 Ali (Buyer), 5 Maya (Buyer).
- Public endpoints ignore the header. On protected endpoints, a missing, non-numeric, unknown, or inactive user gets 401.
- Phase 4 removes this and brings Authorization: Bearer <token>.

### Responses and errors

- Enum values are strings. Role: Buyer, Seller, or Admin. Order status: Pending, InProgress, Delivered, Completed, or Cancelled.
- Dates are UTC with a Z suffix (for example 2026-11-04T22:39:35.9079062Z).
- Every error is Problem Details with type, title, status, and traceId. Validation errors (400) add errors: a map from a camelCase field name to a list of messages. Business errors (403 and 409) add detail with a readable sentence.
- Empty 401, 403, and 404 responses from the framework are also turned into Problem Details.

### Services

- GET /api/services/mine returns only active services. Each item: id, title, price, deliveryDays, createdAt.
- POST and PUT trim the title and the description. Rules: title 5 to 80 characters, description 1 to 2000, price greater than 0 and at most 99999999.99 with at most 2 decimals, deliveryDays 1 to 60.
- POST returns 201 with the service details and a Location header. PUT returns 200 with the service details.
- PUT and DELETE on an inactive service return 404, the same as a missing one.
- DELETE sets IsActive to false (decision D5). Admin activate can bring the service back (see open question Q1 in screens.md).

### Orders

- POST /api/orders needs only serviceId. The price is copied from the service (D3). A missing serviceId is 400. A missing or inactive service, or a service of a deactivated seller, is 404.
- GET /api/orders: a buyer sees the orders they placed, a seller sees the orders of their services, newest first. An admin gets 403. The optional status filter uses the status name and ignores letter case. A number or an unknown name is 400.
- GET /api/orders/{id} returns id, serviceId, serviceTitle, buyer and seller (each with id and displayName), priceAtOrder, status, createdAt, updatedAt, and review (null when there is none, otherwise rating, comment, and createdAt).
- accept, deliver, complete, and cancel check in this order: not part of the order is 404, even for an admin (D10). Not allowed to do this action is 403. The order is in the wrong status is 409 (D14). The status change is one conditional UPDATE, so of two simultaneous requests only one succeeds and the other gets 409.
- The allowed changes are written in Domain/OrderRules.cs and match data-model.md section 3.

### Reviews

- POST /api/orders/{id}/review: body rating (1 to 5, required) and comment (optional, trimmed, up to 1000 characters, an empty comment becomes null).
- Not part of the order is 404. The seller of the order is 403. An order that is not Completed is 409. A second review is 409, including two simultaneous requests (the unique index on OrderId decides).
- 201 returns rating, comment, and createdAt.

### Admin

- Everything under /api/admin needs the Admin role. The policy is set once on the group.
- Lists are ordered by id, ascending. Users: id, email, displayName, role, isActive. Services: id, title, sellerName, price, isActive, and they include inactive services.
- activate and deactivate return 204 and are idempotent. A missing id is 404. An admin deactivating their own account is 409.
- A deactivated user cannot log in. A deactivated seller's services disappear from the public list, the details, the reviews, and from ordering.
- Open for Phase 4: with JWT, a token issued before the deactivation stays valid until it expires, unless we check IsActive on every request.