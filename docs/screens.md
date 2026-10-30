# Mini Marketplace: Screens

This file lists the screens of the MVP, with a rough sketch of the main ones.
A sketch shows what is on the screen and in what order. It is not the final design.
Paths follow the React Router style. The frontend is built in Phases 5 to 7.

## 1. Rules for every screen

- A screen that loads data has three states: loading ("Loading..."), empty (a friendly message), and error (a message with a "Try again" button).
- A form shows validation messages next to each field, and disables the submit button while the request is running.
- A 401 response sends the user to the Log in screen. After logging in, the user returns to the page they wanted.
- Buttons are hidden when the user's role cannot use them. This is only for convenience, because the API checks permissions (D16).
- Dangerous actions ask for confirmation first: delete a service, deactivate a user or a service, cancel an order (D19).
- One responsive layout: a single column on phones, wider on desktop (D15).

## 2. Navigation bar (on every screen)

- Always: the logo "Mini Marketplace" (link to Home) and a "Services" link.
- Visitor: "Log in" and "Register".
- Buyer: "My orders", the user's name, "Log out".
- Seller: "My orders", "My services", the user's name, "Log out".
- Admin: "Admin", the user's name, "Log out".

## 3. Screens

### S1: Home (services list)

- Path: /
- Who: Public
- Stories: US-04
- API: GET /api/services
- Has a search box, min and max price, sort, a list of service cards, and pagination.
- A service with no reviews shows "No reviews yet" instead of stars.
- Empty result: "No services found".

Sketch:

    +----------------------------------------------------------
    | Mini Marketplace   Services           [Log in] [Register]
    +----------------------------------------------------------
    | [ Search services...        ]  Min [___]  Max [___]  [Apply]
    | Sort: [ Newest v ]
    |
    |   Professional logo design
    |   by Sara - 4.8 stars (12 reviews)
    |   3 days delivery - $25
    |   ---------------------------------------------
    |   Article translation (up to 500 words)
    |   by Omar - 4.5 stars (8 reviews)
    |   2 days delivery - $10
    |   ---------------------------------------------
    |   (more cards...)
    |
    |   [< Prev]     Page 1 of 3     [Next >]
    +----------------------------------------------------------

### S2: Service details

- Path: /services/:id
- Who: Public
- Stories: US-05, US-06
- API: GET /api/services/{id}, GET /api/services/{id}/reviews, POST /api/orders
- The "Order now" button: a Buyer sees it. A visitor sees "Log in to order". A seller or an admin sees no button.
- After a successful order, go to the order details screen (S6) of the new order.
- Unknown or inactive service: "Service not found".

Sketch:

    +----------------------------------------------------------
    | (navigation bar)
    +----------------------------------------------------------
    | < Back to services
    |
    | Professional logo design
    | by Sara - 4.8 stars (12 reviews)
    |
    | $25            Delivery: 3 days
    | [ Order now ]      (visitor sees: Log in to order)
    |
    | Description
    | Text of the description...
    |
    | Reviews
    |   5 stars - "Great work!" - Ali, 2 Oct 2026
    |   4 stars - "Good, a bit late." - Maya, 20 Sep 2026
    |   [ Load more ]
    +----------------------------------------------------------

### S3: Register

- Path: /register
- Who: Public
- Stories: US-01
- API: POST /api/auth/register
- Fields: display name, email, password (at least 8 characters), and a choice: "Buy services" (Buyer) or "Sell services" (Seller).
- Email already used (409): show the message next to the email field.
- Success: go to the Log in screen with the message "Account created. Please log in." The API returns the user, not a token.

### S4: Log in

- Path: /login
- Who: Public
- Stories: US-01
- API: POST /api/auth/login
- Fields: email and password.
- Wrong email or password (401): one general message, "Wrong email or password".
- Deactivated account (403): "This account is deactivated."
- Success: keep the token, then go back to the page the user wanted, or to Home.

### S5: My orders

- Path: /orders
- Who: Buyer or Seller
- Stories: US-06 to US-09 (the list)
- API: GET /api/orders
- A buyer sees the orders they placed. A seller sees the orders of their services.
- Each row: order number, service title, the other person's name, price, status, and date. A filter by status. Clicking a row opens S6.
- Empty: "No orders yet".

### S6: Order details

