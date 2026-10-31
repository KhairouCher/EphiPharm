#!/usr/bin/env python3
"""EPIPHARM importer: Clipper/dBase III tables (.DBF, .ARC) -> one SQLite file.

Read-only on the source folder. Rows flagged deleted are kept with _deleted=1
(the app should query WHERE _deleted=0; PARPHARM's live row is flagged deleted). Skips USERS.PSW (accounts are handled separately)
and the old sub-folders (123321, DEC98, ORSEC, Reseau, RESEAUX) unless --subfolders.

Usage: python import_dbf.py <source_folder> <output.sqlite> [--encoding cp850]
"""
import argparse, glob, os, re, sqlite3, struct, sys, time

def read_dbf(path, encoding):
    with open(path, 'rb') as f:
        h = f.read(32)
        if len(h) < 32:
            raise ValueError('file too short')
        nrec = struct.unpack('<I', h[4:8])[0]
        hlen, rlen = struct.unpack('<HH', h[8:12])
        fields, raw = [], f.read(hlen - 32)
        for i in range(0, len(raw), 32):
            if raw[i] == 0x0D:
                break
            name = raw[i:i+11].split(b'\0')[0].decode('ascii', 'replace')
            fields.append((name, chr(raw[i+11]), raw[i+16], raw[i+17]))
        f.seek(hlen)
        def rows():
            for _ in range(nrec):
                rec = f.read(rlen)
                if len(rec) < rlen or rec[0] == 0x1A:
                    return
                yield rec
        return fields, nrec, rlen, list(rows())

def convert(rec, fields, encoding, issues):
    out, pos = [], 1
    for name, t, ln, dc in fields:
        raw = rec[pos:pos+ln]; pos += ln
        s = raw.decode(encoding, 'replace').strip(' \x00')
        if t == 'C':
            out.append(s)
        elif t in 'NF':
            if not s or s in '-.':
                out.append(None)
            else:
                try:
                    out.append(float(s) if (dc or '.' in s) else int(s))
                except ValueError:
                    issues.append((name, 'bad number', s)); out.append(None)
        elif t == 'D':
            if not s or set(s) <= {'0'}:
                out.append(None)
            elif re.fullmatch(r'\d{8}', s):
                y, m, d = s[:4], s[4:6], s[6:]
                if '1800' <= y <= '2200' and '01' <= m <= '12' and '01' <= d <= '31':
                    out.append(f'{y}-{m}-{d}')
                else:
                    issues.append((name, 'bad date', s)); out.append(None)
            else:
                issues.append((name, 'bad date', s)); out.append(None)
        elif t == 'L':
            out.append(1 if s.upper() in ('T', 'Y') else 0 if s.upper() in ('F', 'N') else None)
        elif t == 'M':
            out.append(s or None)   # memo block pointer only; memo text not imported
        else:
            out.append(s)
    return out

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('src'); ap.add_argument('out')
    ap.add_argument('--encoding', default='cp850')
    ap.add_argument('--subfolders', action='store_true')
    a = ap.parse_args()
    if os.path.exists(a.out):
        sys.exit(f'refusing to overwrite {a.out}')
    files = sorted(glob.glob(os.path.join(a.src, '*.DBF')) + glob.glob(os.path.join(a.src, '*.ARC')))
    if a.subfolders:
        files += sorted(glob.glob(os.path.join(a.src, '*', '*.DBF')))
    db = sqlite3.connect(a.out)
    db.execute('CREATE TABLE _import_log(source TEXT, tbl TEXT, dbf_rows INT, deleted INT, imported INT, issues INT)')
    db.execute('CREATE TABLE _import_issues(tbl TEXT, rowno INT, field TEXT, problem TEXT, raw TEXT)')
    used, t0 = set(), time.time()
    for p in files:
        rel = os.path.relpath(p, a.src)
        base = re.sub(r'\W', '_', os.path.splitext(rel)[0].lower()).strip('_')
        tbl = base if not p.upper().endswith('.ARC') else base
        if tbl in used: tbl += '_' + p.upper()[-3:].lower()
        used.add(tbl)
        try:
            fields, n, rlen, recs = read_dbf(p, a.encoding)
        except Exception as e:
            print(f'SKIP {rel}: {e}'); continue
        cols = []
        for name, t, ln, dc in fields:
            ty = 'TEXT' if t in 'CDM' else 'INTEGER' if (t in 'NF' and not dc) else 'REAL' if t in 'NF' else 'INTEGER' if t == 'L' else 'TEXT'
            cols.append(f'"{name.lower()}" {ty}')
        db.execute(f'CREATE TABLE "{tbl}"(_rowno INTEGER PRIMARY KEY, _deleted INTEGER NOT NULL, {", ".join(cols)})')
        ins = f'INSERT INTO "{tbl}" VALUES({",".join("?"*(len(fields)+2))})'
        deleted = imported = nissue = 0
        for i, rec in enumerate(recs, 1):
            isdel = 1 if rec[0] == 0x2A else 0   # '*' = flagged deleted in Clipper; KEPT, with _deleted=1
            deleted += isdel
            iss = []
            vals = convert(rec, fields, a.encoding, iss)
            for fld, prob, raw in iss:
                db.execute('INSERT INTO _import_issues VALUES(?,?,?,?,?)', (tbl, i, fld.lower(), prob, raw))
            nissue += len(iss)
            db.execute(ins, [i, isdel] + vals); imported += 1
        db.execute('INSERT INTO _import_log VALUES(?,?,?,?,?,?)', (rel, tbl, n, deleted, imported, nissue))
        print(f'{rel:28s} -> {tbl:24s} rows={n:7d} deleted={deleted:5d} imported={imported:7d} issues={nissue}')
    db.commit(); db.close()
    print(f'done in {time.time()-t0:.1f}s -> {a.out}')

if __name__ == '__main__':
    main()
