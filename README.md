# InventoryManagement

## TL;DR

`InventoryManagement` is a modern ASP.NET Core Razor Pages application for teams that want to keep stock visible, purchasing organized, and inventory decisions faster.

### In one minute
- Track products, suppliers, categories, and purchase orders in one place.
- Detect low-stock items per product using an independent `Low Stock Threshold`.
- Mark discontinued products so they stay visible historically without polluting active purchasing.
- See products already tied to in-progress purchase orders as `Ordered`.
- Use a dashboard for quick operational visibility.
- Search products by name, description, supplier, category, UPC, and stock status.
- Navigate large datasets with pagination and sortable columns.
- Review a full audit trail of modifications.
- Manage users and roles.
- Support local sign-in and optional Google SSO.
- Secure accounts with authenticator-based 2FA and QR setup.
- Speed up purchase order creation with an AI-powered visual invoice reader based on Google Gemini.

---

## What this system is

`InventoryManagement` is designed for practical inventory operations rather than flashy catalog generation. It focuses on what inventory teams actually need every day:

- knowing what is in stock,
- knowing what is running low,
- knowing what has already been ordered,
- knowing what should no longer be reordered,
- knowing who changed what,
- and reducing the time it takes to turn a supplier invoice into a usable purchase order.

The result is a clean operational system with strong visibility, low friction, and room to grow into predictive inventory planning.

---

## Core functionality

## 1. Inventory management

The system provides a central product catalog with operational fields that matter in real warehouse and replenishment workflows.

Each product can store:
- `Name`
- `Description`
- `UPC`
- `Provider`
- `Cost`
- `Quantity`
- `Date Last Purchased`
- `Estimated Time to Receive (weeks)`
- `Low Stock Threshold`
- `Discontinued`
- one or more `Categories`

### Why this matters
This is not just a static product list. It is a working inventory register that helps teams make restocking decisions with context.

---

## 2. Products with categories

Products can belong to one or more categories. This gives the catalog more structure and makes browsing and searching far more useful.

### Advantages
- Better organization across departments or product families.
- Easier search and filtering.
- Faster understanding of what kind of item is being managed.
- More scalable than relying only on provider names or text descriptions.

This is especially helpful when different providers sell similar items or when a product logically belongs to multiple operational groups.

---

## 3. Low quantity monitoring

The platform highlights products that fall below their configured `Low Stock Threshold`.

### Why `Low Stock Threshold` is per product instead of global
This is one of the strongest design choices in the system.

A global threshold sounds simple, but it is operationally weak because not every product behaves the same.

Examples:
- A fast-moving consumable may need a threshold of `50`.
- A high-cost device may only need a threshold of `2`.
- A niche part with long lead time may need a higher safety margin.
- A bulky, low-turnover item may not.

### Benefits of independent thresholds
- Better alignment with real demand patterns.
- Fewer false alarms.
- Better capital efficiency because you do not overstock everything equally.
- More accurate replenishment priorities.
- Better future compatibility with forecasting and predictive ordering.

In short: per-product thresholds make the inventory logic smarter, more realistic, and more useful.

---

## 4. Discontinued products

Products can be marked as discontinued.

### Why discontinued products matter
A discontinued item is not deleted from history. Instead, it is intentionally kept in the system so teams can still:
- review old purchase and stock history,
- understand why an item is no longer active,
- avoid accidental reordering,
- maintain cleaner operational records.

### Why discontinued products do not appear in purchase orders
Discontinued products are excluded from purchase-order selection because the system is protecting the purchasing flow from mistakes. If a product is no longer part of the active catalog, it should not be reordered by accident.

### Why discontinued products are not directly parsed into purchase orders by the invoice analyzer
The invoice analyzer only matches extracted items against active products. That is intentional.

If an invoice contains an item that maps to a discontinued product, the system does **not** silently add it as a normal order line. Instead, it surfaces it as an **unmatched extracted item**.

### Why this is a good design
- Prevents accidental repurchasing of retired items.
- Keeps the AI-assisted flow safe and reviewable.
- Still shows the user that the invoice contained something important.
- Makes missing or outdated catalog data visible instead of hiding it.

That means the user still sees the missing product as an extra warning item, but the system avoids treating it as a valid active product automatically.

---

## 5. Purchase order management

Purchase orders are first-class operational records in the system.

