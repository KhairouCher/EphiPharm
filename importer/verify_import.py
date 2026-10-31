#!/usr/bin/env python3
"""Independent check: re-reads the DBFs with a different code path and compares to the SQLite output.
Usage: python verify_import.py <source_folder> <sqlite> [--encoding cp850]"""
import glob, os, re, sqlite3, struct, sys
src, dbp = sys.argv[1], sys.argv[2]
enc = 'cp850'
db = sqlite3.connect(dbp); bad = 0
for src_rel, tbl, nrows, ndel, nimp, _ in db.execute('select * from _import_log'):
    d = open(os.path.join(src, src_rel), 'rb').read()
    n, hl, rl = struct.unpack('<IHH', d[4:12])
    flds, i = [], 32
    while d[i] != 0x0D:
        flds.append((d[i:i+11].split(b'\0')[0].decode().lower(), chr(d[i+11]), d[i+16])); i += 32
    sums = {f: 0.0 for f, t, l in flds if t in 'NF'}; dels = 0
    for r in range(n):
        rec = d[hl + r*rl: hl + (r+1)*rl]; dels += rec[0] == 0x2A; pos = 1
        for f, t, l in flds:
            if t in 'NF':
                try: sums[f] += float(rec[pos:pos+l].decode(enc).strip() or 0)
                except ValueError: pass
            pos += l
    cnt, ndb = db.execute(f'select count(*), sum(_deleted) from "{tbl}"').fetchone()
    ok = cnt == n and (ndb or 0) == dels
    for f, v in sums.items():
        got = db.execute(f'select coalesce(sum("{f}"),0) from "{tbl}"').fetchone()[0]
        if abs(got - v) > 0.005: ok = False; print(f'  SUM MISMATCH {tbl}.{f}: file={v} sqlite={got}')
    if not ok: bad += 1
    print(('OK  ' if ok else 'FAIL'), f'{tbl:16s} rows {cnt}/{n} deleted {ndb or 0}/{dels} numeric cols {len(sums)}')
print('\nALL TABLES MATCH' if not bad else f'\n{bad} TABLE(S) DIFFER'); sys.exit(1 if bad else 0)
