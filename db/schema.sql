-- EPIPHARM rebuild — application schema (SQLite)
--
-- This is a clean, normalized schema for the new app. It is NOT the same
-- shape as db/accounts_seed.sql's source data or the raw DBF import
-- (importer/import_dbf.py -> epipharm.sqlite), which mirrors the legacy
-- files field-for-field for verification. A migration script maps the raw
-- import into these tables; see scripts/migrate_to_app_schema.py (next step).
--
-- Conventions:
--   * Money stored as INTEGER centimes (1 DZD = 100 centimes), never REAL,
--     so totals can't drift from floating point rounding.
--   * Dates stored as TEXT 'YYYY-MM-DD' (ISO 8601), sortable as strings.
--   * Every table keeps a legacy_code/legacy_* column where the old DBF
--     used a short code, so the migration is traceable and reversible.
--   * drug_category mirrors the legacy TYPE_MED flag (DCI.DBF), which the
--     whole app is organized around (deliveries/receipts/orders/dotations
--     are each split by this category — see MENU_MAP.md section 2).

PRAGMA foreign_keys = ON;

-- ---------------------------------------------------------------------
-- Accounts (see scripts/generate_accounts.py — already seeded separately)
-- ---------------------------------------------------------------------
-- CREATE TABLE accounts(...) comes from db/accounts_seed.sql, run first.

-- ---------------------------------------------------------------------
-- Geography (Algeria admin hierarchy used for hospital catchment areas)
-- ---------------------------------------------------------------------
CREATE TABLE countries (
    id INTEGER PRIMARY KEY,
    legacy_code TEXT UNIQUE,
    name TEXT NOT NULL,
    sigle TEXT
);

CREATE TABLE wilayas (
    id INTEGER PRIMARY KEY,
    legacy_code TEXT UNIQUE NOT NULL,
    name TEXT NOT NULL
);

CREATE TABLE geo_codes (
    id INTEGER PRIMARY KEY,
    legacy_code TEXT UNIQUE NOT NULL,
    name TEXT NOT NULL,
    wilaya_id INTEGER REFERENCES wilayas(id)
);

CREATE TABLE sanitary_sectors (
    id INTEGER PRIMARY KEY,
    legacy_code TEXT UNIQUE NOT NULL,
    name TEXT NOT NULL
);

CREATE TABLE centers (
    id INTEGER PRIMARY KEY,
    legacy_code TEXT UNIQUE NOT NULL,
    name TEXT NOT NULL,
    region TEXT,
    geo_code_id INTEGER REFERENCES geo_codes(id),
    address TEXT,
    director TEXT,
    phone TEXT,
    fax TEXT
);

-- ---------------------------------------------------------------------
-- Hospital structure
-- ---------------------------------------------------------------------
CREATE TABLE services (
    id INTEGER PRIMARY KEY,
    legacy_code TEXT UNIQUE NOT NULL,          -- CODE
    legacy_center_code TEXT,                    -- CODE_ET
    sigle TEXT,                                  -- short code used elsewhere as SERVICE
    name TEXT NOT NULL,
    address TEXT,
    beds_civil INTEGER,                          -- NBRE_LITT
    beds_military INTEGER,                       -- NBRE_LITO
    category TEXT,                               -- M/T/C or blank, meaning not fully confirmed
    phone TEXT, fax TEXT
);

CREATE TABLE doctors (
    id INTEGER PRIMARY KEY,
    service_id INTEGER REFERENCES services(id),
    seq_in_service INTEGER,                      -- N_O
    last_name TEXT NOT NULL,
    first_name TEXT NOT NULL,
    hired_on TEXT,                                -- DATE_INST
    nationality_code TEXT,
    left_on TEXT                                  -- "Depart" (left service); NULL = active
);

-- ---------------------------------------------------------------------
-- Suppliers / laboratories
-- ---------------------------------------------------------------------
-- FOURNIS.DBF's own CODE field turned out to be a constant ('213', Algeria's
-- country code) across every one of its 81 rows — not a real identifier.
-- N_O is what's actually unique per row, confirmed against the real data,
-- so that's the legacy key here, not CODE.
CREATE TABLE suppliers (
    id INTEGER PRIMARY KEY,
    legacy_code TEXT UNIQUE NOT NULL,              -- = FOURNIS.N_O
    sigle TEXT,
    name TEXT NOT NULL,
    country_id INTEGER REFERENCES countries(id),
    address TEXT, phone TEXT, fax TEXT, telex TEXT,
    contact TEXT
);

