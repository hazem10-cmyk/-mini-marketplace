# Mini Marketplace: Data Model

Database: PostgreSQL (set up in Phase 2).
The names below are conceptual. The real table and column names are decided in Phase 2.

## 1. Entities

### User
- Id: integer, primary key
- Email: required, unique (compared without case), up to 254 characters
- PasswordHash: required. We never store the plain password
- DisplayName: required, 2 to 50 characters
- Role: Buyer, Seller, or Admin
- IsActive: true or false, default true (false means deactivated by an admin)
- CreatedAt: UTC time

### Service
- Id: integer, primary key
- SellerId: foreign key to User.Id (the user must have the Seller role)
- Title: required, 5 to 80 characters
- Description: required, up to 2000 characters
- Price: decimal with 2 decimal places, greater than 0
- DeliveryDays: integer, 1 to 60
- IsActive: true or false, default true (false means hidden from the list)
- CreatedAt, UpdatedAt: UTC time

### Order
- Id: integer, primary key
- ServiceId: foreign key to Service.Id
- BuyerId: foreign key to User.Id (the user must have the Buyer role)
- PriceAtOrder: decimal, copied from Service.Price when the order is created. It never changes (decision D3)
- Status: Pending, InProgress, Delivered, Completed, or Cancelled
- CreatedAt, UpdatedAt: UTC time

### Review
- Id: integer, primary key
- OrderId: foreign key to Order.Id, UNIQUE (one review per order, decision D4)
- Rating: integer from 1 to 5
- Comment: optional, up to 1000 characters
- CreatedAt: UTC time

## 2. Relationships

- One seller has many services. Each service has exactly one seller.
- One buyer places many orders. Each order has exactly one buyer.
- One service can appear in many orders. Each order is for exactly one service.
- One order has zero or one review. Each review belongs to exactly one order.
- The seller of an order is found through the order's service. We do not store it twice.
- A service's average rating is calculated from its reviews. It is not stored.

## 3. Order lifecycle

Statuses:
- Pending: the buyer placed the order and the seller has not answered yet.
- InProgress: the seller accepted and is working.
- Delivered: the seller says the work is done.
- Completed: the buyer confirmed. Final.
- Cancelled: the order was cancelled while Pending. Final.

Allowed changes (everything else is rejected, the error code is decided in api.md):
- Pending to InProgress: by the seller of the order
- Pending to Cancelled: by the buyer or the seller of the order
- InProgress to Delivered: by the seller of the order
- Delivered to Completed: by the buyer of the order

There is no going back in the MVP. Completed and Cancelled orders never change again.

## 4. Decisions

- **D5:** Deleting a service only sets IsActive to false (soft delete).
  - Why: old orders point to the service. A real delete would break the order history.
- **D6:** Money is stored as decimal, never as float or double.
  - Why: floating point numbers give rounding errors (for example 0.1 + 0.2).
- **D7:** All dates are stored in UTC. The screen converts them to the user's time zone.
  - Why: one standard, no confusion between time zones.
- **D8:** Derived values (average rating, number of completed orders) are calculated, not stored.
  - Why: a stored copy can get out of date. We cache only if it becomes slow.

## 5. Rules and where they are enforced

In the database (last line of defense):
- Unique email
- Unique OrderId in Review
- Foreign keys
- Price greater than 0, and Rating between 1 and 5

In the application code (gives friendly error messages):
- Order status changes follow section 3, and only by the allowed person
- A review is allowed only for a Completed order, and only by that order's buyer
- Only the owner of a service can edit or delete it
- Inactive users cannot log in
- Services of a deactivated seller are hidden from the list

## 6. Planned indexes

- User: Email (unique, also used for login)
- Service: SellerId; and IsActive together with Price (for the list and the price filter)
- Order: BuyerId; ServiceId
- Review: OrderId (unique)

We will measure real queries in Phases 2 and 11 before adding more. Every index speeds up reads but slows down writes.

## 7. Seed data (for development)

- 1 admin, 2 sellers, 2 buyers
- About 6 services
- About 5 orders, covering every status
- 2 reviews on Completed orders

## 8. Planned changes after the MVP

- Category (US-14): a Category table (Id, Name) and a CategoryId on Service, added with a migration.
- Favorites (US-12): a table linking a buyer to a service, with a unique pair (BuyerId, ServiceId).
- Chat and notifications: new tables, designed when we reach Phase 8.