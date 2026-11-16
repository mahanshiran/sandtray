#!/usr/bin/env python3
"""Measure and stage the public catalog as deterministic, individually gzipped GLBs.

No credentials required. Outputs outside Assets by default; review size-report.json
before copying manifest.json and models/ into the Unity project.
"""
import argparse
import concurrent.futures
import gzip
import hashlib
import json
from pathlib import Path
import struct
import time
from urllib.request import urlopen

EXCLUDED_IDS = {
    "c05af8c8-b81f-4755-8661-f178650b3e9f",  # Fence
    "9db81f0f-a902-4231-a1e9-3bbaafa91972",  # Picket Fence
}
FIELDS = ("id", "catalog", "display_name", "category", "model_url",
          "thumbnail_url", "model_hash", "description", "tags", "objectSize")


def validate_glb(data, expected_hash):
    if hashlib.sha256(data).hexdigest() != expected_hash:
        raise ValueError("SHA-256 mismatch")
    magic, version, length = struct.unpack_from("<4sII", data)
    if magic != b"glTF" or version != 2 or length != len(data):
        raise ValueError("Invalid GLB header")
    json_length, kind = struct.unpack_from("<II", data, 12)
    if kind != 0x4E4F534A:
        raise ValueError("Missing GLB JSON chunk")
    document = json.loads(data[20:20 + json_length])
    for resource in document.get("buffers", []) + document.get("images", []):
        uri = resource.get("uri", "")
        if uri and not uri.startswith("data:"):
            raise ValueError("External resource: " + uri)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--cache", type=Path)
    parser.add_argument("--endpoint", default="https://api.sandtraypro.com/api/catalogs/public/")
    args = parser.parse_args()
    with urlopen(args.endpoint, timeout=60) as response:
        items = json.load(response)["objects"]
    # Keep optional placement metadata when refreshing the bundled snapshot.
    rules_path = Path(__file__).resolve().parents[2] / "Assets/Resources/CatalogSizing/rules.json"
    rules = json.loads(rules_path.read_text()) if rules_path.exists() else {}
    category_sizes = {r["name"].casefold(): r["objectSize"] for r in rules.get("categories", [])}
    name_sizes = {}
    for group, size in (("normal", 1), ("medium", 2), ("large", 3)):
        name_sizes.update({name.casefold(): size for name in rules.get(group, [])})
    current_manifest = Path(__file__).resolve().parents[2] / "Assets/Resources/BundledCatalog/manifest.json"
    current_sizes = {i["id"]: i["objectSize"] for i in json.loads(current_manifest.read_text()).get("objects", [])
                     if type(i.get("objectSize")) is int and i["objectSize"] in (1, 2, 3)} if current_manifest.exists() else {}
    for item in items:
        if "objectSize" not in item:
            item["objectSize"] = current_sizes.get(item["id"], name_sizes.get(item.get("display_name", "").casefold(), category_sizes.get(item.get("category", "").casefold(), 1)))
        if type(item["objectSize"]) is not int or item["objectSize"] not in (1, 2, 3):
            item["objectSize"] = 1
    args.output.mkdir(parents=True, exist_ok=True)
    models = args.output / "models"
    models.mkdir(exist_ok=True)

    def stage(item):
        digest = item["model_hash"]
        if len(digest) != 64 or any(c not in "0123456789abcdef" for c in digest):
            raise ValueError("Missing/invalid model hash: " + item["display_name"])
        data = None
        cached = args.cache / (digest + ".glb") if args.cache else None
        if cached and cached.exists():
            candidate = cached.read_bytes()
            if hashlib.sha256(candidate).hexdigest() == digest:
                data = candidate
        path = models / (digest + ".glb.gz")
        if data is None and path.exists():
            candidate = gzip.decompress(path.read_bytes())
            if hashlib.sha256(candidate).hexdigest() == digest:
                data = candidate
        if data is None:
            for attempt in range(3):
                try:
                    with urlopen(item["model_url"], timeout=120) as response:
                        data = response.read()
                    break
                except Exception:
                    if attempt == 2:
                        raise
                    time.sleep(attempt + 1)
        validate_glb(data, digest)
        compressed = gzip.compress(data, compresslevel=9, mtime=0)
        if gzip.decompress(compressed) != data:
            raise ValueError("Compression round-trip failed")
        path.write_bytes(compressed)
        return {"id": item["id"], "name": item["display_name"],
                "sha256": digest, "raw_bytes": len(data),
                "bundled_bytes": len(compressed), "excluded": item["id"] in EXCLUDED_IDS}

    with concurrent.futures.ThreadPoolExecutor(max_workers=8) as executor:
        results = list(executor.map(stage, items))
    included = [r for r in results if not r["excluded"]]
    manifest = {"objects": [{k: item.get(k) for k in FIELDS}
                             for item in items if item["id"] not in EXCLUDED_IDS]}
    (args.output / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, separators=(",", ":")))
    report = {"source_count": len(items), "bundled_count": len(included),
              "source_raw_bytes": sum(r["raw_bytes"] for r in results),
              "source_gzip_bytes": sum(r["bundled_bytes"] for r in results),
              "bundled_raw_bytes": sum(r["raw_bytes"] for r in included),
              "bundled_gzip_bytes": sum(r["bundled_bytes"] for r in included),
              "manifest_bytes": (args.output / "manifest.json").stat().st_size,
              "objects": results}
    (args.output / "size-report.json").write_text(json.dumps(report, indent=2))
    print(json.dumps({k: v for k, v in report.items() if k != "objects"}, indent=2))


if __name__ == "__main__":
    main()