-- LABORAT.DBF's CODE is a country code (shared numbering with PAYS.CODE,
-- confirmed — e.g. '213'=Algerie, '033'=France) and is NOT unique alone;
-- N_O distinguishes labs within a country. The real key is (CODE, N_O).
CREATE TABLE laboratories (
    id INTEGER PRIMARY KEY,
    legacy_code TEXT UNIQUE NOT NULL,              -- = LABORAT.CODE || '-' || LABORAT.N_O
    name TEXT NOT NULL,
    country_id INTEGER REFERENCES countries(id)
);

-- ---------------------------------------------------------------------
-- Drug nomenclature
-- ---------------------------------------------------------------------
CREATE TABLE therapeutic_classes (
    id INTEGER PRIMARY KEY,
    legacy_code TEXT UNIQUE NOT NULL,
    name TEXT NOT NULL
);

CREATE TABLE therapeutic_subclasses (
    id INTEGER PRIMARY KEY,
    class_id INTEGER NOT NULL REFERENCES therapeutic_classes(id),
    legacy_code TEXT UNIQUE NOT NULL,
    name TEXT NOT NULL
);

CREATE TABLE dosage_forms (
    id INTEGER PRIMARY KEY,
    legacy_code TEXT UNIQUE NOT NULL,            -- FORMES.CODE
    name TEXT NOT NULL,
    dosage TEXT,
    unit TEXT,
    sigle TEXT
);

-- drug_category: 'medicament' | 'psychotrope' | 'stupefiant' | 'autre'
-- (legacy TYPE_MED: blank/M, P, S, other — confirm exact mapping against
-- the running app before relying on it for controlled-substance handling)
CREATE TABLE dci (                                -- Denomination Commune Internationale
    id INTEGER PRIMARY KEY,
    legacy_code TEXT UNIQUE NOT NULL,             -- C_T+N_O (5 chars), confirmed unique across
                                                   -- all 1595 source rows and the join key
                                                   -- DOTATION/PRODUITS actually use (S_T is a
                                                   -- sub-variant flag, not part of the key)
    subclass_id INTEGER REFERENCES therapeutic_subclasses(id),
    form_id INTEGER REFERENCES dosage_forms(id),
    name TEXT NOT NULL,
    drug_category TEXT NOT NULL CHECK (drug_category IN
        ('medicament','psychotrope','stupefiant','autre')),
    tableau_b INTEGER NOT NULL DEFAULT 0,          -- 1 if subject to Tableau B (controlled)
    priority TEXT,
    stock_minimum INTEGER,
    stock_safety INTEGER,
    reorder_point INTEGER,
    default_price_centimes INTEGER,
    supply_delay_days INTEGER,
    withdrawn_on TEXT                              -- NULL = active
);

-- PRODUITS.DBF (commercial products). legacy_code is NOT unique on its own:
-- confirmed against the real data that the same drug code can have 2-3 rows
-- (32 such codes in the source), distinguished by legacy_fournis_raw.
-- stock_lots and movements key off the 10-char composite CODE+FOURNIS the
-- legacy app used (confirmed 100% match against stock.code / mvt.code_prod
-- — every single row), so the real unique key here is
-- (legacy_code, legacy_fournis_raw), not legacy_code alone.
--
-- supplier_id is intentionally left UNRESOLVED by the migration script.
-- PRODUITS.FOURNIS is a 5-char field whose first 3 digits match the same
-- numbering PAYS and LABORAT use for countries (e.g. '213'=Algerie), and
-- whose last 2 digits often also matches some SUPPLIERS.legacy_code — but
-- that second match is a false positive: SUPPLIERS.legacy_code (FOURNIS.N_O)
-- covers 01-81, a wide enough range that almost any 2-digit suffix matches
-- *something* by coincidence, not because it's the right row. Whether this
-- field means "distributor" (-> suppliers) or "manufacturer" (-> laborat,
-- using the first 3 + last 2 digits together) could not be determined from
-- the file structure alone. legacy_fournis_raw keeps the original value so
-- this can be resolved later against the running app, instead of guessing
-- wrong for 1,623 products in a system that tracks real medication.
CREATE TABLE products (
    id INTEGER PRIMARY KEY,
    legacy_code TEXT NOT NULL,
    legacy_fournis_raw TEXT NOT NULL,
    dci_id INTEGER REFERENCES dci(id),
    supplier_id INTEGER REFERENCES suppliers(id),   -- left NULL by the migration, see above
    laboratory_id INTEGER REFERENCES laboratories(id), -- also left NULL, same reason
    name TEXT NOT NULL,
    form_id INTEGER REFERENCES dosage_forms(id),
    amm_original_no TEXT, amm_original_date TEXT,  -- original marketing authorization
    amm_algeria_no TEXT, amm_algeria_date TEXT,     -- Algerian marketing authorization
    withdrawn_on TEXT,                              -- NULL = active
    localisation TEXT,
    UNIQUE (legacy_code, legacy_fournis_raw)
);

