#!/usr/bin/env python3
"""Migrate the raw 1:1 DBF import (importer/import_dbf.py output) into the
clean, normalized schema (db/schema.sql).

Every join key used below was checked against the real extracted data
before being written here, not assumed from field names:
  - dci.legacy_code       = DCI.C_T + DCI.N_O (5 chars). Confirmed unique
                             across all 1595 source DCI rows.
  - products identity     = PRODUITS.CODE is NOT unique alone (32 codes
                             have 2-3 supplier rows in the source); the real
                             key is (CODE, FOURNIS).
  - stock.code /
    mvt.code_prod         = a 10-char composite: PRODUITS.CODE (5) +
                             PRODUITS.FOURNIS (5). Confirmed 100% match
                             (1063/1063 stock rows, 97592/97592 mvt rows)
                             against that composite, and 0% against
                             PRODUITS.CODE alone.
  - services.code_et      = CENTRES.CODE (establishment)
  - services.code         = SERV_NAT.CODE (service type/name), confirmed
                             84/84 match
  - centres.code_geo      = CODE_GEO.CODE_GEO
  - geo_codes -> wilaya    INFERRED, not confirmed: first 2 digits of
                             CODE_GEO look like the wilaya code (standard
                             Algerian admin numbering), but there is no
                             explicit column linking them in the DBF. Treat
                             wilaya_id on geo_codes as a best guess.
  - dotation.code_dci     = dci.legacy_code, confirmed 5421/5421 match
  - dotation.code_et/
    code_serv              -> services, via (code_et, code) composite
  - products.laboratory_id = PRODUITS.FOURNIS (5 chars) decodes as
                             LABORAT.CODE (3, country) + LABORAT.N_O (2):
                             confirmed against a real screenshot of the
                             running app (Nomenclature Commerciale showing
                             "FRANCE ( FR )" for FOURNIS='03303', which is
                             exactly LABORAT code='033'/n_o='03' = FRANCE),
                             then checked at scale: 1622/1622 (100%) of all
                             5-char FOURNIS values decode to a real LABORAT
                             row. Supersedes the earlier "ambiguous,
                             unresolved" note in schema.sql — this is the
                             manufacturer/laboratory link, not a supplier.
  - receipts.supplier_id  = RECEPT.TIERS, when it's a 5-char code starting
                             with '213' (Algeria), decodes as that + a
                             supplier N_O the same way: confirmed against a
                             real screenshot of "Reception de Produits"
                             showing Code Fournisseur 21312 resolving to
                             "S.A.R.L FARABI M.S", which is exactly
                             FOURNIS code='213'/n_o='12' in the real data.
                             At scale: 2344/2410 (97%) of 5-char TIERS
                             values with a '213' prefix match a real
                             FOURNIS.N_O. The remaining TIERS formats (1-2
                             digit, 7-digit, 8-digit — see RECEPT.DBF note
                             in schema.sql) are a different thing entirely
                             (establishment/center codes, going by the
                             Transfert Inter-Etablissement screen using the
                             same 8-digit shape) and are NOT resolved here.

What this script does NOT yet handle (left for a follow-up pass once the
core data model is validated against the running app):
  - purchase_orders / purchase_order_lines (COMMANDE.DBF was empty in the
    source; DETAIL_C.DBF has 405 rows but order linkage needs the same
    kind of verification done above before trusting it)
  - memo text (HELP.DBT — table was empty in the source anyway)
  - the 2018-2020 yearly archives (MV/LV/CR/RE20xx — same shape as the
    live tables, can reuse this script's logic once this core pass is
    confirmed correct)

Usage: python migrate_to_app_schema.py <raw_import.sqlite> <schema.sql> <accounts_seed.sql> <output.sqlite>
"""
import sqlite3
import sys


def ddmmyyyy_ok(s):
    return s  # dates already normalized to YYYY-MM-DD by the raw importer


def centimes(value):
    if value is None:
        return None
    return int(round(float(value) * 100))


