"""Fetch explicitly selected CC BY articles from Europe PMC; no paid content.

Run from anywhere with Python 3. XML preserves article licensing and attribution.
Only CC BY (not NC/ND) is accepted. Retractions and notices are rejected.
The research archive is never automatically admitted to the runtime knowledge pack.
"""
import concurrent.futures
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import urllib.parse
import urllib.request
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
IDS = ('PMC11273505', 'PMC5415598', 'PMC8650609', 'PMC8725735',
       'PMC6995108', 'PMC9337232', 'PMC11502332', 'PMC12731911',
       'PMC12110614', 'PMC10712052', 'PMC12929380')


def get(url):
    req = urllib.request.Request(url, headers={'User-Agent': 'Sandtray-Research-Library/1.0'})
    with urllib.request.urlopen(req, timeout=50) as response:
        return response.read(15_000_000)


def text(element):
    return ' '.join(''.join(element.itertext()).split()) if element is not None else ''


def fetch(pmcid):
    # Search by PMCID explicitly; EXT_ID primarily refers to PubMed identifiers.
    query = urllib.parse.urlencode({'query': 'PMCID:' + pmcid, 'format': 'json', 'resultType': 'core'})
    records = json.loads(get('https://www.ebi.ac.uk/europepmc/webservices/rest/search?' + query))['resultList']['result']
    record = next(x for x in records if x.get('pmcid') == pmcid)
    if record.get('license', '').lower() != 'cc by' or record.get('isRetracted') == 'Y' or 'retract' in record['title'].lower():
        raise ValueError('License/retraction gate rejected ' + pmcid)
    url = 'https://www.ebi.ac.uk/europepmc/webservices/rest/' + pmcid + '/fullTextXML'
    raw = get(url)
    root = ET.fromstring(raw)
    license_text = ' '.join(text(x) for x in root.findall('.//permissions'))
    licenses = [v for license_node in root.findall('.//license') for x in license_node.iter() for k, v in x.attrib.items() if k.endswith('href')]
    if not (any('creativecommons.org/licenses/by/' in v for v in licenses) or
            'Creative Commons Attribution License (CC BY)' in license_text):
        raise ValueError('Full text does not confirm CC BY: ' + pmcid)
    if root.get('article-type') in ('retraction', 'retraction-notice'):
        raise ValueError('Retraction notice: ' + pmcid)
    folder = ROOT / 'fulltext'
    folder.mkdir(parents=True, exist_ok=True)
    (folder / (pmcid + '.xml')).write_bytes(raw)
    header = f"# {record['title']}\n\nAuthors: {record.get('authorString', '')}\n\nSource: https://doi.org/{record['doi']}\n\n{license_text}\n\n"
    paragraphs = []
    for container in (root.find('.//article-meta/abstract'), root.find('./body')):
        if container is not None:
            for node in container.iter():
                if node.tag == 'title':
                    paragraphs.append('## ' + text(node))
                elif node.tag == 'p':
                    paragraphs.append(text(node))
    (folder / (pmcid + '.md')).write_text(header + '\n\n'.join(paragraphs) + '\n')
    return {'id': pmcid.lower(), 'pmcid': pmcid, 'title': record['title'],
            'authors': record.get('authorString', ''), 'date': record.get('firstPublicationDate'),
            'url': 'https://doi.org/' + record['doi'], 'download_url': url,
            'license': 'CC-BY', 'license_urls': licenses, 'license_statement': license_text,
            'accessed': datetime.now(timezone.utc).date().isoformat(), 'sha256': hashlib.sha256(raw).hexdigest(),
            'fulltext': 'fulltext/' + pmcid + '.md', 'xml': 'fulltext/' + pmcid + '.xml',
            'abstract': text(root.find('.//article-meta/abstract')),
            'retraction_checked': record.get('isRetracted', 'not flagged by Europe PMC')}


if __name__ == '__main__':
    results = []
    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
        for record in pool.map(fetch, IDS):
            results.append(record)
            print(record['pmcid'], record['license'], record['title'], flush=True)
    (ROOT / 'download_manifest.json').write_text(json.dumps(results, ensure_ascii=False, indent=2) + '\n')