Users can:
- create purchase orders,
- choose a provider,
- add product lines manually,
- prefill order lines from an invoice image,
- save orders as `In Process`,
- complete orders later,
- optionally apply received quantities into inventory when the order is completed.

### Status lifecycle
Purchase orders currently support:
- `InProcess`
- `Completed`

### Why this is useful
This separates the act of **ordering** from the act of **receiving**.

That distinction is very important in real inventory operations because ordering stock does not mean the stock is physically available yet.

---

## 6. Tracking of orders and products already on the way

A major strength of the system is that it does not only show current stock. It also shows what is already being replenished.

Products connected to `InProcess` purchase orders are surfaced as `Ordered`.

### Where that helps
- In the product list, items can show as `Ordered`.
- On the dashboard low-stock section, low-stock items already tied to an active purchase order are shown differently.
- Teams can avoid duplicate reorders caused by forgetting that stock is already on the way.

### Operational advantage
This reduces panic ordering and gives a more truthful picture of inventory health.

A product can be low today, but if it is already on an open purchase order, the next action is different than for an item that has no active replenishment in flight.

---

## 7. Dashboard

The dashboard gives a fast operational snapshot of the business state.

It includes key metrics such as:
- total products,
- total providers,
- total purchase orders,
- in-progress purchase orders,
- low-stock item count.

It also provides a low-stock table with sorting support and a visible distinction for products that are already ordered.

### Why this matters
A good inventory dashboard reduces the time needed to answer questions like:
- What needs attention now?
- Are low-stock items already being handled?
- Are purchase orders accumulating?
- How large is the catalog?

This turns the homepage into an operational command center instead of a decorative landing page.

---

## 8. Search module

The product catalog includes a practical search experience for day-to-day use.

### Searchable dimensions
Users can search by:
- product name,
- description,
- supplier/provider,
- category,
- UPC.

### Stock-status filtering
Users can also filter products by stock state:
- `All`
- `Low stock`
- `Ordered`
- `Discontinued`

### Why this is valuable
This makes the product list useful as a working tool, not just a table.

Examples:
- Find all low-stock office items.
- Find items from a specific provider.
- Find discontinued products for cleanup or review.
- Find products already ordered but not yet received.

---

## 9. Pagination and sorting

Large lists become hard to use if everything is dumped into one page. This system avoids that problem.

### Included usability features
- paginated product listings,
- paginated purchase order listings,
- paginated audit listings,
- sortable columns across major tables.

### Benefits
- faster navigation,
- cleaner UI,
- better performance on growing datasets,
- easier review of records during real operational work.

This is one of those details that quietly makes the system feel professional.

---

## 10. Audit system for all modifications

The application records audits for entity changes, including creates, updates, and deletes across the core data model.

Each audit entry stores:
- user id,
- action,
- table name,
- record id,
- timestamp,
- details of the changed fields.

### Why this matters
The audit system gives accountability and traceability.

It helps answer questions like:
- Who changed this product?
- When was this record modified?
- What exactly changed?
- Was something deleted intentionally?

### Business value
- Better governance.
- Easier debugging.
- Better support for operational reviews.
- Stronger trust in the system.

For inventory platforms, this is a major credibility feature.

---

## 11. User management

The application includes user management so access can be handled inside the system.

### Current role model
- `Manager`
- `Staff`

### Current version behavior
In this version, newly registered local users are currently created with the `Manager` role.

That is convenient for demos, testing, and rapid setup, but it is intentionally not the ideal long-term security posture.

### Planned future hardening
A more secure future version should default new users to `Staff`, then allow an existing manager to promote them when appropriate.

That future model would be safer because it follows the principle of least privilege more closely.

---

## 12. Google SSO

The application supports optional Google SSO through ASP.NET Core Identity external login.

### What it does
Users can sign in with their Google account instead of creating a local password-only account.

### Why it is useful
- Faster onboarding.
- Less password friction.
- Familiar sign-in experience.
- Good fit for organizations already using Google accounts.

### Current behavior note
When Google authentication is configured and used, the application can normalize role assignment for Google-authenticated users according to the current role rules.

---

## 13. 2FA with QR code

The system supports authenticator-based two-factor authentication.

### What it does
Users can enable 2FA using an authenticator app such as:
- Microsoft Authenticator
- Google Authenticator
- Authy
- other TOTP-compatible apps

