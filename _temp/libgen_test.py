import re
import sys
import requests
from bs4 import BeautifulSoup

# Usage: python _temp/libgen_test.py "<title>" "<author>"
title = sys.argv[1] if len(sys.argv) > 1 else "Systematic reviews and meta-analyses of interventions in the Journal of Critical Care"
author = sys.argv[2] if len(sys.argv) > 2 else ""
query = f"{title} {author}".strip()
url = f"https://libgen.li/index.php/search.php?req={requests.utils.quote(query)}&column=title&res=100"

print(f"[INFO] URL: {url}")
resp = requests.get(url, timeout=15)
resp.raise_for_status()

soup = BeautifulSoup(resp.text, "html.parser")
tables = soup.find_all("table")
print(f"[DEBUG] Found {len(tables)} tables")

results = []
for idx, table in enumerate(tables):
    rows = table.find_all("tr")
    headers = []
    if rows:
        headers = [c.get_text(strip=True).lower() for c in rows[0].find_all(["th", "td"])]
        print(f"[TABLE {idx}] headers: {headers}")

    for row in rows[1:]:
        cells = row.find_all("td")
        if len(cells) < 3:
            continue

        md5 = None
        title_text = "Unknown Title"
        author_text = "Unknown Author"
        download = None

        for link in row.find_all("a"):
            href = link.get("href") or ""
            text = link.get_text(strip=True)
            if "md5=" in href and md5 is None:
                m = re.search(r"md5=([0-9a-fA-F]{32})", href)
                if m:
                    md5 = m.group(1)
                    title_text = text or title_text
            if "search.php" in href and author_text == "Unknown Author":
                author_text = text
            if any(k in href for k in ["ads.php", "get.php", "library.lol"]):
                download = href

        if md5 and download:
            results.append({"md5": md5, "title": title_text, "author": author_text, "download": download})

if results:
    print(f"[OK] Parsed {len(results)} results. First 5:")
    for r in results[:5]:
        print(f" - md5={r['md5']} | author={r['author']} | title={r['title']} | download={r['download']}")
else:
    print("[WARN] No results parsed.")
