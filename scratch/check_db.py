import sqlite3

conn = sqlite3.connect('bin/Debug/ServerDataBase.db')
c = conn.cursor()
tables = [t[0] for t in c.execute("SELECT name FROM sqlite_master WHERE type='table'").fetchall()]
print("Tables:", tables)

for t in tables:
    if 'char' in t.lower():
        rows = c.execute(f"SELECT * FROM {t} LIMIT 5").fetchall()
        print(f"Table {t} ({len(rows)} samples):", rows)