### How to set it up
1. Sign in.
2. Open the account management area.
3. Go to the two-factor authentication section.
4. Choose to enable an authenticator app.
5. Scan the QR code shown by the application.
6. If needed, manually enter the shared key.
7. Enter the verification code from the authenticator app.
8. Save the setup and store the recovery codes safely.

### Why this matters
Password-only security is not enough for administrative systems. 2FA adds a strong extra layer of protection, especially for users with access to purchasing, user management, and audit data.

---

## 14. Visual invoice reader with Google Gemini

This is one of the most important features in the system, and it deserves special attention.

The purchase-order workflow includes an AI-assisted visual invoice reader powered by Google Gemini.

Users can:
- upload an invoice image, or
- capture one from the webcam,
- extract invoice text,
- identify the supplier,
- detect product lines and quantities,
- prefill a purchase order automatically,
- review unmatched items before saving.

### Why this feature is a big deal
This feature directly reduces operational friction.

Instead of retyping invoice lines manually, the system helps transform a real-world supplier document into a structured purchase order draft.

### Advantages
- Reduces manual data-entry time.
- Reduces typing mistakes.
- Reduces quantity transcription errors.
- Speeds up purchase-order creation.
- Helps users work from real invoice evidence.
- Highlights items that do not match the active catalog.
- Keeps a human review step instead of blindly automating everything.

### Built-in sample invoices for testing
The purchase-order page includes quick links to bundled sample files:
- `wwwroot/images/Invoice1.png`
- `wwwroot/images/Invoice2.png`
- `wwwroot/images/Invoice3.png`

These are included so testers can immediately try the invoice extraction flow without needing to find their own documents first.

### Why the unmatched items list is excellent UX
When the AI finds something it cannot safely map, the system does not hide that fact.
Instead, it shows the extracted item as unmatched with a reason.

That is a very strong design decision because it balances automation with operational control.

### Why this feature fits an inventory system so well
Invoice ingestion is a natural inventory workflow. It supports procurement, receiving, and catalog validation directly.

This is practical AI, not decorative AI.

---

## Why I chose invoice analysis instead of AI product-description generation

An AI product-description generator is more aligned with an e-commerce platform, where the goal is to improve customer-facing marketing copy.

This application is not an e-commerce storefront. It is an operational inventory system.

That means the better AI investment is the one that improves internal workflows, accuracy, and speed.

### Why the invoice analyzer is the better choice here
- It supports a real operational bottleneck.
- It saves staff time.
- It reduces manual errors.
- It improves purchasing efficiency.
- It helps translate supplier documents into structured records.
- It fits the daily life of inventory teams.

In short: the invoice analyzer solves an inventory problem, while product-description generation solves a storefront problem.

---

## Google Gemini setup

Gemini settings are stored in the application database so the system can:
- save the API key,
- refresh available models,
- choose a model,
- customize the invoice extraction prompts.

### How to get a Google Gemini API key
1. Go to `https://aistudio.google.com/`.
2. Sign in with your Google account.
3. Open the API key area.
4. Create a new API key.
5. Copy the key.
6. Open the app and go to the `Gemini` settings page.
7. Paste the API key and save it.
8. Refresh models.
9. Select the model you want to use for invoice extraction.

> Google refers to this experience as `Google AI Studio`. Some people casually call it `Google Studio`, but the official product name is `Google AI Studio`.

### How to tweak the prompts
The Gemini settings page stores two editable prompts:
- `Invoice Image To Text Prompt`
- `Invoice Structured Extraction Prompt`

### What each prompt does
- The first prompt controls OCR-style extraction from the image.
- The second prompt tells Gemini how to convert that extracted text into structured JSON with supplier and line items.

### When to tweak prompts
You may want to adjust prompts when:
- invoices have unusual layouts,
- supplier branding confuses extraction,
- quantity columns use special wording,
- you want stricter or broader matching behavior,
- you want better handling of faint or irregular rows.

### Prompt-tuning advice
- Change one thing at a time.
- Keep the required JSON structure explicit.
- Be clear about quantity labels and supplier detection rules.
- Test against several real invoices before keeping a new version.
- Avoid turning the prompt into something too vague or overly creative.

The prompt architecture is especially useful because it lets the system evolve without changing core application code every time invoice formats vary.

---

## Estimated Time to Receive (weeks)

Each product can store an `Estimated Time to Receive (weeks)` value.

