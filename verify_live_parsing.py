import xml.etree.ElementTree as ET

def parse_response(file_path):
    tree = ET.parse(file_path)
    root = tree.getroot()
    # Get default namespace
    ns = {'atom': 'http://www.w3.org/2005/Atom'}
    
    entries = root.findall('atom:entry', ns)
    print(f"Found {len(entries)} entries")
    
    for i, entry in enumerate(entries):
        id_elem = entry.find('atom:id', ns)
        title_elem = entry.find('atom:title', ns)
        
        # Find PDF link
        pdf_link = None
        links = entry.findall('atom:link', ns)
        for link in links:
            title = link.attrib.get('title')
            type_attr = link.attrib.get('type')
            href = link.attrib.get('href')
            # print(f"Entry {i} Link: title='{title}', type='{type_attr}', href='{href}'")
            
            if title == 'pdf' or type_attr == 'application/pdf':
                pdf_link = href
                break
        
        if not pdf_link:
            print(f"Entry {i}: No PDF link found")
            continue
            
        print(f"Entry {i}: Found PDF: {pdf_link}")

parse_response('arxiv_feed.xml')
