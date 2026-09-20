"""Restore only two confirmed lost Mizaki equipment items; defaults to a rehearsal."""
import argparse
import datetime
import json
from pathlib import Path
import sqlite3
import subprocess

ROOT = Path(__file__).resolve().parent.parent
DB = ROOT / 'bin/Debug/ServerDataBase.db'
OLD = ROOT / 'bin/Debug/ServerDataBase.db.before-pet-restore-20260919-0152.bak'
CHAR_ID = 4510001
ITEMS = {21013: 'Kimono', 24013: 'Wooden Shoes'}

def repair(conn):
    conn.row_factory = sqlite3.Row
    before = [dict(r) for r in conn.execute('SELECT * FROM inventory ORDER BY pri_key')]
    others = {name: conn.execute('SELECT * FROM "' + name.replace('"', '""') + '"').fetchall()
              for (name,) in conn.execute("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name<>'inventory'")}
    occupied = {r['pos'] for r in before if r['charID'] == CHAR_ID and r['storID'] == 0}
    original = sqlite3.connect(OLD.resolve().as_uri() + '?mode=ro', uri=True)
    original.row_factory = sqlite3.Row
    restored = []
    try:
        for item_id, name in ITEMS.items():
            if any(r['charID'] == CHAR_ID and r['itemID'] == item_id and r['qty'] > 0 for r in before):
                continue
            rows = original.execute('SELECT * FROM inventory WHERE charID=? AND storID=1 AND itemID=?', (CHAR_ID, item_id)).fetchall()
            if len(rows) != 1 or rows[0]['qty'] != 1:
                raise RuntimeError('Original equipment evidence changed')
            slot = next((s for s in range(1, 51) if s not in occupied), None)
            if slot is None:
                raise RuntimeError('Inventory full; no changes applied')
            row = dict(rows[0]); del row['pri_key']
            row.update(storID=0, invIdx=slot, pos=slot)
            cur = conn.execute('INSERT INTO inventory (' + ','.join(row) + ') VALUES (' + ','.join('?' for _ in row) + ')', tuple(row.values()))
            actual = dict(conn.execute('SELECT * FROM inventory WHERE pri_key=?', (cur.lastrowid,)).fetchone())
            assert {k: v for k, v in actual.items() if k != 'pri_key'} == row
            restored.append({'item': name, 'id': item_id, 'bag_slot': slot, 'row_id': cur.lastrowid})
            occupied.add(slot)
    finally:
        original.close()
    added_ids = {r['row_id'] for r in restored}
    after = [dict(r) for r in conn.execute('SELECT * FROM inventory ORDER BY pri_key')]
    assert [r for r in after if r['pri_key'] not in added_ids] == before, 'Existing inventory changed'
    for table, rows in others.items():
        assert conn.execute('SELECT * FROM "' + table.replace('"', '""') + '"').fetchall() == rows, 'Other table changed: ' + table
    assert conn.execute('PRAGMA integrity_check').fetchone()[0] == 'ok'
    return {'character': CHAR_ID, 'restored': restored, 'existing_inventory_and_other_tables_unchanged': True}

def stopped():
    ps = str(Path(__import__('os').environ['SystemRoot']) / 'System32/WindowsPowerShell/v1.0/powershell.exe')
    r = subprocess.run([ps, '-NoProfile', '-Command', "if (Get-Process -Name 'Wonderland Private Server' -ErrorAction SilentlyContinue) { exit 1 }"], capture_output=True)
    if r.returncode:
        raise RuntimeError('Close server before applying to avoid autosave overwriting recovery')

def main():
    parser = argparse.ArgumentParser(); parser.add_argument('--apply', action='store_true'); args = parser.parse_args()
    if not args.apply:
        source = sqlite3.connect(DB.resolve().as_uri() + '?mode=ro', uri=True)
        conn = sqlite3.connect(':memory:'); source.backup(conn); source.close()
        result = repair(conn)
        assert not repair(conn)['restored'], 'Recovery is not idempotent'
        print(json.dumps({'mode': 'rehearsal; runtime DB unchanged', 'repeat_repair_no_duplicates': True, **result}, indent=2)); conn.close(); return
    stopped()
    folder = ROOT / '.codex-verify' / ('db-backup-' + datetime.datetime.now().strftime('%Y%m%d-%H%M%S-%f') + '-inventory')
    folder.mkdir(); conn = sqlite3.connect(DB)
    backup = sqlite3.connect(folder / 'ServerDataBase.db'); conn.backup(backup); backup.close()
    try:
        stopped(); conn.execute('BEGIN IMMEDIATE'); result = repair(conn); conn.commit()
    except Exception:
        conn.rollback(); raise
    finally:
        conn.close()
    (folder / 'repair.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps({'mode': 'applied', 'backup': str(folder), **result}, indent=2))

if __name__ == '__main__':
    main()
