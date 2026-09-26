"""User-approved best-effort replacement of Mizaki's invisible legacy food IDs.
Default mode rehearses on an in-memory copy. Mapping is an approximation, not proof
of original identity; preserve quantities/positions and all other data.
"""
import argparse
import datetime
import json
from pathlib import Path
import sqlite3
import subprocess
import os
ROOT=Path(__file__).resolve().parent.parent
DB=ROOT/'bin/Debug/ServerDataBase.db'
CHAR_ID=4510001
MAPPING={28006:(41040,'Apple'),28007:(41050,'Small Pineapple'),28014:(41066,'Coconut')}

def repair(conn):
    conn.row_factory=sqlite3.Row
    before=[dict(r) for r in conn.execute('SELECT * FROM inventory ORDER BY pri_key')]
    other_tables={name: conn.execute('SELECT * FROM "'+name.replace('"','""')+'"').fetchall() for (name,) in conn.execute("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name<>'inventory'")}
    expected=[dict(r) for r in before];changed=[]
    for row in expected:
        if row['charID']!=CHAR_ID or row['storID']!=0 or row['itemID'] not in MAPPING: continue
        old=row['itemID']; new,name=MAPPING[old]
        if row['qty']<1 or row['qty']>50: raise RuntimeError('Unexpected stack size; no changes applied')
        conn.execute('UPDATE inventory SET itemID=? WHERE pri_key=? AND itemID=?',(new,row['pri_key'],old))
        row['itemID']=new
        changed.append({'slot':row['pos'],'old_id':old,'new_id':new,'name':name,'quantity':row['qty']})
    assert [dict(r) for r in conn.execute('SELECT * FROM inventory ORDER BY pri_key')]==expected
    for name,rows in other_tables.items(): assert conn.execute('SELECT * FROM "'+name.replace('"','""')+'"').fetchall()==rows
    assert conn.execute('PRAGMA integrity_check').fetchone()[0]=='ok'
    return {'character':CHAR_ID,'approximate_mapping_authorized_by_user':True,'replaced':changed,'quantities_positions_and_other_data_preserved':True}

def stopped():
    ps=str(Path(os.environ['SystemRoot'])/'System32/WindowsPowerShell/v1.0/powershell.exe')
    result=subprocess.run([ps,'-NoProfile','-Command',"if(Get-Process -Name 'Wonderland Private Server' -ErrorAction SilentlyContinue){exit 1}"],capture_output=True)
    if result.returncode: raise RuntimeError('Close server before applying')

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--apply',action='store_true');args=parser.parse_args()
    if not args.apply:
        src=sqlite3.connect(DB.resolve().as_uri()+'?mode=ro',uri=True);conn=sqlite3.connect(':memory:');src.backup(conn);src.close()
        result=repair(conn);assert not repair(conn)['replaced'];conn.close()
        print(json.dumps({'mode':'rehearsal; runtime DB unchanged','idempotent':True,**result},indent=2));return
    stopped();folder=ROOT/'.codex-verify'/('db-backup-'+datetime.datetime.now().strftime('%Y%m%d-%H%M%S-%f')+'-visible-items');folder.mkdir()
    conn=sqlite3.connect(DB);backup=sqlite3.connect(folder/'ServerDataBase.db');conn.backup(backup);backup.close()
    try:
        stopped();conn.execute('BEGIN IMMEDIATE');result=repair(conn);conn.commit()
    except Exception:
        conn.rollback();raise
    finally: conn.close()
    (folder/'repair.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    print(json.dumps({'mode':'applied','backup':str(folder),**result},indent=2))
if __name__=='__main__':main()
