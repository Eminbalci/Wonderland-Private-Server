import sqlite3
import os

db_path = r"D:\GitHub\Wonderland-Private-Server\Data\ServerDataBase.db"
conn = sqlite3.connect(db_path)
c = conn.cursor()
c.execute("PRAGMA table_info(npcs)")
print("Cols:", [col[1] for col in c.fetchall()])
c.execute("SELECT * FROM npcs")
rows = c.fetchall()
print("Total rows:", len(rows))
for r in rows[:20]:
    print(r)