### Current value
Even today, this field is useful because it gives buyers and managers an immediate sense of supplier lead time.

### Future value for machine learning or predictive algorithms
This field could become very important in a future forecasting engine.

A predictive model could combine:
- stock levels,
- low-stock thresholds,
- purchase frequency,
- seasonality,
- historical purchase-order timing,
- supplier lead times,
- and demand trends

to recommend when a product should be reordered **before** it runs out.

### Important realism note
This kind of prediction would need a meaningful amount of history.

A serious approach would usually require at least:
- several months of data,
- and ideally a year or more,
- sometimes multiple years for seasonal businesses.

Without enough history, predictions would be weak and unstable.

So the field is already operationally useful now, and it is also a smart foundation for future intelligent replenishment.

---

## Running the project

## Prerequisites
- `.NET 10 SDK`
- a local machine capable of running ASP.NET Core

## Database
The app uses `SQLite`.

By default, the connection string falls back to:
- `Data Source=inventory.db`

### Startup behavior
On startup, the application automatically:
- applies pending EF Core migrations,
- creates or updates the SQLite database,
- seeds roles,
- seeds baseline suppliers, categories, and products when the database is empty.

That means first-run setup is intentionally simple.

## Run steps
1. Create `appsettings.json` from `appsettings.Development.json`.
   - Example (PowerShell): `Copy-Item appsettings.Development.json appsettings.json`
2. Restore dependencies:
   - `dotnet restore`
3. Run the application:
   - `dotnet run`
4. Open the local URL shown in the console.
5. Register a user or sign in.

If the database is empty, initial sample data is seeded automatically on first run.

### Testing the invoice extraction quickly
To test the Gemini invoice workflow right away:
1. Open `Create Purchase Order`.
2. Use one of the bundled sample invoice links next to the file picker.
3. Open the image and save it locally if your browser needs a local copy for upload.
4. Upload it and run `Extract and Prefill Order`.

The sample files are:
- `wwwroot/images/Invoice1.png`
- `wwwroot/images/Invoice2.png`
- `wwwroot/images/Invoice3.png`

---

## Google SSO setup

The application can support Google external login when configured.

### What you need from Google
You need a Google OAuth client configured in Google Cloud.

### How to create the Google OAuth credentials
1. Go to `https://console.cloud.google.com/`.
2. Sign in with your Google account.
3. Create or select a Google Cloud project.
4. Go to `APIs & Services`.
5. Configure the OAuth consent screen if you have not done so already.
6. Create `OAuth client ID` credentials.
7. Choose `Web application` as the application type.
8. Add the correct authorized redirect URI for your app.

For local development, a common callback is:
- `https://localhost:{port}/signin-google`

The exact port must match your local app URL.

### Values to copy from Google
After creating the OAuth client, copy:
- `Client ID`
- `Client Secret`

### Where to put them in the app
Add them to configuration under:
- `Authentication:Google:ClientId`
- `Authentication:Google:ClientSecret`

You can also optionally set:
- `Authentication:Google:CallbackPath`

If no callback path is provided, the app uses:
- `/signin-google`

### Example configuration structure
- `Authentication`
  - `Google`
    - `ClientId`
    - `ClientSecret`
    - `CallbackPath` (optional)

### Testing Google SSO
1. Configure the Google values.
2. Run the application.
3. Open the sign-in page.
4. Choose the Google login option.
5. Complete the Google sign-in flow.

---

## Why the system feels strong as a product

This project combines several good design decisions:
- practical inventory fields,
- purchase-order lifecycle awareness,
- low-stock intelligence,
- discontinued-product safety,
- AI-assisted invoice ingestion,
- auditability,
- user security,
- optional Google SSO,
- pagination and sorting for scale.

That mix makes it feel more complete than a simple CRUD app.

It is not just storing records. It is helping users operate.

---

## Final pitch

`InventoryManagement` is built to be useful where it counts:
- less manual work,
- fewer purchasing mistakes,
- clearer stock visibility,
- safer change tracking,
- and a smoother path from supplier invoice to actionable purchase order.

The standout feature is the visual invoice reader. It turns AI into a productivity tool instead of a gimmick, and that gives the platform a real edge.

If the next iterations add stronger role hardening, richer analytics, predictive restocking, and richer identity controls, this foundation can grow from a solid inventory tool into a genuinely smart operations platform.