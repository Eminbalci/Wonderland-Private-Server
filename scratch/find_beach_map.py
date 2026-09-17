with open(r"D:\GitHub\Wonderland-Private-Server\listdata\maps.csv", "r", encoding="utf-8", errors="ignore") as f:
    for line in f:
        lower = line.lower()
        if "newbie" in lower or "beach" in lower or "wreck" in lower or "cabin" in lower or "deck" in lower or "ship" in lower:
            print(line.strip())
