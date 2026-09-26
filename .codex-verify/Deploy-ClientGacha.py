"""Rehearse or deploy original-client gacha; never stop running processes."""
from pathlib import Path
import argparse,datetime,hashlib,json,shutil,sqlite3,subprocess
ROOT=Path(__file__).resolve().parent.parent
VERIFY=ROOT/'.codex-verify';RUNTIME=ROOT/'bin/Debug';CLIENT=Path(r'D:/Game Private/WLRI/aLogin.exe')
ORIGINAL=VERIFY/'deploy-backup-20260919-131033-013-native-gacha/aLogin.exe'
STAGE=VERIFY/'client-gacha-bin';DB=RUNTIME/'ServerDataBase.db'
FILES=['Wonderland Private Server.exe','Wonderland Private Server.pdb','wlo.pserver.core.dll','wlo.pserver.core.pdb']
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def stopped():
 result=subprocess.run(['powershell.exe','-NoProfile','-Command',"if(Get-Process -Name 'Wonderland Private Server','aLogin' -ErrorAction SilentlyContinue){exit 1};exit 0"],capture_output=True)
 if result.returncode:raise RuntimeError('Close server and game before deployment.')
def snapshot(c):
 return {name:sorted(c.execute('SELECT * FROM "'+name.replace('"','""')+'"').fetchall(),key=repr) for name, in c.execute("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name<>'item_mall'")}
def migrate(c):
 before=snapshot(c);rows=c.execute('SELECT * FROM item_mall ORDER BY id').fetchall();columns=[d[0] for d in c.execute('SELECT * FROM item_mall LIMIT 0').description];key=columns.index('item_id')
 keep=[r for r in rows if r[key]!=34333];c.execute('DELETE FROM item_mall WHERE item_id=34333')
 assert c.execute('SELECT * FROM item_mall ORDER BY id').fetchall()==keep
 assert snapshot(c)==before and c.execute('PRAGMA integrity_check').fetchone()[0]=='ok'
 return dict(removed_lucky_offers=len(rows)-len(keep),remaining_offers=len(keep),other_tables_unchanged=True)
def main():
 parser=argparse.ArgumentParser();parser.add_argument('--apply',action='store_true');args=parser.parse_args()
 manifest=json.loads((VERIFY/'client-gacha-deploy-manifest.json').read_text())
 for rel,expected in manifest['files'].items():assert digest(ROOT/rel)==expected,'Staged file changed: '+rel
 assert digest(ORIGINAL)==manifest['original_client']
 if not args.apply:
  source=sqlite3.connect(DB.resolve().as_uri()+'?mode=ro',uri=True);fixture=sqlite3.connect(VERIFY/'client-gacha-catalog-fixture.db');source.backup(fixture);source.close()
  with fixture:result=migrate(fixture)
  with fixture:assert migrate(fixture)['removed_lucky_offers']==0
  fixture.close();print(json.dumps(dict(mode='rehearsal; live database and client unchanged',**result)));return
 stopped();assert digest(CLIENT) in [manifest['original_client'],manifest['patched_client']],'Client changed; review before restoring'
 folder=VERIFY/('deploy-backup-'+datetime.datetime.now().strftime('%Y%m%d-%H%M%S-%f')+'-client-gacha');folder.mkdir()
 for name in FILES:shutil.copy2(RUNTIME/name,folder/name)
 shutil.copy2(CLIENT,folder/'aLogin.exe')
 con=sqlite3.connect(DB);backup=sqlite3.connect(folder/'ServerDataBase.db');con.backup(backup);backup.close()
 try:
  stopped();con.execute('BEGIN IMMEDIATE');result=migrate(con)
  for name in FILES:shutil.copy2(STAGE/name,RUNTIME/name)
  shutil.copy2(ORIGINAL,CLIENT)
  for name in FILES:assert digest(RUNTIME/name)==digest(STAGE/name)
  assert digest(CLIENT)==manifest['original_client']
  con.commit()
 except Exception:
  con.rollback()
  for name in FILES:shutil.copy2(folder/name,RUNTIME/name)
  shutil.copy2(folder/'aLogin.exe',CLIENT)
  raise
 finally:con.close()
 result.update(mode='deployed',backup=str(folder),client_restored=True,combat_delay_unchanged=True)
 (folder/'result.json').write_text(json.dumps(result,indent=2))
 with (VERIFY/'Client-Gacha-20260919.md').open('a',encoding='utf-8') as f:f.write('\nDeployment: '+json.dumps(result)+'\n')
 print(json.dumps(result,indent=2))
if __name__=='__main__':main()
