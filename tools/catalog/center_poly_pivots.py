"""Center placement pivots using measured Unity bounds; preserve source GLBs.

Run after an initial RenderPolyBatch pass and before the final render/validation.
Only a glTF scene wrapper translation changes. Mesh/material/texture bytes remain
unchanged. Rerunning restores from originals and produces the same prepared copy.
"""
import hashlib
import json
import shutil
import struct
import sys
from pathlib import Path


def center_glb(data, translation):
    magic, version, length = struct.unpack_from('<4sII', data)
    assert (magic, version, length) == (b'glTF', 2, len(data))
    chunks = []
    offset = 12
    while offset < len(data):
        size, kind = struct.unpack_from('<II', data, offset)
        chunks.append((kind, data[offset + 8:offset + 8 + size]))
        offset += 8 + size
    assert chunks[0][0] == 0x4e4f534a
    document = json.loads(chunks[0][1])
    scene = document['scenes'][document.get('scene', 0)]
    roots = scene['nodes']
    wrapper = {'name': 'Sandtray placement origin', 'children': roots,
               'translation': translation}
    index = len(document['nodes'])
    document['nodes'].append(wrapper)
    scene['nodes'] = [index]
    encoded = json.dumps(document, ensure_ascii=False, separators=(',', ':')).encode('utf-8')
    encoded += b' ' * (-len(encoded) % 4)
    chunks[0] = (0x4e4f534a, encoded)
    body = b''.join(struct.pack('<II', len(chunk), kind) + chunk for kind, chunk in chunks)
    return struct.pack('<4sII', b'glTF', 2, 12 + len(body)) + body


def main():
    root = Path(sys.argv[1])
    manifest = json.loads((root / 'manifest.json').read_text())
    measurements_path = root / 'unity-before-pivot-centering.json'
    if not measurements_path.exists():
        shutil.copyfile(root / 'unity-verification.json', measurements_path)
    checks = {x['index']: x for x in json.loads(measurements_path.read_text())}
    originals = root / 'originals'
    originals.mkdir(exist_ok=True)
    changes = ('Original downloaded GLB retained unchanged in the source archive. '
               'Published copy has its placement origin centered horizontally at the '
               'base using a scene wrapper; mesh geometry, materials and textures are '
               'unchanged. Preview rendered in Unity; application scales the model.')
    for model in manifest['models']:
        check = checks[model['index']]
        assert check['status'] == 'passed'
        working = root / (model['file'] + '.glb')
        original = originals / working.name
        if not original.exists():
            shutil.copyfile(working, original)
        raw = original.read_bytes()
        offset = check['normalized_pivot_offset']
        size = check['normalized_size']
        scale = .6 / check['source_max_dimension']
        # UnityGLTF mirrors glTF X; the remaining axes use the same sign.
        translation = [offset['x'] / scale,
                       -(offset['y'] - size['y'] * .5) / scale,
                       -offset['z'] / scale]
        prepared = center_glb(raw, translation)
        working.write_bytes(prepared)
        model.update(source_sha256=hashlib.sha256(raw).hexdigest(),
                     source_size_bytes=len(raw), original_file='originals/' + working.name,
                     sha256=hashlib.sha256(prepared).hexdigest(), size_bytes=len(prepared),
                     placement_translation_gltf=translation, changes=changes)
        print(f"{model['index']}: centered {model['title']}", flush=True)
    manifest['changes'] = changes
    manifest['require_sandtray_checks'] = True
    (root / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')


if __name__ == '__main__':
    main()
