"""Deploy all verified pending fixes; keep server/game stopped for the user's restart."""
from pathlib import Path
import argparse,datetime,hashlib,importlib.util,json,shutil,sqlite3
ROOT=Path(__file__).resolve().parent.parent
VERIFY=ROOT/'.codex-verify';STAGE=VERIFY/'combat-handoff-bin';RUNTIME=ROOT/'bin/Debug'
spec=importlib.util.spec_from_file_location('gacha_deployment',VERIFY/'Deploy-ClientGacha.py')
gacha=importlib.util.module_from_spec(spec);spec.loader.exec_module(gacha)
FILES=gacha.FILES+['RCLibrary.dll','RCLibrary.pdb','PhoenixData.dll','PhoenixData.pdb']
MANIFEST=VERIFY/'all-pending-deploy-manifest.json'
def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest().upper()
def main():
 parser=argparse.ArgumentParser();parser.add_argument('--apply',action='store_true');args=parser.parse_args()
 original=json.loads((VERIFY/'client-gacha-deploy-manifest.json').read_text())
 for name,value in original['files'].items():assert digest(ROOT/name)==value,'Staged/source file changed: '+name
 assert digest(gacha.ORIGINAL)==original['original_client']
 expected={str((STAGE/name).relative_to(ROOT)):digest(STAGE/name) for name in FILES}
 expected.update({name:value for name,value in original['files'].items() if name.startswith('Data/')})
 if not args.apply:
  MANIFEST.write_text(json.dumps({'files':expected,'original_client':original['original_client']},indent=2)+'\n')
  source=sqlite3.connect(gacha.DB.resolve().as_uri()+'?mode=ro',uri=True)
  fixture=sqlite3.connect(VERIFY/'all-pending-deploy-fixture.db');source.backup(fixture);source.close()
  with fixture:result=gacha.migrate(fixture)
  fixture.close();print(json.dumps(dict(mode='preflight',files=len(expected),**result)));return
 frozen=json.loads(MANIFEST.read_text());assert frozen['files']==expected,'Preflight changed; repeat review'
 gacha.stopped();assert digest(gacha.CLIENT) in [original['original_client'],original['patched_client']]
 backup=VERIFY/('deploy-backup-'+datetime.datetime.now().strftime('%Y%m%d-%H%M%S-%f')+'-all-pending');backup.mkdir()
 for name in FILES:
  if (RUNTIME/name).exists():shutil.copy2(RUNTIME/name,backup/name)
 shutil.copy2(gacha.CLIENT,backup/'aLogin.exe');shutil.copy2(MANIFEST,backup/MANIFEST.name)
 (backup/'Data').mkdir()
 for name in expected:
  if name.startswith('Data/'):shutil.copy2(ROOT/name,backup/name)
 con=sqlite3.connect(gacha.DB);db_backup=sqlite3.connect(backup/'ServerDataBase.db');con.backup(db_backup);db_backup.close()
 try:
  gacha.stopped();con.execute('BEGIN IMMEDIATE');result=gacha.migrate(con)
  for name in FILES:shutil.copy2(STAGE/name,RUNTIME/name)
  shutil.copy2(gacha.ORIGINAL,gacha.CLIENT)
  for name in FILES:assert digest(RUNTIME/name)==expected[str((STAGE/name).relative_to(ROOT))],name
  for name,value in expected.items():
   if name.startswith('Data/'):assert digest(ROOT/name)==value,name
  assert digest(gacha.CLIENT)==original['original_client']
  con.commit()
 except Exception:
  con.rollback()
  for name in FILES:
   if (backup/name).exists():shutil.copy2(backup/name,RUNTIME/name)
   elif (RUNTIME/name).exists():(RUNTIME/name).unlink()
  shutil.copy2(backup/'aLogin.exe',gacha.CLIENT);raise
 finally:con.close()
 result.update(mode='deployed',backup=str(backup),runtime_files=len(FILES),client_restored=True,combat_spell_ms=4500,combat_combo_margin_ms=250,critical_multiplier=1.5,server_left_stopped=True)
 (backup/'result.json').write_text(json.dumps(result,indent=2)+'\n')
 for name in ['Client-Gacha-20260919.md','Combat-Handoff-20260919.md','Mall-Forging-20260919.md','Stat-Allocation-20260919.md','Critical-Hits-20260919.md']:
  with (VERIFY/name).open('a',encoding='utf-8') as out:out.write('\n\nDeployment '+datetime.datetime.now().isoformat(timespec='seconds')+': all pending changes applied from combat-handoff-bin, including4500ms/+250ms timing; eight runtime file hashes and original client verified; character tables unchanged. Backup: '+str(backup)+'. Server/game left stopped for user restart.\n')
 print(json.dumps(result,indent=2))
if __name__=='__main__':main()
