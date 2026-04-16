# Copilot Instructions

## Project Overview
- Project name: `InventoryManagement`
- App type: ASP.NET Core Razor Pages (prioritize Razor Pages patterns over MVC/Blazor)
- Runtime: `.NET 10`
- Language: `C# 14`
- Data access: `Entity Framework Core` with `SQLite`
- Identity: `ASP.NET Core Identity` with roles and optional Google external login

## Core Architecture
- Entry point and app wiring is in `Program.cs`.
- Database context: `Data/ApplicationDbContext.cs`.
- Identity seeding: `Data/IdentityDataSeeder.cs`.
- Razor Pages folders map to core domains:
  - `Pages/Product`
  - `Pages/Provider`
  - `Pages/PurchaseOrder`
  - `Pages/UserManagement`
  - `Pages/Gemini`
- Identity area overrides/custom pages live under `Areas/Identity/Pages/*`.
- Gemini integrations live in `Services/*Gemini*.cs`.

## Authentication and Authorization
- Uses cookie auth via default Identity setup.
- Optional Google OAuth is enabled when `Authentication:Google:ClientId` and `ClientSecret` are configured.
- On principal validation, Google users are normalized to role rules:
  - Ensure user has `Manager` role.
  - Remove `Staff` role if present.
  - Renew principal if role claims changed.
- Authenticator-app 2FA setup includes QR code provisioning via `Areas/Identity/Pages/Account/Manage/EnableAuthenticator.cshtml` and `EnableAuthenticator.cshtml.cs`.

## Roles and Capacities
- Role constants are defined in `InventoryManagement.Security.Roles`.
- `Manager`:
  - Full access to management pages.
  - Required for create/edit/delete operations on products/providers.
  - Required for creating purchase orders.
  - Required for `Gemini` settings page.
- `Staff`:
  - General authenticated access where allowed.
  - Should not keep `Staff` role when user is identified as Google-managed `Manager`.

## Razor Pages Access Rules (from startup conventions)
- Auth required for folders:
  - `/Product`
  - `/Provider`
  - `/PurchaseOrder`
  - `/UserManagement`
  - `/Gemini`
- Manager-only pages:
  - `/Product/Create`, `/Product/Edit`, `/Product/Delete`
  - `/Provider/Create`, `/Provider/Edit`, `/Provider/Delete`
  - `/PurchaseOrder/Create`
  - `/Gemini/Settings`

## Gemini/AI Integration Standards
- Gemini HTTP clients are registered with base URL:
  - `https://generativelanguage.googleapis.com/v1beta/`
- External AI provider configuration (API keys, model IDs, related settings) must be stored in the database, not in environment variables.
- Keep Gemini prompt defaults and extraction logic in dedicated service files.
- Prefer resilient, explicit error handling for external API failures and malformed responses.

## Feature Summary
- Product management (CRUD with role restrictions).
- Provider management (CRUD with role restrictions).
- Purchase order workflows (creation restricted to managers).
- Purchase orders support status lifecycle:
  - `InProcess`
  - `Completed`
- Completing a purchase order is manual and supports optional inventory application:
  - If confirmed, line quantities are added to product inventory.
  - If declined, status is updated without quantity changes.
- Product and dashboard status surfaces include ordered-state awareness:
  - Products that belong to `InProcess` purchase orders are marked `Ordered`.
  - Dashboard includes `In Progress Orders` metric.
  - Dashboard low-stock table shows `Ordered` when low-stock products are already in an in-process purchase order.
- User management area.
- Gemini features for settings/model catalog/invoice extraction.
- Identity with local accounts and optional Google login.
- Identity 2FA authenticator setup supports QR code generation.

## Database and Startup Standards
- Connection string key: `ConnectionStrings:DefaultConnection` with SQLite fallback `Data Source=inventory.db`.
- Apply migrations automatically on startup.
- Seed identity data at startup using `IdentityDataSeeder`.
- Keep startup behavior deterministic and idempotent.

## Coding Style and Standards
- Follow existing project conventions and naming.
- Make minimal, targeted changes.
- Avoid introducing new dependencies unless necessary.
- Do not move logic across layers without clear need.
- Prefer async APIs where available.

## Function Comment Requirement
- Always add comments to functions including:
  - Purpose
  - Explanation
  - Parameters
  - Expected output
  - Possible errors

## Helpful Working Notes for Contributors
- Start analysis from `Program.cs` for auth, DI, and route conventions.
- For authorization issues, verify both:
  - Razor Page conventions in startup.
  - Role assignment behavior in identity principal validation.
- For purchase-order workflow issues, inspect:
  - `Data/PurchaseOrder.cs`
  - `Pages/PurchaseOrder/Create.cshtml.cs`
  - `Pages/PurchaseOrder/Details.cshtml.cs`
  - `Pages/PurchaseOrder/Index.cshtml(.cs)`
  - `Pages/Product/Index.cshtml(.cs)`
  - `Pages/Index.cshtml(.cs)`
- For 2FA QR setup issues, inspect:
  - `Areas/Identity/Pages/Account/Manage/EnableAuthenticator.cshtml`
  - `Areas/Identity/Pages/Account/Manage/EnableAuthenticator.cshtml.cs`
- For Gemini issues, inspect:
  - `Services/GeminiModelCatalogService.cs`
  - `Services/GeminiInvoiceExtractionService.cs`
  - `Services/GeminiInvoicePromptDefaults.cs`
  - `Pages/Gemini/Settings.cshtml.cs`
- Keep documentation aligned with actual role constants and startup policy changes.