- Path: /orders/:id
- Who: the buyer or the seller of that order
- Stories: US-07 to US-10
- API: GET /api/orders/{id}, POST /api/orders/{id}/accept, deliver, complete, cancel, and POST /api/orders/{id}/review
- Buttons appear only for what the current user can do now (see the sketch).
- After an action, the screen shows the updated order. A 409 error shows "This order has changed. Refresh the page."

Sketch:

    +----------------------------------------------------------
    | (navigation bar)
    +----------------------------------------------------------
    | < Back to My orders
    |
    | Order #12                          Status: Pending
    |
    | Service: Professional logo design
    | Buyer: Ali        Seller: Sara
    | Price agreed: $25
    | Created: 30 Oct 2026
    |
    | Progress: [Pending] > [InProgress] > [Delivered] > [Completed]
    |
    | Buttons (only what this user can do now):
    |   Seller, Pending:     [ Accept ]  [ Cancel ]
    |   Seller, InProgress:  [ Mark as delivered ]
    |   Buyer, Pending:      [ Cancel ]
    |   Buyer, Delivered:    [ Confirm delivery ]
    |   Buyer, Completed:    review form (rating 1 to 5, comment, [ Send review ])
    |                        or the saved review if one exists
    +----------------------------------------------------------

### S7: My services

- Path: /seller/services
- Who: Seller
- Stories: US-03
- API: GET /api/services/mine, DELETE /api/services/{id}
- Shows only active services (see Q1).
- Each row: title, price, delivery days, "Edit", and "Delete" (with confirmation). A "New service" button on top.
- Empty: "You have no services yet" with a "New service" button.

### S8: Service form

- Paths: /seller/services/new and /seller/services/:id/edit
- Who: Seller
- Stories: US-02, US-03
- API: POST /api/services (new). For edit: GET /api/services/{id} to fill the form, then PUT /api/services/{id}.
- Validation: title 5 to 80 characters, description up to 2000, price greater than 0, delivery days 1 to 60.
- Success: go back to S7.

Sketch:

    +----------------------------------------------------------
    | New service                      (or: Edit service)
    |
    | Title        [ ______________________________ ]  5 to 80 characters
    | Description  [ ______________________________ ]  up to 2000
    |              [ ______________________________ ]
    | Price ($)    [ ______ ]                           greater than 0
    | Delivery     [ ___ ] days                         1 to 60
    |
    |              [ Save ]   [ Cancel ]
    +----------------------------------------------------------

### S9: Admin

- Path: /admin
- Who: Admin
- Stories: US-11
- API: the /api/admin endpoints
- Two sections, each paginated: Users (email, name, role, status, Deactivate or Activate) and Services (title, seller, status, Deactivate or Activate).
- An admin cannot deactivate themselves, so the button is hidden on their own row.

### S10: Not found

- Path: any other path
- Shows "Page not found" with a link to Home.

## 4. Story coverage

Every MVP story has at least one screen:
- US-01: S3, S4
- US-02: S8
- US-03: S7, S8
- US-04: S1
- US-05: S2
- US-06: S2, S5, S6
- US-07 to US-10: S6
- US-11: S9

## 5. What the screens need from the API

Found while sketching. We update api.md with these when we build the endpoints in Phase 3.

- GET /api/orders/{id} returns the price, the status, the buyer and seller names, and the review (null when there is none). S6 uses the review to decide between the review form and the saved review.
- GET /api/auth/me returns the role. The navigation bar uses it to choose which links to show.

## 6. Decisions

- **D15:** One responsive layout for all sizes: a single column on phones, wider on desktop.
  - Why: one codebase, and many users are on phones.
- **D16:** The UI hides buttons by role, but only the API enforces permissions.
  - Why: anyone can change what runs in their own browser.
- **D17:** The MVP interface is English and left-to-right. Arabic and right-to-left are in the Later list.
  - Why: it keeps the first version small.
- **D18:** Order actions live on the order details screen (S6), not in the list.
  - Why: one place that has all the context.
- **D19:** Dangerous actions ask for confirmation first.
  - Why: it prevents accidents.

## 7. Open questions

- **Q1:** D5 sets IsActive to false both when a seller deletes a service and when an admin deactivates it. Should a seller see the services an admin deactivated, with a badge?
  - MVP answer: no, they simply disappear from My services. If we need the difference, we add a separate flag with a migration.