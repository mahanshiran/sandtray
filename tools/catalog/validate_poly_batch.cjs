// NODE_PATH=/path/to/node_modules node validate_poly_batch.cjs /path/to/batch
// Requires the Khronos gltf-validator npm package.
const fs = require('node:fs');
const path = require('node:path');
const validator = require('gltf-validator');

(async () => {
  const root = path.resolve(process.argv[2]);
  const manifest = JSON.parse(fs.readFileSync(path.join(root, 'manifest.json'), 'utf8'));
  const results = [];
  for (const model of manifest.models) {
    const data = fs.readFileSync(path.join(root, model.file + '.glb'));
    const report = await validator.validateBytes(new Uint8Array(data), {
      uri: model.file + '.glb', maxIssues: 1000,
    });
    results.push({ index: model.index, title: model.title,
      validator_version: validator.version(), ...report.issues });
    fs.writeFileSync(path.join(root, 'gltf-validation.json'), JSON.stringify(results, null, 2) + '\n');
    console.log(`${model.index}: ${model.title}: ${report.issues.numErrors} errors, ${report.issues.numWarnings} warnings`);
  }
  process.exitCode = results.some(r => r.numErrors > 0) ? 1 : 0;
})().catch(error => { console.error(error); process.exitCode = 2; });
