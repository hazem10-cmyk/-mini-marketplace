# Mini Marketplace: API Design

This file is the contract between the backend and the frontend.
We design it before writing the code, so both sides build against the same rules.

## 1. Conventions

- Base path: /api. Requests and responses are JSON.
- JSON property names use camelCase (for example deliveryDays).
- Dates use the ISO 8601 format in UTC (for example 2026-10-30T14:05:00Z).
- Money is a JSON number with up to 2 decimals.
- Ids in the URL are integers. A non-number such as /api/services/abc does not match any route, so it returns 404.
- Logged-in requests send the header: Authorization: Bearer <token>. The token details are decided in Phase 4.
- Access levels used below: Public (no login), Logged-in (any role), Buyer, Seller, Admin.

### Pagination

- Query parameters: page (starts at 1, default 1) and pageSize (default 10, maximum 50).
- A paged response has: items, page, pageSize, totalItems, totalPages.
- An empty result is a normal 200 response with an empty items list. It is not an error.

### Errors

- Errors use the Problem Details format (RFC 9457), which ASP.NET Core supports.
- Fields: status, title, detail. Validation errors also have errors (a field name with a list of messages).

### Status codes we use

- 200 OK: success with a body
- 201 Created: something new was created
- 204 No Content: success without a body
- 400 Bad Request: invalid input (validation failed)
- 401 Unauthorized: not logged in, or the token or password is wrong
- 403 Forbidden: logged in, but not allowed to do this
- 404 Not Found: the thing does not exist (or we hide that it exists)
- 409 Conflict: the request is valid but conflicts with the current state (duplicate email, wrong order status, already reviewed)

## 2. Endpoints

### Health

- GET /api/health (Public)
  - 200 with status and the server time. Already built in Phase 0.

### Auth (US-01)

- POST /api/auth/register (Public)
  - Body: email, password (at least 8 characters), displayName, role (Buyer or Seller only)
  - 201 with the new user (never the password). Errors: 400 invalid data (including role Admin), 409 email already used
- POST /api/auth/login (Public)
  - Body: email, password
  - 200 with a token and the user. Errors: 401 wrong email or password (same message for both), 403 account deactivated
- GET /api/auth/me (Logged-in)
  - 200 with the current user. Errors: 401

### Services (US-02 to US-05)

- GET /api/services (Public, US-04)
  - Query: q (search in title and description), minPrice, maxPrice, sort (newest, priceAsc, priceDesc; default newest), page, pageSize
  - 200 with a paged result. Each item: id, title, price, deliveryDays, sellerName, averageRating, reviewCount
  - Only active services of active sellers appear
- GET /api/services/{id} (Public, US-05)
  - 200 with: id, title, description, price, deliveryDays, seller (id and displayName), averageRating, reviewCount
  - averageRating is null when there are no reviews. Error: 404 (also when the service is inactive)
- GET /api/services/{id}/reviews (Public, US-05)
  - Query: page, pageSize
  - 200 with a paged result. Each item: rating, comment, buyerName, createdAt. Error: 404 service not found
- GET /api/services/mine (Seller, US-03)
  - Query: page, pageSize
  - 200 with a paged result of the seller's own services. Errors: 401, 403
  - "mine" is not a number, so it does not clash with {id}
- POST /api/services (Seller, US-02)
  - Body: title (5 to 80 characters), description (required, up to 2000), price (greater than 0), deliveryDays (1 to 60)
  - 201 with the new service. Errors: 400, 401, 403
- PUT /api/services/{id} (the Seller who owns it, US-03)
  - Body: the same fields as create (full replacement)
  - 200 with the updated service. Errors: 400, 401, 403 (not the owner), 404
- DELETE /api/services/{id} (the Seller who owns it, US-03)
  - Soft delete (D5): sets IsActive to false
  - 204 with no body. Errors: 401, 403 (not the owner), 404

### Orders (US-06 to US-09)

- POST /api/orders (Buyer, US-06)
  - Body: serviceId only. The server copies the price (D12)
  - 201 with the new order (status Pending). Errors: 400 missing serviceId, 401, 403 (not a buyer), 404 (service not found or inactive)
- GET /api/orders (Buyer or Seller)
  - A buyer sees the orders they placed. A seller sees the orders of their services
  - Query: status (optional), page, pageSize
  - 200 with a paged result. Each item: id, serviceTitle, otherPartyName, priceAtOrder, status, createdAt
- GET /api/orders/{id} (the buyer or the seller of that order)
  - 200 with the full order. Errors: 401, 404 (also when you are not part of the order, D10)
- POST /api/orders/{id}/accept (the seller of the order, US-07)
  - Pending to InProgress
- POST /api/orders/{id}/deliver (the seller of the order, US-07)
  - InProgress to Delivered
- POST /api/orders/{id}/complete (the buyer of the order, US-08)
  - Delivered to Completed
- POST /api/orders/{id}/cancel (the buyer or the seller of the order, US-09)
  - Pending to Cancelled
- For these four actions: 200 with the updated order. Errors: 401, 403 (you are part of the order but not allowed to do this action), 404 (not found or not yours), 409 (the order is not in the required status)

### Reviews (US-10)

- POST /api/orders/{id}/review (the buyer of the order)
  - Body: rating (1 to 5), comment (optional, up to 1000)
  - 201 with the review. Errors: 400 invalid data, 401, 403 (not the buyer of this order), 404, 409 (the order is not Completed, or it already has a review)

### Admin (US-11)

- GET /api/admin/users (Admin)
  - Query: page, pageSize
  - 200 with a paged result. Each item: id, email, displayName, role, isActive
- POST /api/admin/users/{id}/deactivate (Admin)
  - 204 with no body. Errors: 401, 403, 404, 409 (an admin cannot deactivate themselves)
- POST /api/admin/users/{id}/activate (Admin)
  - 204 with no body. Errors: 401, 403, 404
- GET /api/admin/services (Admin)
  - Query: page, pageSize
  - 200 with a paged result, including inactive services
- POST /api/admin/services/{id}/deactivate (Admin)
  - 204 with no body. Errors: 401, 403, 404
- POST /api/admin/services/{id}/activate (Admin)
  - 204 with no body. Errors: 401, 403, 404

## 3. Not in the MVP

Chat, notifications, image upload, favorites, the seller profile page, and the category filter.
They come later and will get their own endpoints.

## 4. Decisions

- **D9:** Order status changes are separate action endpoints (accept, deliver, complete, cancel), not one generic "set status" endpoint.
  - Why: each action has its own allowed person and rules, and the errors are clearer.
- **D10:** A user who is not part of an order gets 404, not 403.
  - Why: we do not reveal that the order exists.
- **D11:** There is no logout endpoint. The client deletes its token.
  - Why: tokens are stateless. We revisit this if we ever need to revoke a token.
- **D12:** The client never sends values it could cheat with. The price comes from the service, the buyer comes from the token, and the role Admin cannot be requested.
  - Why: never trust the client with money or permissions.
- **D13:** Every list is paginated, with a maximum pageSize of 50.
  - Why: it protects the server and the browser from huge responses.
- **D14:** A valid request that does not fit the current state (for example, completing an order that is Pending) returns 409.
  - Why: 400 means the input itself is wrong. 409 means the input is fine but the state does not allow it.