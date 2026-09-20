"""Repair only Mizaki's legacy invisible duplicate; preserve it in Pet Hotel.
Defaults to a read-only-source backup rehearsal. --apply requires server stopped.
"""
import argparse
import datetime
import json
from pathlib import Path
import sqlite3
import subprocess

ROOT = Path(__file__).resolve().parent.parent
DB = ROOT / 'bin' / 'Debug' / 'ServerDataBase.db'
CHAR_ID = 4510001


def repair(conn):
    conn.row_factory = sqlite3.Row
    before = [dict(r) for r in conn.execute('SELECT * FROM character_pets ORDER BY id')]
    carried = [r for r in before if r['charID'] == CHAR_ID and not r['isHotel']]
    visible = next((r for r in carried if r['slot'] == 3 and r['petID'] == 17003), None)
    hidden = next((r for r in carried if r['slot'] == 4 and r['petID'] == 17003), None)
    if visible is None or hidden is None:
        raise RuntimeError('Expected duplicate pair changed; no data modified.')
    if hidden['isBattle'] or hidden['isRide']:
        raise RuntimeError('Hidden pet is active; manual review needed before moving it.')
    hotel_slots = {r['slot'] for r in before if r['charID'] == CHAR_ID and r['isHotel']}
    free = next((s for s in range(1, 21) if s not in hotel_slots), None)
    if free is None:
        raise RuntimeError('Pet Hotel full; no pet removed.')
    updated = conn.execute('UPDATE character_pets SET slot=?, isHotel=1 WHERE id=? AND charID=? AND slot=4 AND petID=17003 AND isHotel=0', (free, hidden['id'], CHAR_ID))
    assert updated.rowcount == 1
    after = [dict(r) for r in conn.execute('SELECT * FROM character_pets ORDER BY id')]
    expected = [dict(r) for r in before]
    expected_hidden = next(r for r in expected if r['id'] == hidden['id'])
    expected_hidden.update(slot=free, isHotel=1)
    assert after == expected, 'Unexpected pet data change'
    assert len([r for r in after if r['charID'] == CHAR_ID and not r['isHotel']]) == len(carried) - 1
    assert conn.execute('PRAGMA integrity_check').fetchone()[0] == 'ok'
    return {'character': CHAR_ID, 'pet_id': hidden['petID'], 'row_id': hidden['id'], 'from_team_slot': 4, 'to_hotel_slot': free, 'carried_pets_after': len(carried) - 1, 'all_pet_stats_preserved': True, 'all_other_pet_rows_unchanged': True}


def server_stopped():
    result = subprocess.run(['powershell.exe', '-NoProfile', '-Command', "if (Get-Process -Name 'Wonderland Private Server' -ErrorAction SilentlyContinue) { exit 1 }"], capture_output=True)
    if result.returncode:
        raise RuntimeError('Server must be closed first to prevent autosave overwriting the repair.')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--apply', action='store_true')
    args = parser.parse_args()
    if not args.apply:
        source = sqlite3.connect(DB.resolve().as_uri() + '?mode=ro', uri=True)
        rehearsal = sqlite3.connect(':memory:')
        source.backup(rehearsal)
        source.close()
        result = repair(rehearsal)
        rehearsal.close()
        print(json.dumps({'mode': 'rehearsal; runtime DB unchanged', **result}, indent=2))
        return
    server_stopped()
    folder = ROOT / '.codex-verify' / ('db-backup-' + datetime.datetime.now().strftime('%Y%m%d-%H%M%S-%f') + '-hidden-pet')
    folder.mkdir()
    source = sqlite3.connect(DB)
    backup = sqlite3.connect(folder / 'ServerDataBase.db')
    source.backup(backup)
    backup.close()
    try:
        server_stopped()
        source.execute('BEGIN IMMEDIATE')
        result = repair(source)
        source.commit()
    except Exception:
        source.rollback()
        raise
    finally:
        source.close()
    (folder / 'repair.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps({'mode': 'applied', 'backup': str(folder), **result}, indent=2))


if __name__ == '__main__':
    main()