def main():
    if len(sys.argv) != 5:
        sys.exit(__doc__)
    raw_path, schema_path, accounts_path, out_path = sys.argv[1:5]

    raw = sqlite3.connect(raw_path)
    raw.row_factory = sqlite3.Row
    out = sqlite3.connect(out_path)
    out.executescript(open(accounts_path).read())
    out.executescript(open(schema_path).read())

    counts = {}

    def report(name, n):
        counts[name] = n
        print(f'{name:28s} {n}')

    # ---- reference tables -------------------------------------------------
    out.executemany(
        "INSERT INTO countries(legacy_code, name, sigle) VALUES (?,?,?)",
        [(r['code'], r['libelle'].strip(), r['sigle'].strip())
         for r in raw.execute("SELECT code, libelle, sigle FROM pays WHERE _deleted=0")])
    report('countries', out.execute("SELECT count(*) FROM countries").fetchone()[0])

    out.executemany(
        "INSERT INTO wilayas(legacy_code, name) VALUES (?,?)",
        [(r['code'], r['libelle'].strip())
         for r in raw.execute("SELECT code, libelle FROM wilaya WHERE _deleted=0")])
    report('wilayas', out.execute("SELECT count(*) FROM wilayas").fetchone()[0])
    wilaya_ids = {r[0]: r[1] for r in out.execute("SELECT legacy_code, id FROM wilayas")}

    geo_rows = []
    unmatched_wilaya = 0
    for r in raw.execute("SELECT code_geo, libelle FROM code_geo WHERE _deleted=0"):
        wcode = r['code_geo'][:2]
        wid = wilaya_ids.get(wcode)
        if wid is None:
            unmatched_wilaya += 1
        geo_rows.append((r['code_geo'], r['libelle'].strip(), wid))
    out.executemany("INSERT INTO geo_codes(legacy_code, name, wilaya_id) VALUES (?,?,?)", geo_rows)
    report('geo_codes', out.execute("SELECT count(*) FROM geo_codes").fetchone()[0])
    if unmatched_wilaya:
        print(f'  NOTE: {unmatched_wilaya} geo_codes did not match a wilaya by prefix (inferred join, not confirmed)')
    geo_ids = {r[0]: r[1] for r in out.execute("SELECT legacy_code, id FROM geo_codes")}

    out.executemany(
        "INSERT INTO sanitary_sectors(legacy_code, name) VALUES (?,?)",
        [(r['code'], r['libelle'].strip())
         for r in raw.execute("SELECT code, libelle FROM secteurs WHERE _deleted=0")])
    report('sanitary_sectors', out.execute("SELECT count(*) FROM sanitary_sectors").fetchone()[0])

    center_rows, seen_center_codes, dup_centers, unmatched_geo = [], set(), 0, 0
    for r in raw.execute("SELECT code, libelle, region, code_geo, adresse, directeur, telephone, fax "
                          "FROM centres WHERE _deleted=0 ORDER BY _rowno"):
        if r['code'] in seen_center_codes:
            # Real data quality issue, not a join bug: the source genuinely has
            # duplicate center codes with slightly different names/addresses
            # (e.g. 23121010 appears as both "...DE BAKARIA" and "...DE TEBESSA").
            # Keep the first (lowest _rowno) occurrence, drop the rest, and say so.
            dup_centers += 1
            continue
        seen_center_codes.add(r['code'])
        gid = geo_ids.get(r['code_geo'])
        if gid is None:
            unmatched_geo += 1
        center_rows.append((r['code'], r['libelle'].strip(), r['region'], gid,
                             r['adresse'].strip(), r['directeur'].strip(), r['telephone'].strip(), r['fax'].strip()))
    out.executemany(
        "INSERT INTO centers(legacy_code, name, region, geo_code_id, address, director, phone, fax) "
        "VALUES (?,?,?,?,?,?,?,?)", center_rows)
    report('centers', out.execute("SELECT count(*) FROM centers").fetchone()[0])
    if unmatched_geo:
        print(f'  NOTE: {unmatched_geo} centers did not match a geo_code (check before trusting addresses)')
    if dup_centers:
        print(f'  NOTE: {dup_centers} duplicate center codes in the source were dropped, keeping the first row of each '
              f'(real source data issue, confirm the kept names/addresses are the right ones)')
    center_ids = {r[0]: r[1] for r in out.execute("SELECT legacy_code, id FROM centers")}

    # ---- hospital structure -------------------------------------------------
    servnat = {r['code']: r['libelle'].strip() for r in raw.execute("SELECT code, libelle FROM serv_nat")}
    service_rows, seen_service_keys, dup_services = [], set(), 0
    unmatched_center = unmatched_name = 0
    for r in raw.execute("SELECT code_et, code, adresse, nbre_litt, nbre_lito, categorie, telephone, fax "
                          "FROM services WHERE _deleted=0 ORDER BY _rowno"):
        key = (r['code_et'], r['code'])
        if key in seen_service_keys:
            # Same kind of source duplication as centers above — keep the first row, drop the rest.
            dup_services += 1
            continue
        seen_service_keys.add(key)
        name = servnat.get(r['code'])
        if name is None:
            unmatched_name += 1
            name = f"(service {r['code']} - nom non trouve)"
        if r['code_et'] not in center_ids:
            unmatched_center += 1
        service_rows.append((r['code_et'], r['code'], name, r['adresse'].strip(),
                              r['nbre_litt'], r['nbre_lito'], r['categorie'], r['telephone'].strip(), r['fax'].strip()))
    # legacy_center_code (CODE_ET) is a real column in db/schema.sql but was
    # never actually in this INSERT's column list — left NULL for every one
    # of the 81 rows despite the schema comment promising it. Found while
    # building the delivery-entry screen, which needs to filter services by
    # establishment (confirmed from a real screenshot that deliveries in
    # this data belong to code_et='23124010', not '25120305' — see
    # DeliveryEntryRepository). Fixed here rather than worked around in the
    # app layer, since the column already exists for exactly this.
    out.executemany(
        "INSERT INTO services(legacy_code, legacy_center_code, sigle, name, address, beds_civil, beds_military, "
        "category, phone, fax) VALUES (?,?,?,?,?,?,?,?,?,?)",
        [(f"{et}:{code}", et, code, name, addr, bc, bm, cat, ph, fax)
         for et, code, name, addr, bc, bm, cat, ph, fax in service_rows])
    report('services', out.execute("SELECT count(*) FROM services").fetchone()[0])
    if unmatched_name:
        print(f'  NOTE: {unmatched_name} services had no matching serv_nat name')
    if unmatched_center:
        print(f'  NOTE: {unmatched_center} services referenced an unknown establishment code')
    if dup_services:
        print(f'  NOTE: {dup_services} duplicate (establishment, service) pairs in the source were dropped')
    # service lookup keyed by (code_et, code) since legacy_code alone repeats across establishments
    service_ids = {}
    for r in out.execute("SELECT legacy_code, id FROM services"):
        et, code = r[0].split(':', 1)
        service_ids[(et, code)] = r[1]

    doctor_rows = []
    for r in raw.execute("SELECT code_serv, code_et, n_o, nom, prenom, date_inst, code_nat FROM medecins WHERE _deleted=0"):
        sid = service_ids.get((r['code_et'], r['code_serv']))
        doctor_rows.append((sid, r['n_o'], r['nom'].strip(), r['prenom'].strip(), r['date_inst'], r['code_nat']))
    out.executemany(
        "INSERT INTO doctors(service_id, seq_in_service, last_name, first_name, hired_on, nationality_code) "
        "VALUES (?,?,?,?,?,?)", doctor_rows)
    report('doctors', out.execute("SELECT count(*) FROM doctors").fetchone()[0])

    # ---- suppliers / laboratories -------------------------------------------
    # FOURNIS.CODE turned out to be a constant ('213') across all 81 rows —
    # confirmed against the real data, not a real identifier — so N_O is the
    # legacy key here, and CODE is kept only to resolve country_id.
    country_ids = {r[0]: r[1] for r in out.execute("SELECT legacy_code, id FROM countries")}
    out.executemany(
        "INSERT INTO suppliers(legacy_code, sigle, name, country_id, address, phone, fax, telex, contact) "
        "VALUES (?,?,?,?,?,?,?,?,?)",
        [(r['n_o'], r['sigle'].strip(), r['libelle'].strip(), country_ids.get(r['code']), r['adresse'].strip(),
          r['telephone'].strip(), r['fax'].strip(), r['telex'].strip(), r['interloc'].strip())
         for r in raw.execute("SELECT code, n_o, sigle, libelle, adresse, telephone, fax, telex, interloc "
                               "FROM fournis WHERE _deleted=0")])
    report('suppliers', out.execute("SELECT count(*) FROM suppliers").fetchone()[0])
    # Used below to resolve receipts.supplier_id (confirmed decoding, see
    # the note above this function) — not products, which resolve against
    # laboratories instead (laboratory_ids_by_key, defined after laborat).
    supplier_ids_by_no = {r[0]: r[1] for r in out.execute("SELECT legacy_code, id FROM suppliers")}

    # LABORAT.CODE is a country code (same numbering as PAYS.CODE, confirmed:
    # '213'=Algerie, '033'=France, etc.) and is NOT unique alone; N_O
    # distinguishes labs within a country, so the key here is (code, n_o).
    out.executemany(
        "INSERT INTO laboratories(legacy_code, name, country_id) VALUES (?,?,?)",
        [(f"{r['code']}-{r['n_o']}", r['libelle'].strip(), country_ids.get(r['code']))
         for r in raw.execute("SELECT code, n_o, libelle FROM laborat WHERE _deleted=0")])
    report('laboratories', out.execute("SELECT count(*) FROM laboratories").fetchone()[0])
    laboratory_ids_by_key = {r[0]: r[1] for r in out.execute("SELECT legacy_code, id FROM laboratories")}

    def laboratory_id_for_fournis(raw_fournis):
        """PRODUITS.FOURNIS (5 chars) = LABORAT.CODE (3) + LABORAT.N_O (2).
        See the confirmed-against-a-screenshot note above the function this
        migration runs in. Returns None (not a guess) for anything that
        doesn't cleanly decode, rather than forcing a match."""
        if not raw_fournis or len(raw_fournis) != 5:
            return None
        return laboratory_ids_by_key.get(f"{raw_fournis[:3]}-{raw_fournis[3:]}")

    # ---- nomenclature --------------------------------------------------------
    out.executemany(
        "INSERT INTO therapeutic_classes(legacy_code, name) VALUES (?,?)",
        [(r['code'], r['libelle'].strip()) for r in raw.execute("SELECT code, libelle FROM classe_t WHERE _deleted=0")])
    report('therapeutic_classes', out.execute("SELECT count(*) FROM therapeutic_classes").fetchone()[0])
    class_ids = {r[0]: r[1] for r in out.execute("SELECT legacy_code, id FROM therapeutic_classes")}

    subclass_rows, unmatched_class = [], 0
    for r in raw.execute("SELECT code, scode, libelle FROM classe_s WHERE _deleted=0"):
        cid = class_ids.get(r['code'])
        if cid is None:
            unmatched_class += 1
            continue
        subclass_rows.append((cid, r['code'] + r['scode'], r['libelle'].strip()))
    out.executemany("INSERT INTO therapeutic_subclasses(class_id, legacy_code, name) VALUES (?,?,?)", subclass_rows)
    report('therapeutic_subclasses', out.execute("SELECT count(*) FROM therapeutic_subclasses").fetchone()[0])
    if unmatched_class:
        print(f'  NOTE: {unmatched_class} subclasses referenced an unknown class code')

    # FORMES.CODE has 2 codes ('0001', '0002') that are NOT duplicates of the
    # same thing like centers/services above — each covers a dozen+ wildly
    # unrelated entries (code '0001' alone spans "COMPRIME 10mg" to "ROULEAU
    # 500g" bandage roll to "BOITE 10x10"). Picking any one row to keep is a
    # genuine guess, not a confirmed dedup. Flagged loudly below; this needs
    # real cleanup of the source data, not a migration-script fix.
    form_dup_codes = {r[0] for r in raw.execute(
        "SELECT code FROM formes WHERE _deleted=0 GROUP BY code HAVING count(*)>1")}
    out.executemany(
        "INSERT INTO dosage_forms(legacy_code, name, dosage, unit, sigle) VALUES (?,?,?,?,?)",
        [(r['code'], r['libelle'].strip(), r['dosage'].strip(), r['unite'].strip(), r['sigle'].strip())
         for r in raw.execute("SELECT code, libelle, dosage, unite, sigle FROM formes "
                               "WHERE _deleted=0 GROUP BY code ORDER BY _rowno")])
    report('dosage_forms', out.execute("SELECT count(*) FROM dosage_forms").fetchone()[0])
    if form_dup_codes:
        print(f'  WARNING: form code(s) {sorted(form_dup_codes)} are not reliable identifiers in the source —')
        print(f'           each maps to many unrelated dosage forms there. The name kept for each is an')
        print(f'           arbitrary pick (first row), not a confirmed value. Any DCI/product using one of')
        print(f'           these codes needs its dosage form re-entered by hand, not trusted from this import.')
    form_ids = {r[0]: r[1] for r in out.execute("SELECT legacy_code, id FROM dosage_forms")}

    subclass_ids = {r[0]: r[1] for r in out.execute("SELECT legacy_code, id FROM therapeutic_subclasses")}
    category_map = {'M': 'medicament', 'P': 'psychotrope', 'S': 'stupefiant', '': 'autre', ' ': 'autre'}
    dci_rows, unmapped_cat = [], 0
    for r in raw.execute("SELECT c_t, n_o, s_t, forme, libelle, tableau, type_med, priorite, "
                          "stock_mini, stock_secu, point_comm, prix_def, delai_appr, d_rupture FROM dci WHERE _deleted=0"):
        legacy_code = r['c_t'] + r['n_o']
        cat = category_map.get((r['type_med'] or '').strip())
        if cat is None:
            unmapped_cat += 1
            cat = 'autre'
        dci_rows.append((
            legacy_code, subclass_ids.get(r['c_t'] + r['s_t']), form_ids.get(r['forme']),
            r['libelle'].strip(), cat, 1 if (r['tableau'] or '').strip() else 0,
            r['priorite'], r['stock_mini'], r['stock_secu'], r['point_comm'],
            centimes(r['prix_def']), r['delai_appr'], r['d_rupture']))
    # D_RUPTURE -> projected_rupture_on, not "withdrawn_on": see the long
    # comment on this column in db/schema.sql. An earlier version of this
    # migration called it withdrawn_on and DotationEntryRepository filtered
    # on it as "is active", which silently hid 184 of 270 medicament DCI
    # (68%) from the dotation screen — found by checking real counts, not
    # by inspection.
    out.executemany(
        "INSERT INTO dci(legacy_code, subclass_id, form_id, name, drug_category, tableau_b, priority, "
        "stock_minimum, stock_safety, reorder_point, default_price_centimes, supply_delay_days, projected_rupture_on) "
        "VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?)", dci_rows)
    report('dci', out.execute("SELECT count(*) FROM dci").fetchone()[0])
    if unmapped_cat:
        print(f'  NOTE: {unmapped_cat} dci rows had an unrecognized TYPE_MED value, defaulted to autre')
    dci_ids = {r[0]: r[1] for r in out.execute("SELECT legacy_code, id FROM dci")}

    # supplier_id is still left NULL here — PRODUITS.FOURNIS turned out to
    # decode against LABORAT (laboratory_id below), not FOURNIS (suppliers);
    # see the confirmed note above. legacy_fournis_raw keeps the original
    # value regardless, so this stays checkable against the raw import.
    product_rows, unmatched_dci, unmatched_lab = [], 0, 0
    for r in raw.execute("SELECT code, fournis, libelle, forme, amm_origin, d_amm_ori, amm_algeri, d_amm_alg, "
                          "retire, date_ret, localisat FROM produits WHERE _deleted=0"):
        dci_id = dci_ids.get(r['code'])
        if dci_id is None:
            unmatched_dci += 1
        lab_id = laboratory_id_for_fournis(r['fournis'])
        if lab_id is None:
            unmatched_lab += 1
        product_rows.append((
            r['code'], r['fournis'], dci_id, lab_id, r['libelle'].strip(), form_ids.get(r['forme']),
            r['amm_origin'], r['d_amm_ori'], r['amm_algeri'], r['d_amm_alg'],
            r['date_ret'] if (r['retire'] or '').strip() else None, r['localisat']))
    out.executemany(
        "INSERT INTO products(legacy_code, legacy_fournis_raw, dci_id, laboratory_id, name, form_id, "
        "amm_original_no, amm_original_date, amm_algeria_no, amm_algeria_date, withdrawn_on, localisation) "
        "VALUES (?,?,?,?,?,?,?,?,?,?,?,?)", product_rows)
    report('products', out.execute("SELECT count(*) FROM products").fetchone()[0])
    if unmatched_dci:
        print(f'  NOTE: {unmatched_dci} products did not match a dci row')
    if unmatched_lab:
        print(f'  NOTE: {unmatched_lab} products did not resolve a laboratory_id from FOURNIS (kept as legacy_fournis_raw only)')
    print('  NOTE: products.supplier_id still left NULL for every row — FOURNIS decodes as a')
    print('        laboratory (see laboratory_id above), not a supplier; nothing in the source data')
    print('        links a product directly to the supplier(s) who deliver it.')
    # product lookup keyed by the legacy composite (code, fournis) — this is
    # STILL valid and confirmed 100%, it's self-referential to products' own
    # two fields and doesn't depend on resolving what fournis itself means.
    product_ids = {(r[0], r[1]): r[2] for r in out.execute(
        "SELECT legacy_code, legacy_fournis_raw, id FROM products")}

    def product_id_for_composite(code10):
        return product_ids.get((code10[:5], code10[5:]))

    # ---- stock ----------------------------------------------------------------
    stock_rows, unmatched_stock_product = [], 0
    for r in raw.execute("SELECT code, num_lot, date_perem, date_fabri, qte_stock, prix_unit, "
                          "qte_theori, qte_reelle, inventaire FROM stock WHERE _deleted=0"):
        pid = product_id_for_composite(r['code'])
        if pid is None:
            unmatched_stock_product += 1
            continue
        stock_rows.append((pid, r['num_lot'].strip(), r['date_perem'], r['date_fabri'], r['qte_stock'] or 0,
                            centimes(r['prix_unit']),
                            int(r['qte_theori']) if r['qte_theori'] and r['qte_theori'].strip().lstrip('-').isdigit() else None,
                            int(r['qte_reelle']) if r['qte_reelle'] and r['qte_reelle'].strip().lstrip('-').isdigit() else None,
                            1 if (r['inventaire'] or '').strip() else 0))
    out.executemany(
        "INSERT OR IGNORE INTO stock_lots(product_id, lot_number, expires_on, manufactured_on, quantity, "
        "unit_price_centimes, quantity_theoretical, quantity_actual, in_inventory) VALUES (?,?,?,?,?,?,?,?,?)",
        stock_rows)
    inserted = out.execute("SELECT count(*) FROM stock_lots").fetchone()[0]
    report('stock_lots', inserted)
    if unmatched_stock_product:
        print(f'  NOTE: {unmatched_stock_product} stock rows had no matching product (unexpected — investigate before trusting stock levels)')
    dropped_dupe_lots = len(stock_rows) - inserted
    if dropped_dupe_lots:
        print(f'  NOTE: {dropped_dupe_lots} stock rows were dropped as duplicate (product, lot) pairs — real')
        print(f'        duplicate entries confirmed in the source (not a migration bug); the kept row for')
        print(f'        each is the first one encountered, not necessarily the most recent/correct one.')

    # ---- movements --------------------------------------------------------------
    mvt_rows, unmatched_mvt_product = [], 0
    for r in raw.execute("SELECT num_oper, type_mvt, num_ligne, tiers, date_mvt, date_perem, num_piece, "
                          "code_prod, qte_dem, qte_mvt, prix_unit, ref_serv, ref_pharm FROM mvt WHERE _deleted=0"):
        pid = product_id_for_composite(r['code_prod'])
        if pid is None:
            unmatched_mvt_product += 1
        mvt_rows.append((r['num_oper'], r['num_ligne'], r['type_mvt'], r['tiers'], r['date_mvt'], r['date_perem'],
                          r['num_piece'], pid, r['qte_dem'], r['qte_mvt'] or 0, centimes(r['prix_unit']),
                          r['ref_serv'], r['ref_pharm']))
    out.executemany(
        "INSERT INTO movements(operation_no, line_no, movement_type, counterparty, moved_on, expires_on, "
        "document_no, product_id, quantity_requested, quantity_moved, unit_price_centimes, service_ref, pharmacist_ref) "
        "VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?)", mvt_rows)
    inserted = out.execute("SELECT count(*) FROM movements").fetchone()[0]
    report('movements', inserted)
    assert inserted == len(mvt_rows), (
        f'movements: inserted {inserted} but attempted {len(mvt_rows)} — something is silently '
        f'dropping rows again, stop and investigate before trusting this data')
    if unmatched_mvt_product:
        print(f'  NOTE: {unmatched_mvt_product} movement rows had no matching product')

    # ---- deliveries / receipts ---------------------------------------------------
    out.executemany(
        "INSERT INTO deliveries(operation_no, slip_no, delivered_on, legacy_service_raw, service_id, "
        "amount_centimes, document_no, movement_type, service_ref, pharmacist_ref) "
        "VALUES (?,?,?,?,?,?,?,?,?,?)",
        [(r['num_oper'], r['num_bon'], r['date_liv'], r['service'], None, centimes(r['montant']), r['num_piece'],
          r['type_mvt'], r['ref_serv'], r['ref_pharm'])
         for r in raw.execute("SELECT num_oper, num_bon, date_liv, service, montant, num_piece, type_mvt, "
                               "ref_serv, ref_pharm FROM livraiso WHERE _deleted=0")])
    report('deliveries', out.execute("SELECT count(*) FROM deliveries").fetchone()[0])
    print('  NOTE: deliveries.service_id left NULL (legacy_service_raw kept) — see schema.sql note.')

    def supplier_id_for_tiers(raw_tiers):
        """RECEPT.TIERS, when 5 chars starting with '213' (Algeria), decodes
        as FOURNIS.CODE (3) + FOURNIS.N_O (2) — same scheme as
        laboratory_id_for_fournis above, confirmed against a real
        screenshot (see the note above this function runs in). Other TIERS
        shapes (1-2 digit, 7-digit, 8-digit) aren't this and are left
        unresolved, not guessed at."""
        if not raw_tiers or len(raw_tiers) != 5 or raw_tiers[:3] != '213':
            return None
        return supplier_ids_by_no.get(raw_tiers[3:])

    receipt_rows, unmatched_supplier = [], 0
    for r in raw.execute("SELECT num_oper, num_bon, date_rec, tiers, montant, num_piece, type_mvt FROM recept WHERE _deleted=0"):
        supplier_id = supplier_id_for_tiers(r['tiers'])
        if supplier_id is None:
            unmatched_supplier += 1
        receipt_rows.append((r['num_oper'], r['num_bon'], r['date_rec'], r['tiers'], supplier_id,
                              centimes(r['montant']), r['num_piece'], r['type_mvt']))
    out.executemany(
        "INSERT INTO receipts(operation_no, slip_no, received_on, legacy_tiers_raw, supplier_id, "
        "amount_centimes, document_no, movement_type) VALUES (?,?,?,?,?,?,?,?)", receipt_rows)
    report('receipts', out.execute("SELECT count(*) FROM receipts").fetchone()[0])
    print(f'  NOTE: receipts.supplier_id resolved for {len(receipt_rows) - unmatched_supplier}/{len(receipt_rows)} rows')
    print('        (the "213"+N_O supplier code shape); the rest (other TIERS shapes — see')
    print('        schema.sql) are left unresolved in legacy_tiers_raw, not guessed at.')

    # ---- allocations (dotations) ---------------------------------------------------
    dot_rows, unmatched_dot_service, unmatched_dot_dci = [], 0, 0
    for r in raw.execute("SELECT code_et, code_serv, code_dci, type_med, dotation, annee_liv, week_liv, qte_liv "
                          "FROM dotation WHERE _deleted=0"):
        sid = service_ids.get((r['code_et'], r['code_serv']))
        did = dci_ids.get(r['code_dci'])
        if sid is None:
            unmatched_dot_service += 1
        if did is None:
            unmatched_dot_dci += 1
        if sid is None or did is None:
            continue
        cat = category_map.get((r['type_med'] or '').strip(), 'autre')
        year2 = r['annee_liv']
        year = 2000 + year2 if year2 is not None and year2 < 70 else (1900 + year2 if year2 is not None else None)
        dot_rows.append((r['code_et'], sid, did, cat, r['dotation'] or 0, year, r['week_liv'], r['qte_liv'] or 0))
    out.executemany(
        "INSERT OR IGNORE INTO allocations(center_legacy_code, service_id, dci_id, drug_category, allocation_qty, "
        "year, week, delivered_qty) VALUES (?,?,?,?,?,?,?,?)", dot_rows)
    report('allocations', out.execute("SELECT count(*) FROM allocations").fetchone()[0])
    if unmatched_dot_service:
        print(f'  NOTE: {unmatched_dot_service} dotation rows had no matching service')
    if unmatched_dot_dci:
        print(f'  NOTE: {unmatched_dot_dci} dotation rows had no matching dci')

    # ---- settings (single live row — PARPHARM.DBF's row is marked deleted in the source) --
    # Verified directly against the real row (not assumed from field names):
    # UNITE = facility's own code (matches its CENTRES.CODE), TUTELLE = the
    # facility's own name ("EPH DE CHERIA") — NOT an oversight-authority name
    # despite what the field name suggests. The "Ministere de la Sante..."
    # header seen in PHARMA.EXE's strings is printed literally on reports,
    # not read from this table, so authority_name is left NULL here rather
    # than guessed.
    row = raw.execute("SELECT unite, tutelle, annee, date_archi, n_oper_mvt, n_bon_liv, n_bon_rec, "
                       "n_oper_cmd, n_oper_cor, num_inv, peremption FROM parpharm LIMIT 1").fetchone()
    if row:
        out.execute(
            "INSERT INTO settings(id, facility_code, facility_name, authority_name, current_year, "
            "archived_through_date, next_operation_no, next_delivery_slip_no, next_receipt_slip_no, "
            "next_order_no, next_correction_no, next_inventory_no, expiry_warning_days) "
            "VALUES (1,?,?,NULL,?,?,?,?,?,?,?,?,?)",
            (row['unite'], row['tutelle'].strip(), int(row['annee']), row['date_archi'],
             row['n_oper_mvt'], row['n_bon_liv'], row['n_bon_rec'], row['n_oper_cmd'], row['n_oper_cor'],
             row['num_inv'], row['peremption']))
        print('settings                     1 row (from the source PARPHARM row, which was flagged')
        print('                             deleted in Clipper — confirm this is really current)')
    else:
        print('settings                     NO ROW FOUND — must be entered manually before go-live')

    out.commit()
    print('\nmigration finished ->', out_path)


if __name__ == '__main__':
    main()
