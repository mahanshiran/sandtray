"""Run via Django manage.py shell in the existing catalog server container.

SANDTRAY_POLY_BATCH must point to a staged batch directory. All source files,
per-model provenance, and successful Unity verification must already be present.
Adds one entry per transaction; reruns recover by source identity and SHA-256.
"""
import hashlib
import json
import os
import struct
from pathlib import Path

from django.core.files.base import ContentFile
from django.db import transaction
from catalogs.models import Catalog, CatalogAsset, CatalogObject


root = Path(os.environ['SANDTRAY_POLY_BATCH'])
manifest = json.loads((root / 'manifest.json').read_text())
verified = json.loads((root / 'unity-verification.json').read_text())
complete = json.loads((root / 'unity-complete.json').read_text())
validation = json.loads((root / 'gltf-validation.json').read_text())
expected = len(manifest['models'])
assert expected > 0 and complete == {'total': expected, 'failures': 0}, complete
assert len(verified) == expected
assert len(validation) == expected and all(check['numErrors'] == 0 for check in validation)
assert manifest['creator'] == 'Poly by Google' and manifest['license'] == 'CC BY 3.0'
catalog = Catalog._default_manager.get(pk=manifest['catalog_id'], is_default=True)

# Prepare everything before writing to the catalog.
prepared = []
for model, check in zip(manifest['models'], verified):
    assert check['status'] == 'passed' and check['title'] == model['title']
    if manifest.get('require_sandtray_checks'):
        assert check['ground_placement_rotations_passed'] == 4
        assert check['selection_raycast_rotations_passed'] == 4
        pivot = check['normalized_pivot_offset']
        assert max(abs(pivot['x']), abs(pivot['z'])) < .001
        assert abs(pivot['y'] - check['normalized_size']['y'] / 2) < .001
    assert model['creator'] == 'Poly by Google' and model['license'] == 'CC BY 3.0'
    if model.get('original_file'):
        assert hashlib.sha256((root / model['original_file']).read_bytes()).hexdigest() == model['source_sha256']
    glb = (root / (model['file'] + '.glb')).read_bytes()
    png = (root / (model['file'] + '.png')).read_bytes()
    assert struct.unpack_from('<4sII', glb) == (b'glTF', 2, len(glb))
    digest = hashlib.sha256(glb).hexdigest()
    assert digest == model['sha256']
    assert png.startswith(b'\x89PNG\r\n\x1a\n')
    assert struct.unpack_from('>II', png, 16) == (512, 512)
    assert len(glb) <= 25 * 1024 * 1024
    prepared.append((model, glb, png, digest))

backup = root / 'catalog-before.json'
if not backup.exists():
    backup.write_text(json.dumps(list(catalog.objects.values()), default=str, indent=2))
results = []
for model, glb, png, digest in prepared:
    display_name = model.get('display_name', model['title'])
    stored_files = []
    try:
        with transaction.atomic():
            Catalog._default_manager.select_for_update().get(pk=catalog.pk)
            existing = catalog.objects.filter(model_hash=digest).first()
            if existing:
                # Reuse exact bytes; do not add an alias duplicate.
                result = {'index': model['index'], 'title': model['title'],
                          'id': str(existing.pk), 'status': 'already_present',
                          'sha256': digest, 'model': existing.model_url,
                          'thumbnail': existing.thumbnail_url}
            else:
                if catalog.objects.filter(display_name__iexact=display_name).exists():
                    raise RuntimeError('Name collision: ' + model['title'])
                if catalog.objects.filter(description__contains=model['source_url']).exists():
                    raise RuntimeError('Source already imported with different bytes: ' + model['title'])
                urls = {}
                for kind, data, ext, content_type in (
                    ('model', glb, '.glb', 'model/gltf-binary'),
                    ('thumbnail', png, '.png', 'image/png'),
                ):
                    asset = CatalogAsset(owner_id=catalog.owner_id, kind=kind,
                        original_name=model['file'] + ext, content_type=content_type,
                        size_bytes=len(data), sha256=hashlib.sha256(data).hexdigest())
                    asset.file.save(model['file'] + ext, ContentFile(data), save=True)
                    stored_files.append((asset.file.storage, asset.file.name))
                    urls[kind] = 'https://api.sandtraypro.com/api/catalogs/files/' + str(asset.pk) + '/'
                credit = (f"{model['title']} by {manifest['creator']} ({manifest['creator_url']}), "
                    f"via Poly Pizza: {model['source_url']}. {manifest['license']} "
                    f"({manifest['license_url']}). {model.get('changes', manifest['changes'])}")
                obj = CatalogObject._default_manager.create(catalog=catalog,
                    display_name=display_name, category=model['category'],
                    tags=list(dict.fromkeys(model['tags']))[:30], description=credit,
                    model_url=urls['model'], thumbnail_url=urls['thumbnail'], model_hash=digest)
                result = {'index': model['index'], 'title': model['title'],
                          'id': str(obj.pk), 'status': 'created', 'sha256': digest, **urls}
    except Exception:
        for storage, name in stored_files:
            storage.delete(name)
        raise
    results.append(result)
    # An interrupted run can resume by hash even between DB commit and this journal.
    (root / 'result.json').write_text(json.dumps(results, indent=2) + '\n')
    print(json.dumps(result), flush=True)
print(json.dumps({'completed': len(results), 'catalog_count': catalog.objects.count()}), flush=True)
