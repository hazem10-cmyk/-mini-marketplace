# Mini Marketplace: Requirements

## 1. Summary

A small marketplace where sellers publish services and buyers order them.
It is built step by step to learn full-stack engineering.

## 2. Roles

| Role | Who | Can do |
|---|---|---|
| Visitor | Not logged in | Browse, search, and view services |
| Buyer | Registered user | Everything a visitor can, plus order services and review completed orders |
| Seller | Registered user | Everything a visitor can, plus publish services and deliver orders |
| Admin | Platform owner (created by seed data) | Deactivate users and services |

## 3. Decisions

| # | Decision | Why |
|---|---|---|
| D1 | A user picks ONE role (Buyer or Seller) at registration | Keeps authorization simple. We can revisit it later |
| D2 | No real payments in the MVP. An order only records the price | Payments are a big separate topic |
| D3 | An order stores a copy of the price at order time | The service price may change later, but the order must keep the agreed price |
| D4 | One review per completed order | Prevents duplicate or fake reviews |

## 4. Scope

### MVP (must have)

- Register, log in, log out
- Sellers: create, edit, and delete their own services
- Everyone: list services with search, price filter, and pagination; view service details
- Buyers: place an order
- Order lifecycle: Pending → InProgress → Delivered → Completed (or Cancelled)
- Buyers: review a completed order (1 to 5 stars and a comment)
- Buyers and sellers: a "My orders" page
- Admin: deactivate a user or a service

### Later (after the MVP works)

- Real-time chat between buyer and seller
- Notifications and emails
- Service images (file upload)
- Arabic interface (right-to-left)
- Real payments
- Favorites list (US-12)
- Seller profile page (US-13)
- Category filter (US-14)

### Not planned

- Withdrawals, disputes, refunds, multiple currencies

## 5. User stories

Format: As a [role], I want [action], so that [benefit].

| ID | Story | Acceptance criteria |
|---|---|---|
| US-01 | As a visitor, I want to register as a Buyer or a Seller and log in, so that I can use the platform | Email is unique. Password has at least 8 characters. Wrong credentials show an error message |
| US-02 | As a seller, I want to publish a service with a title, description, price, and delivery days, so that buyers can find it | Title is 5 to 80 characters. Price is greater than 0. The service appears in the list after publishing |
| US-03 | As a seller, I want to edit or delete my own services, so that my offers stay accurate | I cannot edit or delete another seller's service |
| US-04 | As a visitor, I want to search, filter, and page through services, so that I can find what I need | 10 services per page. No results shows "No services found" and is not an error |
| US-05 | As a visitor, I want to see a service's details and its reviews, so that I can decide to buy | Shows seller name, price, delivery days, and average rating |
| US-06 | As a buyer, I want to order a service, so that the seller starts working | New order status is Pending. I cannot order my own service. The price is copied into the order |
| US-07 | As a seller, I want to accept an order and mark it as delivered, so that the buyer gets the work | Only the seller of that order can change it. Allowed: Pending → InProgress → Delivered |
| US-08 | As a buyer, I want to confirm the delivery, so that the order is completed | Allowed only when the status is Delivered |
| US-09 | As a buyer or seller, I want to cancel a Pending order, so that I can back out early | Not allowed once the order is InProgress |
| US-10 | As a buyer, I want to review a completed order, so that others know how good the seller is | Rating is 1 to 5. One review per order. Only for Completed orders |
| US-11 | As an admin, I want to deactivate a user or a service, so that I can stop abuse | A deactivated service disappears from the list |

## 6. Extra stories (nice to have)

These are not part of the MVP. We will decide later whether to build them.

- **US-12:** As a buyer, I want to save services to a favorites list, so that I can come back to them later.
  - Acceptance: Only logged-in buyers can add or remove favorites. A service appears at most once in my list. My favorites page shows only my own saved services. A deactivated service disappears from the list.
- **US-13:** As a visitor, I want to open a seller's profile page, so that I can see all their services and ratings before buying.
  - Acceptance: Anyone can open the page without logging in. It shows the seller's name, average rating, and number of completed orders. It lists only that seller's active services. An unknown seller shows a "Seller not found" page.
- **US-14:** As a visitor, I want to filter services by category, so that I only see the type of work I need.
  - Acceptance: Each service belongs to exactly one category (for example Design, Translation, Programming). Choosing a category shows only its services. It works together with search, price filter, and pagination. A category with no services shows "No services found" and is not an error.