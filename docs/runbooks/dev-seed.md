# Dev sample data: seeding a database

`web/scripts/seed-dev.mjs` fills a database with one small, believable Dhaka clinic and shop. It goes
through the **real API**, so validation, the publication checklist, stock movements and audit all run
exactly as they do when someone uses the staff screens. `web/scripts/seed-dev-db.mjs` wraps the whole
job for one named database: create, migrate, bootstrap, start a private API, seed, verify, stop.

Both scripts are plain Node 20+ with no npm packages. Running them twice changes nothing: every item is
looked up before it is created.

## What it creates

| Area | Seeded |
|---|---|
| Staff (password `SEED_STAFF_PASSWORD`) | `e2e.admin@purrcare.test` Administrator (Farhana Akter), `e2e.approver@purrcare.test` Administrator (Shirin Sultana), `e2e.nurse@purrcare.test` Nurse, `e2e.reception@purrcare.test` Reception, `e2e.inventory@purrcare.test` Inventory. All are assigned to Main clinic as their primary branch. |
| Branch | The migration's **Main clinic** (`…ba1c`), with its address in Dhanmondi, Dhaka 1209, a phone number, an email address, and opening hours of Saturday to Thursday 09:00–20:00 (closed on Friday). It also gets three rooms (Consultation room 1, Treatment room, Theatre) and a sample billing profile. |
| Tax | Class **Standard** (`STANDARD`) at 0%. The migration's `DELIVERY` class gets a 0% rate if it has none. |
| Shop | 12 published products from 6 brands (Whiskas, Royal Canin, Me-O, Purina Friskies, Catsan, Kit Cat), with 20 SKUs priced from ৳150 to ৳3,900. They cover food (dry, wet, kitten, treats), litter, a tray, a toy and a hairball gel. Food products have their country of origin, life stage, protein source, ingredients, guaranteed analysis, feeding guide and storage instructions. The 5 origin countries are set up as featured sourcing origins. There are no images. |
| Stock | Supplier *Paws Distribution Ltd*, then 1 purchase order (issued), then 1 goods receipt of 24 units per SKU at Main clinic › Shop floor. Expiry-tracked SKUs get lot `SEED-2610-nn`, expiring 2027-09-30. Every SKU is sellable. |
| Content | One published **Shop** banner ("Free delivery in Dhaka on orders over ৳2,000") and 3 published FAQs. The **privacy** policy requires acceptance; the **terms** policy does not. Both have version 1 published. |
| Delivery | Zone **Dhaka** covering the whole Dhaka district, with cash on delivery allowed. Its Standard service costs ৳60, is free over ৳2,000, has a lead time of 1 day, and has two windows a day (10–14 and 15–19) from Saturday to Thursday. It also sets up a clearly fake bank-transfer account. |
| Clinic | Services: General consultation ৳800, Vaccination ৳1,200, Dental scaling ৳3,500. Boarding: units `STD-01` (Standard) and `DLX-01` (Deluxe), each with a rate card. |
| Customer (password `SEED_CUSTOMER_PASSWORD`) | **Ayesha Rahman**, `customer@purrcare.test`, mobile `01819123456`. She has the address *Home*, House 12, Road 5, Block C, Mohammadpur, Dhaka 1207, and the cat **Mishti** (female, calico). She has not accepted the privacy policy yet, so the UI asks her once. |

These are **not** seeded: appointments, rosters, visits, orders, invoices, vaccines and drugs, product
images, and any discount codes.

Effective dates: the API refuses tax rates, service prices and boarding rate cards dated in the past,
so they take effect when the seed runs, not on 2026-01-01. SKU prices start 30 seconds before the run.

## Why one row is written directly

Every staff endpoint needs a signed-in Administrator, and the `DevUser` fallback does not help here:
`RequireActionFilter` answers 401 to any caller without a session. So the API cannot create the first
staff account. `seed-dev-db.mjs` inserts that single row (`e2e.admin@purrcare.test`) without a
password, the same way `CreateStaffAccountCommand` would. Everything after that goes through the API,
including that admin's password, which is set by the normal reset flow.

