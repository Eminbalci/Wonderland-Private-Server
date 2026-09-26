"""Update only Item Mall rows; default is a rehearsal on a DB copy."""
import argparse,datetime,hashlib,json,sqlite3,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parent.parent
DB=ROOT/'bin/Debug/ServerDataBase.db'
NEW_IDS={34333}
REMOVE_IDS={32075,32002,34014,34096,34097,34098,34126}
EXPECTED_DAT_HASH='8194a3214259df1f51dc10a2bdd86e5315028f37ca7dfb7153e58f8cf71e35a1'

def stopped():
 p=subprocess.run(['powershell.exe','-NoProfile','-Command',"if(Get-Process -Name 'Wonderland Private Server' -ErrorAction SilentlyContinue){exit 1};exit 0"],capture_output=True)
 if p.returncode:raise RuntimeError('Close server before catalog deployment.')

def snapshot(c):
 result={}
 for (name,) in c.execute("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name<>'item_mall'"):
  quoted='"'+name.replace('"','""')+'"'
  data=sorted(c.execute('SELECT * FROM '+quoted).fetchall(),key=repr)
  result[name]=hashlib.sha256(repr(data).encode()).hexdigest()
 return result

def update(c):
 assert hashlib.sha256((ROOT/'Data/itemDat.wpdat').read_bytes()).hexdigest()==EXPECTED_DAT_HASH,'Item data changed; refresh catalog audit.'
 defs={i['id']:i for i in json.loads((ROOT/'.codex-verify/mall-item-definitions.json').read_text(encoding='utf-8-sig'))}
 seed=json.loads((ROOT/'Data/item_mall.json').read_text(encoding='utf-8-sig'))
 additions=[r for r in seed if r['is_bonus']==0 and r['item_id'] in NEW_IDS]
 assert len(additions)==1 and all(r['item_id'] in defs for r in additions)
 before=snapshot(c)
 names=[d[0] for d in c.execute('SELECT * FROM item_mall LIMIT 0').description]
 rows=[dict(zip(names,r)) for r in c.execute('SELECT * FROM item_mall ORDER BY id')]
 removed=[r for r in rows if r['item_id'] in REMOVE_IDS and r['is_bonus']==0]
 kept=[r for r in rows if r not in removed]
 c.executemany('DELETE FROM item_mall WHERE id=?',[(r['id'],) for r in removed])
 added=[]
 order=max([r['order_idx'] for r in kept if not r['is_bonus']]+[0])
 for row in additions:
  if any(r['item_id']==row['item_id'] and r['is_bonus']==0 for r in kept):continue
  order+=1;row=dict(row,order_idx=order,subcategory_id=1)
  cols=list(row)
  c.execute('INSERT INTO item_mall ('+','.join(cols)+') VALUES ('+','.join('?' for _ in cols)+')',[row[k] for k in cols]);added.append(row)
 after=[dict(zip(names,r)) for r in c.execute('SELECT * FROM item_mall ORDER BY id')]
 assert all(r in after for r in kept),'Existing valid offers changed'
 assert all(r['item_id'] in defs for r in after),'Missing item remains'
 assert len(after)==len(kept)+len(added)
 assert snapshot(c)==before,'Non-catalog table changed'
 assert c.execute('PRAGMA integrity_check').fetchone()[0]=='ok'
 return dict(removed=len(removed),added=len(added),total=len(after),non_catalog_tables_unchanged=True,removed_rows=removed,added_rows=added)

def main():
 parser=argparse.ArgumentParser();parser.add_argument('--apply',action='store_true');args=parser.parse_args()
 source=sqlite3.connect(DB.resolve().as_uri()+'?mode=ro',uri=True)
 if not args.apply:
  fixture=ROOT/'.codex-verify/gacha-catalog-fixture.db';target=sqlite3.connect(fixture);source.backup(target);source.close()
  with target:result=update(target)
  with target:second=update(target)
  assert second['removed']==0 and second['added']==0,'Not idempotent'
  target.close();mode='rehearsal; live DB unchanged'
 else:
  source.close();stopped();folder=ROOT/'.codex-verify'/('db-backup-'+datetime.datetime.now().strftime('%Y%m%d-%H%M%S-%f')+'-gacha-catalog');folder.mkdir()
  target=sqlite3.connect(DB);backup=sqlite3.connect(folder/'ServerDataBase.db');target.backup(backup);backup.close()
  try:
   stopped();target.execute('BEGIN IMMEDIATE');result=update(target);target.commit()
  except Exception:target.rollback();raise
  finally:target.close()
  (folder/'catalog-change.json').write_text(json.dumps(result,indent=2),encoding='utf-8');mode='applied; backup '+str(folder)
 (ROOT/'.codex-verify/gacha-catalog-change.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
 print(json.dumps({k:v for k,v in dict(mode=mode,**result).items() if k not in ['removed_rows','added_rows']},indent=2))
if __name__=='__main__':main()
