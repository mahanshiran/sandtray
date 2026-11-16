"""Verify a published batch via the same public HTTPS API used by the app."""
import hashlib
import json
import time
from pathlib import Path
from urllib.request import Request, urlopen
import sys


def download(url):
    if not url.startswith('https://api.sandtraypro.com/'):
        raise ValueError('Unexpected verification host: ' + url)
    for attempt in range(3):
        try:
            with urlopen(Request(url, headers={'User-Agent': 'Sandtray-Catalog-Verification'}), timeout=60) as response:
                assert response.status == 200
                return response.read()
        except Exception:
            if attempt == 2:
                raise
            time.sleep(1)


def main():
    root = Path(sys.argv[1])
    manifest = json.loads((root / 'manifest.json').read_text())
    published = json.loads((root / 'result.json').read_text())
    before = json.loads((root / 'catalog-before.json').read_text())
    live = json.loads(download('https://api.sandtraypro.com/api/catalogs/public/'))['objects']
    by_id = {entry['id']: entry for entry in live}
    fields = ('display_name', 'category', 'tags', 'model_url', 'thumbnail_url',
              'model_hash', 'description', 'age_range_min', 'age_range_max', 'cultural_tags')
    for entry in before:
        current = by_id[entry['id']]
        assert all(current[key] == entry[key] for key in fields), entry['display_name']
    result = {'previous_entries_unchanged': len(before), 'public_catalog_count': len(live), 'models': []}
    for model, entry in zip(manifest['models'], published):
        assert model['title'] == entry['title']
        current = by_id[entry['id']]
        assert current['display_name'] == model.get('display_name', model['title'])
        assert current['category'] == model['category']
        assert model['source_url'] in current['description']
        assert 'Poly by Google' in current['description'] and 'CC BY 3.0' in current['description']
        if model.get('changes'):
            assert model['changes'] in current['description']
        assert current['model_hash'] == model['sha256']
        model_bytes = download(current['model_url'])
        assert hashlib.sha256(model_bytes).hexdigest() == model['sha256']
        image_bytes = download(current['thumbnail_url'])
        assert image_bytes == (root / (model['file'] + '.png')).read_bytes()
        result['models'].append({'index': model['index'], 'title': model['title'],
            'id': entry['id'], 'status': 'passed', 'model_sha256': model['sha256'],
            'thumbnail_sha256': hashlib.sha256(image_bytes).hexdigest()})
        (root / 'live-verification.json').write_text(json.dumps(result, indent=2) + '\n')
        print(f"{model['index']}/{len(manifest['models'])} live verified: {model['title']}", flush=True)
    assert len(result['models']) == len(published) == len(manifest['models'])
    print(f"All {len(published)} live models verified; {len(before)} previous entries unchanged; {len(live)} total entries.", flush=True)


if __name__ == '__main__':
    main()
