import argparse, sqlite3, json
from pathlib import Path
from datetime import datetime
parser = argparse.ArgumentParser()
parser.add_argument('--database', required=True)
parser.add_argument('--backup-directory', required=True)
args = parser.parse_args()
path = Path(args.database).resolve()
backup = Path(args.backup_directory).resolve()
backup.mkdir(parents=True, exist_ok=False)
c = sqlite3.connect(str(path))
def snapshot():
    names = [r[0] for r in c.execute("SELECT name FROM sqlite_master WHERE type='table' ORDER BY name")]
    return {n: c.execute('SELECT * FROM "'+n.replace('"','""')+'" ORDER BY rowid').fetchall() for n in names}
before = snapshot()
assert c.execute('SELECT IM FROM users WHERE userID=1').fetchone() == (1598,), 'Balance changed; re-audit required'
assert c.execute('SELECT count(*) FROM inventory WHERE charID=4510001 AND itemID=57205').fetchone() == (0,), 'Unexpected item remains'
with sqlite3.connect(str(backup / 'ServerDataBase.db')) as old:
    c.backup(old)
try:
    c.execute('BEGIN IMMEDIATE')
    assert c.execute('UPDATE users SET IM=IM+160 WHERE userID=1 AND IM=1598').rowcount == 1
    after = snapshot()
    expected = dict(before)
    cols = [r[1] for r in c.execute('PRAGMA table_info(users)')]
    expected['users'] = [tuple(1758 if i==cols.index('IM') and row[cols.index('userID')]==1 else value for i,value in enumerate(row)) for row in before['users']]
    assert after == expected, 'Unexpected database change'
    assert c.execute('PRAGMA integrity_check').fetchone() == ('ok',)
    c.commit()
except:
    c.rollback()
    raise
(backup / 'refund.json').write_text(json.dumps({'userID':1,'before':1598,'refund':160,'after':1758,'reason':'Five logged AC34:1 balance queries incorrectly charged 32 IM each; no delivered item remains','all_other_data_unchanged':True},indent=2))
print('Refund verified: 1598 + 160 = 1758 IM; all other database rows unchanged. Backup:', backup)