-- ---------------------------------------------------------------------
-- Stock (one row per product + lot)
-- ---------------------------------------------------------------------
CREATE TABLE stock_lots (
    id INTEGER PRIMARY KEY,
    product_id INTEGER NOT NULL REFERENCES products(id),
    lot_number TEXT NOT NULL,
    expires_on TEXT,
    manufactured_on TEXT,
    quantity INTEGER NOT NULL DEFAULT 0,
    unit_price_centimes INTEGER,
    quantity_theoretical INTEGER,                   -- for inventory reconciliation
    quantity_actual INTEGER,
    in_inventory INTEGER NOT NULL DEFAULT 0,
    UNIQUE (product_id, lot_number)
);
CREATE INDEX idx_stock_lots_expiry ON stock_lots(expires_on);

-- ---------------------------------------------------------------------
-- Movements — kept as one ledger table (legacy MVT.DBF), movement_type
-- distinguishes receipt / delivery / correction / etc. Each row is one
-- line of one operation (an operation groups several lines by operation_no).
-- ---------------------------------------------------------------------
-- IMPORTANT: operation_no + line_no is NOT a usable unique key, despite
-- looking like one. Checked directly against the real MVT.DBF: there are
-- only 103 distinct (operation_no, line_no) pairs across 97,592 real rows
-- (234 even including movement_type in the key) — NUM_OPER is reused on a
-- much smaller scale than its name suggests, not a stable per-transaction
-- id. An earlier version of this schema had UNIQUE(operation_no, line_no)
-- here, which silently dropped 97,489 of 97,592 rows on import (99.9% of
-- the stock movement history) — caught by actually running the migration
-- against the real data and checking the row counts, not assumed safe.
-- Each row's real identity is just "this record from MVT.DBF" — the
-- surrogate id below is the only thing to rely on; do not add a uniqueness
-- constraint on the legacy fields without re-verifying against real data.
CREATE TABLE movements (
    id INTEGER PRIMARY KEY,
    operation_no INTEGER NOT NULL,
    line_no INTEGER NOT NULL,
    movement_type TEXT NOT NULL,                    -- legacy TYPE_MVT code, meaning TBD from running app
    counterparty TEXT,                               -- TIERS: supplier code or service code depending on type
    moved_on TEXT NOT NULL,
    expires_on TEXT,
    document_no TEXT,                                 -- NUM_PIECE
    product_id INTEGER REFERENCES products(id),
    quantity_requested INTEGER,
    quantity_moved INTEGER NOT NULL,
    unit_price_centimes INTEGER,
    service_ref TEXT,
    pharmacist_ref TEXT
);
CREATE INDEX idx_movements_operation ON movements(operation_no, line_no);
CREATE INDEX idx_movements_date ON movements(moved_on);
CREATE INDEX idx_movements_product ON movements(product_id);

-- LIVRAISO.DBF: one row per delivery slip. SERVICE is a bare code with no
-- confirmed (establishment, service) pairing in this table (unlike
-- DOTATION, which carries both halves) — legacy_service_raw keeps the
-- original value; service_id is left NULL by the migration until that's
-- checked against the running app.
CREATE TABLE deliveries (
    id INTEGER PRIMARY KEY,
    operation_no INTEGER NOT NULL,
    slip_no INTEGER NOT NULL,
    delivered_on TEXT NOT NULL,
    legacy_service_raw TEXT,
    service_id INTEGER REFERENCES services(id),
    amount_centimes INTEGER NOT NULL,
    document_no TEXT,
    movement_type TEXT NOT NULL,
    service_ref TEXT,
    pharmacist_ref TEXT
);

-- RECEPT.DBF: one row per supplier receipt slip. TIERS (legacy_tiers_raw)
-- is NOT cleanly a supplier code: checked against the real data and it's a
-- mix of formats (5-digit codes like products' supplier field, bare 1-2
-- digit numbers, and what look like 8-digit establishment/center codes) —
-- only ~9% matched suppliers.legacy_code directly. Left unresolved rather
-- than guessed, same reasoning as products.supplier_id in schema.sql above.
CREATE TABLE receipts (
    id INTEGER PRIMARY KEY,
    operation_no INTEGER NOT NULL,
    slip_no INTEGER NOT NULL,
    received_on TEXT NOT NULL,
    legacy_tiers_raw TEXT,
    supplier_id INTEGER REFERENCES suppliers(id),   -- left NULL by the migration, see above
    amount_centimes INTEGER NOT NULL,
    document_no TEXT,
    movement_type TEXT NOT NULL
);

