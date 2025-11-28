import sys
import time
from urllib.parse import quote
from selenium import webdriver
from selenium.webdriver.chrome.options import Options
from selenium.webdriver.common.by import By

# Usage: python _temp/libgen_selenium_test.py "<title>" "<author>"
title = sys.argv[1] if len(sys.argv) > 1 else "Systematic reviews and meta-analyses of interventions in the Journal of Critical Care"
author = sys.argv[2] if len(sys.argv) > 2 else ""
query = f"{title} {author}".strip()
encoded = quote(query)
url = f"https://libgen.li/index.php/search.php?req={encoded}&column=title&res=100"

options = Options()
options.add_argument("--headless=new")
options.add_argument("--disable-gpu")
options.add_argument("--no-sandbox")
options.add_argument("--disable-dev-shm-usage")

print(f"[INFO] URL: {url}")

with webdriver.Chrome(options=options) as driver:
    driver.get(url)
    time.sleep(3)

    tables = driver.find_elements(By.CSS_SELECTOR, "table")
    print(f"[DEBUG] Found {len(tables)} tables")

    results = []
    for idx, table in enumerate(tables):
        rows = table.find_elements(By.CSS_SELECTOR, "tr")
        # Dump header cells to understand structure
        if rows:
            header_cells = [c.text.strip() for c in rows[0].find_elements(By.CSS_SELECTOR, "th,td")]
            print(f"[TABLE {idx}] headers: {header_cells}")

        for row in rows:
            cells = row.find_elements(By.CSS_SELECTOR, "td")
            if len(cells) < 3:
                continue
            if not cells[0].text.strip().isdigit():
                continue

            title_text = "Unknown Title"
            author_text = "Unknown Author"
            download_link = None
            md5 = None

            for link in row.find_elements(By.TAG_NAME, "a"):
                href = link.get_attribute("href") or ""
                text = link.text.strip()
                if "md5=" in href and md5 is None:
                    md5 = href.split("md5=")[-1][:32]
                    title_text = text or title_text
                if "search.php" in href and author_text == "Unknown Author":
                    author_text = text
                if any(k in href for k in ["ads.php", "get.php", "library.lol"]):
                    download_link = href

            if md5 and download_link:
                results.append({
                    "md5": md5,
                    "title": title_text,
                    "author": author_text,
                    "download": download_link
                })

    if results:
        print(f"[OK] Parsed {len(results)} results. First 5:")
        for r in results[:5]:
            print(f" - md5={r['md5']} | author={r['author']} | title={r['title']} | download={r['download']}")
    else:
        print("[WARN] No results parsed.")