## How passwords are set

Every staff account and the customer get their password through the normal reset flow:
`POST /api/staff/auth/password-reset/request` (or `/api/auth/…`), then
`POST /api/auth/password-reset/confirm`. The seeder reads the `#token=` link from the API's stdout.
The API's `DevelopmentChannelSender` writes there when it is started with
`Notifications:Channels:{Email,Sms}:Sender=Development`. The link must be an absolute **https** URL,
otherwise the message is suppressed as `RenderFailed`. That is why the seeding API runs with
`PublicSite:WebBaseUrl` and `Account:WebBaseUrl` set to `https://localhost:3000`. Passwords are never
printed.

## Run it (scratch database)

From the repo root, in Git Bash:

```bash
export PGPASSWORD=...                    # PostgreSQL password (PGHOST/PGPORT/PGUSER default to localhost/5432/postgres)
export SEED_STAFF_PASSWORD=...           # at least 10 characters
export SEED_CUSTOMER_PASSWORD=...        # at least 10 characters
node web/scripts/seed-dev-db.mjs --db catecommerce_seedcheck
```

The wrapper does the following:

1. Creates the database if it is missing. It never drops one.
2. Builds the API in **Release**, because the developer's own API usually holds `bin/Debug` locked.
3. Runs `dotnet ef database update --connection …` against that database.
4. Publishes a private copy of the API to a temp folder and **deletes its
   `appsettings.Development.json`** (that file points at `catecommerce_dev`).
5. Starts the copy on port **5099** with the overrides above, plus raised sign-in, reset and
   registration rate limits.
6. Runs `seed-dev.mjs` and then `seed-dev.mjs --verify`, and finally stops the API.

Options:

- `--port <n>` runs the seeding API on another port. Ports 5005 and 3000 are refused.
- `--skip-build` reuses the last Release build.
- `--no-verify` skips the checks.
- `--keep-log` keeps the API log.

Safety: the wrapper always refuses `catecommerce_test`, and refuses `catecommerce_dev` unless
`--i-mean-dev` is passed.

You can also run the seeder against an API you started yourself. That API must be in Development, use
the Development senders and an https base URL, and have its stdout redirected to a file:

```bash
node web/scripts/seed-dev.mjs --api http://localhost:5099 --api-log /path/to/api-stdout.log
node web/scripts/seed-dev.mjs --api http://localhost:5099 --verify    # read-only checks; empties the customer's cart after
```

`--verify` checks the following:

- Every staff account can sign in, and so can the customer.
- There is exactly one Shop banner, 3 FAQs, and a published terms policy.
- The shop filter brands are exactly the 6 seeded brands, and all 12 products are listed as in stock.
- The customer's only pending policy is `privacy`, and her address and cat exist.
- A two-line delivery quote (Whiskas 1.2 kg ৳1,050 + Catsan 5 L ৳850) gives subtotal 1,900, delivery
  60 and total 1,960, with no excluded lines.
- Raising the Whiskas quantity to 2 gives subtotal 2,950, delivery 0 and total 2,950.

## Rebuild `catecommerce_dev` from scratch

1. Stop the dev API (port 5005) and the web app (port 3000), so nothing holds connections to the
   database.
2. Back up the database:
   `pg_dump -h localhost -U postgres -Fc -f catecommerce_dev-<date>.dump catecommerce_dev`
   (see [backup-and-restore.md](backup-and-restore.md)).
3. Drop it:
   `psql -h localhost -U postgres -d postgres -c "DROP DATABASE catecommerce_dev WITH (FORCE)"`
4. Seed it. The wrapper creates the database again:
   `node web/scripts/seed-dev-db.mjs --db catecommerce_dev --i-mean-dev`
5. Start the dev API and the web app as usual. If you have one, set the e2e env file
   (`web/.env.e2e.local`) to the same `E2E_STAFF_*` identifiers, and set `E2E_STAFF_PASSWORD` to
   `SEED_STAFF_PASSWORD`.

To restore the old data instead, use `pg_restore -d catecommerce_dev <dump>` on an empty database.
