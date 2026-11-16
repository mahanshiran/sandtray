"""Prepare a resumable, sequential Poly by Google catalog import.

Usage: python prepare_poly_batch.py /path/to/batch-directory
The directory must contain shortlist.json with Poly Pizza profile records.
This downloads public model files; it does not publish catalog entries.
"""
import hashlib
import json
import re
import struct
import subprocess
import sys
from pathlib import Path


def fetch(url, destination):
    if destination.exists() and destination.stat().st_size:
        return
    temporary = destination.with_suffix(destination.suffix + '.part')
    subprocess.run(['curl', '--fail', '--location', '--silent', '--show-error',
                    '--retry', '3', '--retry-delay', '2', '--max-time', '90',
                    '--user-agent', 'Mozilla/5.0', url, '-o', str(temporary)], check=True)
    temporary.replace(destination)


def inspect_glb(path):
    data = path.read_bytes()
    if len(data) < 20 or struct.unpack_from('<4sII', data) != (b'glTF', 2, len(data)):
        raise ValueError('Invalid GLB header: ' + str(path))
    length, kind = struct.unpack_from('<II', data, 12)
    if kind != 0x4e4f534a:
        raise ValueError('Missing GLB JSON chunk')
    document = json.loads(data[20:20 + length])
    if not document.get('meshes'):
        raise ValueError('Model contains no meshes')
    for resource in document.get('buffers', []) + document.get('images', []):
        uri = resource.get('uri', '')
        if uri and not uri.startswith('data:'):
            raise ValueError('External resource is not embedded: ' + uri)
    return {'sha256': hashlib.sha256(data).hexdigest(), 'size_bytes': len(data),
            'mesh_count': len(document['meshes']),
            'animations': len(document.get('animations', [])),
            'extensions_required': document.get('extensionsRequired', [])}


def category_for(index):
    if index <= 20 or 51 <= index <= 60:
        return 'animals'
    if 21 <= index <= 26 or 69 <= index <= 72 or index == 74:
        return 'buildings'
    if 27 <= index <= 34 or 61 <= index <= 68:
        return 'nature'
    if 45 <= index <= 50 or 96 <= index <= 100:
        return 'vehicles'
    return 'abstract'


def main():
    root = Path(sys.argv[1]).resolve()
    shortlist = json.loads((root / 'shortlist.json').read_text())
    manifest = {
        'catalog_id': 'aac4b15a-00c2-496b-8126-f40896ee3415',
        'creator': 'Poly by Google',
        'creator_url': 'https://poly.pizza/u/Poly%20by%20Google',
        'license': 'CC BY 3.0',
        'license_url': 'https://creativecommons.org/licenses/by/3.0/',
        'changes': ('Original downloaded GLB files retained unchanged. Preview images '
                    'rendered in Unity; application scales models for display.'),
        'models': [],
    }
    for index, item in enumerate(shortlist, 1):
        source = 'https://poly.pizza' + item['url']
        stem = f'{index:03d}-' + re.sub(r'[^A-Za-z0-9_-]+', '-', item['title']).strip('-')
        page = root / (stem + '.html')
        fetch(source, page)
        match = re.search(r'window.__SERVER_APP_STATE__\s*=\s*(.*?)</script>',
                          page.read_text(), re.S)
        if not match:
            raise ValueError('Missing model page metadata: ' + source)
        state = json.loads(match.group(1).strip().rstrip(';'))['initialData']['model']
        if (state['Creator']['Username'] != 'Poly by Google' or
                state['Licence'] != 'CC-BY 3.0' or state['PublicID'] != item['publicID'] or
                state['Title'] != item['title']):
            raise ValueError('Source identity or license mismatch: ' + source)
        resource_id = state['ResourceID']
        glb = root / (stem + '.glb')
        download_url = f'https://static.poly.pizza/{resource_id}.glb'
        fetch(download_url, glb)
        audit = inspect_glb(glb)
        if audit['animations'] or state.get('Animated'):
            raise ValueError('Animated model requires separate review: ' + source)
        fetch(item['previewUrl'], root / (stem + '.source.webp'))
        category = item.get('category', category_for(index))
        if category not in ('animals', 'buildings', 'nature', 'vehicles', 'abstract', 'people', 'mythological', 'religious', 'custom'):
            raise ValueError('Unknown catalog category: ' + category)
        record = {
            'index': index, 'file': stem, 'title': state['Title'], 'source_url': source,
            'display_name': item.get('display_name', state['Title']),
            'public_id': state['PublicID'], 'resource_id': resource_id,
            'download_url': download_url, 'category': category,
            'tags': list(dict.fromkeys([category] + item.get('tags', []) + state.get('Tags', []) +
                                      re.findall(r'[a-z]+', state['Title'].lower()))),
            'source_description': state.get('Description', ''),
            'creator': state['Creator']['Username'], 'license': 'CC BY 3.0',
            'license_url': manifest['license_url'], **audit,
        }
        (root / (stem + '.source.json')).write_text(json.dumps(state, indent=2))
        manifest['models'].append(record)
        (root / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
        print(f'{index}/{len(shortlist)} verified: {record["title"]} '
              f'({audit["size_bytes"]:,} bytes)', flush=True)
    print('Prepared all models. Unity import/render verification is still required.', flush=True)


if __name__ == '__main__':
    main()