CREATE TABLE purchase_orders (                       -- COMMANDE.DBF + DETAIL_C.DBF
    id INTEGER PRIMARY KEY,
    order_no INTEGER NOT NULL UNIQUE,
    ordered_on TEXT NOT NULL,
    delivered_flag TEXT,
    delivered_on TEXT,
    supplier_id INTEGER REFERENCES suppliers(id),
    reminder_on TEXT,
    reminder_count INTEGER,
    delivery_delay_days INTEGER,
    cancelled_on TEXT                                -- NULL = active ("Lancee"/not cancelled)
);

CREATE TABLE purchase_order_lines (
    id INTEGER PRIMARY KEY,
    order_id INTEGER NOT NULL REFERENCES purchase_orders(id),
    line_no INTEGER NOT NULL,
    dci_id INTEGER REFERENCES dci(id),
    quantity_proposed INTEGER,
    quantity_ordered INTEGER,
    quantity_delivered INTEGER,
    delivered_on TEXT,
    quantity_remaining INTEGER,
    packaging TEXT,                                   -- CONDITION
    UNIQUE (order_id, line_no)
);

-- ---------------------------------------------------------------------
-- Allocations (dotations) — weekly per-service allowance per DCI,
-- split into the same 4 drug categories as deliveries (see MENU_MAP.md).
-- ---------------------------------------------------------------------
CREATE TABLE allocations (
    id INTEGER PRIMARY KEY,
    center_legacy_code TEXT NOT NULL,
    service_id INTEGER NOT NULL REFERENCES services(id),
    dci_id INTEGER NOT NULL REFERENCES dci(id),
    drug_category TEXT NOT NULL CHECK (drug_category IN
        ('medicament','psychotrope','stupefiant','autre')),
    allocation_qty INTEGER NOT NULL,
    year INTEGER NOT NULL,                            -- ANNEE_LIV (2-digit in legacy; store 4-digit)
    week INTEGER NOT NULL,                             -- WEEK_LIV
    delivered_qty INTEGER NOT NULL DEFAULT 0,
    justified INTEGER NOT NULL DEFAULT 0,               -- blocks edit/delete once set, per legacy rule
    UNIQUE (service_id, dci_id, drug_category, year, week)
);

-- ---------------------------------------------------------------------
-- Settings (legacy PARPHARM.DBF: single live row)
-- ---------------------------------------------------------------------
CREATE TABLE settings (
    id INTEGER PRIMARY KEY CHECK (id = 1),           -- enforce single row
    facility_code TEXT NOT NULL,
    facility_name TEXT NOT NULL,
    authority_name TEXT,                               -- TUTELLE
    current_year INTEGER NOT NULL,
    archived_through_date TEXT,
    next_operation_no INTEGER NOT NULL DEFAULT 1,
    next_delivery_slip_no INTEGER NOT NULL DEFAULT 1,
    next_receipt_slip_no INTEGER NOT NULL DEFAULT 1,
    next_order_no INTEGER NOT NULL DEFAULT 1,
    next_correction_no INTEGER NOT NULL DEFAULT 1,
    next_inventory_no INTEGER NOT NULL DEFAULT 1,
    expiry_warning_days INTEGER                         -- PEREMPTION
);

-- ---------------------------------------------------------------------
-- Year-end archive log (legacy MV/LV/CR/RE20xx.ARC produced one set per year)
-- ---------------------------------------------------------------------
CREATE TABLE year_archives (
    year INTEGER PRIMARY KEY,
    archived_at TEXT NOT NULL DEFAULT (datetime('now')),
    archived_by_account_id INTEGER REFERENCES accounts(id)
);

-- ---------------------------------------------------------------------
-- Audit trail — legacy MOUCHARD.DBF existed but was always empty in the
-- source data (never actually used). The rebuild should actually use this.
-- ---------------------------------------------------------------------
CREATE TABLE audit_log (
    id INTEGER PRIMARY KEY,
    at TEXT NOT NULL DEFAULT (datetime('now')),
    account_id INTEGER REFERENCES accounts(id),
    table_name TEXT NOT NULL,
    row_id INTEGER,
    action TEXT NOT NULL CHECK (action IN ('insert','update','delete')),
    detail TEXT
);
