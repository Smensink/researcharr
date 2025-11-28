import xml.etree.ElementTree as ET
import datetime

atom_xml = """<?xml version='1.0' encoding='UTF-8'?>
<feed xmlns:arxiv="http://arxiv.org/schemas/atom" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns="http://www.w3.org/2005/Atom" xml:lang="en-us">
  <id>http://rss.arxiv.org/atom/cs</id>
  <title>cs updates on arXiv.org</title>
  <updated>2025-11-23T05:00:04.556492+00:00</updated>
  <link href="http://rss.arxiv.org/atom/cs" rel="self" type="application/atom+xml"/>
  <subtitle>cs updates on the arXiv.org e-print archive.</subtitle>
  <entry>
    <id>http://arxiv.org/abs/2511.08685</id>
    <updated>2025-11-23T05:00:04+00:00</updated>
    <published>2025-11-23T05:00:04+00:00</published>
    <title>Test Paper Title</title>
    <summary>Test Summary</summary>
    <author>
      <name>Test Author</name>
    </author>
    <link title="pdf" href="http://arxiv.org/pdf/2511.08685" rel="related" type="application/pdf"/>
  </entry>
</feed>"""

def parse_response(content):
    root = ET.fromstring(content)
    # Get default namespace
    ns = {'atom': 'http://www.w3.org/2005/Atom'}
    
    entries = root.findall('atom:entry', ns)
    print(f"Found {len(entries)} entries")
    
    for entry in entries:
        id_elem = entry.find('atom:id', ns)
        title_elem = entry.find('atom:title', ns)
        
        # Find PDF link
        pdf_link = None
        for link in entry.findall('atom:link', ns):
            if link.attrib.get('title') == 'pdf' or link.attrib.get('type') == 'application/pdf':
                pdf_link = link.attrib.get('href')
                break
        
        if not pdf_link:
            print("No PDF link found")
            continue
            
        print(f"Title: {title_elem.text}")
        print(f"PDF: {pdf_link}")
        print("Category: 8000")

parse_response(atom_xml)
