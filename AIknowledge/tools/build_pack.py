"""Build the audited runtime notes; never automatically ingest downloaded papers."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TARGET = ROOT.parent / 'api_backend/analysis/data/knowledge.json'

def build():
    catalog = json.loads((ROOT / 'catalog.json').read_text())
    for source in catalog['sources']:
        if source.get('xml'):
            actual = hashlib.sha256((ROOT / source['xml']).read_bytes()).hexdigest()
            if actual != source['sha256']:
                raise ValueError('Archive checksum changed: ' + source['id'])
    passages = [p for p in catalog['passages'] if p['eligible']]
    ids = {p['source_id'] for p in passages}
    sources = [{k: s[k] for k in ('id', 'title', 'url', 'license', 'review_status', 'locator')}
               for s in catalog['sources'] if s['id'] in ids]
    by_id = {s['id']: s for s in catalog['sources']}
    for source in sources:
        source['authors'] = by_id[source['id']].get('authors', '')
        source['license_urls'] = by_id[source['id']].get('license_urls', [])
    if len(ids) != len(sources) or len({p['id'] for p in passages}) != len(passages):
        raise ValueError('Duplicate or missing source/passages')
    payload = dict(schema_version=1, sources=sources, passages=passages)
    payload['version'] = hashlib.sha256(json.dumps(payload, ensure_ascii=False, sort_keys=True).encode()).hexdigest()
    TARGET.parent.mkdir(parents=True, exist_ok=True)
    TARGET.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + '\n')
    notes = ROOT / 'notes'
    notes.mkdir(exist_ok=True)
    index = ['# Source index', '', '| Resource | Type | Local material |', '| --- | --- | --- |']
    for passage in catalog['passages']:
        source = by_id[passage['source_id']]
        name = source['id'] + '.md'
        authors = source.get('authors', '')
        note = (f"# {source['title']}\n\nSource: {source['url']}\n\n"
                f"Authors/publisher: {authors or source['title'].split(' — ')[0]}\n\n"
                f"Type: {source['kind']}\n\nReuse: {source['license']}\n\n"
                f"Location checked: {source['locator']}\n\n"
                f"{source['review_status']} — accessed {source['accessed']}.\n\n"
                f"## Reading note\n\n{passage['en']}\n\n{passage['zh']}\n\n"
                f"Runtime retrieval: {'eligible' if passage['eligible'] else 'excluded; critical reading only'}.\n")
        (notes / name).write_text(note)
        material = f"[Full text]({source['fulltext']}) + note" if source.get('fulltext') else 'Note + source link'
        index.append(f"| [{source['title']}](notes/{name}) | {source['kind']} | {material} |")
    (ROOT / 'SOURCE_INDEX.md').write_text('\n'.join(index) + '\n')
    print(f"Built {len(passages)} notes, {len(sources)} sources: {payload['version'][:12]}")

if __name__ == '__main__':
    build()
