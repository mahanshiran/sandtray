import {build} from 'esbuild';import fs from 'node:fs';import path from 'node:path';
const root=path.dirname(new URL(import.meta.url).pathname);
const result=await build({entryPoints:[path.join(root,'editor.mjs')],bundle:true,write:false,minify:true,format:'iife',target:['safari13','chrome90'],legalComments:'eof'});
const html=fs.readFileSync(path.join(root,'index.html'),'utf8').replace('__CSS__',()=>fs.readFileSync(path.join(root,'editor.css'),'utf8')).replace('__JS__',()=>result.outputFiles[0].text.replaceAll('</script','<\\/script'));
fs.writeFileSync(path.join(root,'../../Assets/StreamingAssets/ReportEditor/index.html'),html);
// Ship the dependency licenses with the offline editor, including transitive modules.
const lock=JSON.parse(fs.readFileSync(path.join(root,'package-lock.json'),'utf8'));
const notices=[];
for(const [name,info] of Object.entries(lock.packages||{})){
  if(!name||info.dev)continue;
  const folder=path.join(root,name);
  const license=fs.readdirSync(folder).find(file=>/^licen[sc]e(\.|$)/i.test(file));
  if(license)notices.push(`${name.replace('node_modules/','')} ${info.version}\n${fs.readFileSync(path.join(folder,license),'utf8')}`);
}
fs.writeFileSync(path.join(root,'../../Assets/StreamingAssets/ReportEditor/THIRD_PARTY_NOTICES.txt'),notices.join('\n\n--------------------\n\n'));
