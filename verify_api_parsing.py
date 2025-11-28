import xml.etree.ElementTree as ET

def parse_response(file_path):
    try:
        tree = ET.parse(file_path)
        root = tree.getroot()
        # Get default namespace
        ns = {'atom': 'http://www.w3.org/2005/Atom'}
        
        entries = root.findall('atom:entry', ns)
        print(f"Found {len(entries)} entries")
        
        for i, entry in enumerate(entries):
            title_elem = entry.find('atom:title', ns)
            
            # Find PDF link
            pdf_link = None
            links = entry.findall('atom:link', ns)
            for link in links:
                title = link.attrib.get('title')
                type_attr = link.attrib.get('type')
                href = link.attrib.get('href')
                
                if title == 'pdf' or type_attr == 'application/pdf':
                    pdf_link = href
                    break
            
            if not pdf_link:
                # print(f"Entry {i}: No PDF link found")
                continue
                
            print(f"Entry {i}: Title: {title_elem.text}")
            print(f"Entry {i}: Found PDF: {pdf_link}")
            if i >= 5: break # Just show first 5
    except Exception as e:
        print(f"Error parsing: {e}")

parse_response('arxiv_search_combined.xml')
