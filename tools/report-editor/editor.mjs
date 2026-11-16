import 'core-js/actual/array/at';
import 'core-js/actual/array/find-last';
import 'core-js/actual/object/has-own';
import 'core-js/actual/string/replace-all';
import { Editor, Extension } from '@tiptap/core';
import { Plugin } from '@tiptap/pm/state';
import StarterKit from '@tiptap/starter-kit';
import Image from '@tiptap/extension-image';
import { TextStyleKit } from '@tiptap/extension-text-style';
import TextAlign from '@tiptap/extension-text-align';
import { TableKit } from '@tiptap/extension-table';
import { cleanHTML, bridgePayload, MAX_IMAGE_BYTES, MAX_DOCUMENT_BYTES } from './document.mjs';

function hasContent(editor){let found=false;editor.state.doc.descendants(node=>{if(node.type.name==='image'||node.type.name==='paragraph'&&node.textContent.trim())found=true;});return found;}
function plain(editor){const text=editor.getText({blockSeparator:'\n\n'});return text.trim()||!editor.getHTML().includes('<img')?text:'[Image]';}
let active, publicEditor, editing=false, suppressed=false, snapshotTimer, readOnly=false;
const notice=document.querySelector('#notice');
const send=value=>{ if(window.Unity?.call)window.Unity.call(encodeURIComponent(JSON.stringify(value))); };
function snapshot(request='') {
  if(!publicEditor)return;
  bridgePayload({type:'snapshot',request,hasContent:hasContent(publicEditor),html:publicEditor.getHTML(),text:hasContent(publicEditor)?plain(publicEditor):'',
    privateHtml:'',privateText:''},msg=>window.Unity.call(encodeURIComponent(msg)));
}
function changed(){
  if(suppressed)return;
  send({type:'dirty'});clearTimeout(snapshotTimer);snapshotTimer=setTimeout(()=>{try{snapshot();}catch(error){notice.textContent=error.message;}},500);
}
function focus(editor){active=editor;editing=true;send({type:'focus',focused:true});refreshToolbar();}
const DocumentLimits=Extension.create({name:'documentLimits',addProseMirrorPlugins(){return [new Plugin({filterTransaction(transaction){
  if(!transaction.docChanged||suppressed)return true;
  if(JSON.stringify(transaction.doc.toJSON()).length>MAX_DOCUMENT_BYTES || transaction.doc.textContent.length>90000){
    notice.textContent='Report limit reached. Remove some text or images before adding more.';return false;
  }
  return true;
}})];}});
function makeEditor(element){return new Editor({element,
  extensions:[DocumentLimits,StarterKit.configure({link:false}),Image.configure({allowBase64:true,resize:{enabled:true,directions:['top-left','top-right','bottom-left','bottom-right'],minWidth:48,minHeight:32,alwaysPreserveAspectRatio:true}}),TextStyleKit,TextAlign.configure({types:['heading','paragraph']}),TableKit.configure({table:{resizable:true}})],
  editorProps:{attributes:{'aria-label':'Report document',spellcheck:'true'},
    handlePaste(view,event){const images=[...(event.clipboardData?.files||[])].filter(f=>f.type.startsWith('image/'));if(!images.length)return false;event.preventDefault();images.forEach(file=>insertImage(file));return true;},
    handleDrop(view,event){const files=[...(event.dataTransfer?.files||[])].filter(f=>f.type.startsWith('image/'));if(!files.length)return false;event.preventDefault();files.forEach(file=>insertImage(file));return true;},
    transformPastedHTML:html=>cleanHTML(window,html)},
  onFocus:({editor})=>focus(editor),onUpdate:changed,onSelectionUpdate:refreshToolbar,
});}
function refreshToolbar(){if(!active)return;for(const button of document.querySelectorAll('[data-mark]')){const checked=active.isActive(button.dataset.mark);button.classList.toggle('selected',checked);button.setAttribute('aria-pressed',String(checked));}}
function command(fn){if(readOnly)return;try{fn(active.chain().focus()).run();refreshToolbar();}catch(e){notice.textContent=e.message;}}
async function insertImage(file){
  if(readOnly)return;
  if(file.size>MAX_IMAGE_BYTES){notice.textContent='Choose an image smaller than 2 MB.';return;}
  if(!/^image\/(png|jpeg|webp)$/.test(file.type)){notice.textContent='Use a PNG, JPEG or WebP image.';return;}
  const data=await new Promise((resolve,reject)=>{const reader=new FileReader();reader.onload=()=>resolve(reader.result);reader.onerror=reject;reader.readAsDataURL(file);});
  const image=document.createElement('img');image.src=data;
  try{await image.decode();}catch{notice.textContent='This image could not be opened. Choose another image.';return;}
  if(image.naturalWidth*image.naturalHeight>16000000){notice.textContent='Choose an image smaller than 16 megapixels.';return;}
  command(chain=>chain.setImage({src:data,alt:file.name||'Report image'}));notice.textContent='';
}
const commands={bold:c=>c.toggleBold(),italic:c=>c.toggleItalic(),underline:c=>c.toggleUnderline(),bulletList:c=>c.toggleBulletList(),orderedList:c=>c.toggleOrderedList(),undo:c=>c.undo(),redo:c=>c.redo(),table:c=>c.insertTable({rows:3,cols:2,withHeaderRow:true}),deleteTable:c=>c.deleteTable(),clear:c=>c.unsetAllMarks().clearNodes()};
for(const button of document.querySelectorAll('[data-command]')){button.addEventListener('mousedown',e=>e.preventDefault());button.onclick=()=>command(commands[button.dataset.command]);}
document.querySelector('#heading').onchange=e=>command(c=>e.target.value==='p'?c.setParagraph():c.toggleHeading({level:Number(e.target.value)}));
document.querySelector('#font').onchange=e=>command(c=>c.setFontFamily(e.target.value));
document.querySelector('#size').onchange=e=>command(c=>c.setFontSize(e.target.value));
const colorButton=document.querySelector('#color'), palette=document.querySelector('#color-palette');
let colorSelection;
colorButton.onmousedown=e=>e.preventDefault();
colorButton.onclick=()=>{
  if(readOnly)return;
  colorSelection={from:active.state.selection.from,to:active.state.selection.to};
  palette.hidden=!palette.hidden;colorButton.setAttribute('aria-expanded',String(!palette.hidden));
  if(!palette.hidden){palette.style.left='0px';const bounds=palette.getBoundingClientRect();palette.style.left=-Math.max(0,bounds.right-window.innerWidth+8)+'px';}
};
function applyColor(value){
  if(!/^#[0-9a-f]{6}$/i.test(value)){notice.textContent='Enter a color such as #FF0000.';return;}
  if(colorSelection)active.commands.setTextSelection(colorSelection);
  command(c=>c.setColor(value));colorButton.style.setProperty('--text-color',value);
  palette.hidden=true;colorButton.setAttribute('aria-expanded','false');notice.textContent='';
}
for(const value of ['#1f2937','#000000','#ffffff','#dc2626','#ea580c','#ca8a04','#16a34a','#0891b2','#2563eb','#7c3aed','#db2777','#64748b']){
  const button=document.createElement('button');button.type='button';button.title=value;button.setAttribute('aria-label','Text color '+value);
  button.style.background=value;button.onmousedown=e=>e.preventDefault();button.onclick=()=>applyColor(value);
  document.querySelector('#color-swatches').appendChild(button);
}
document.querySelector('#apply-color').onclick=()=>applyColor(document.querySelector('#custom-color').value.trim());
document.querySelector('#custom-color').onkeydown=e=>{if(e.key==='Enter'){e.preventDefault();applyColor(e.target.value.trim());}};
document.addEventListener('mousedown',e=>{if(!e.target.closest('#color-control')){palette.hidden=true;colorButton.setAttribute('aria-expanded','false');}});
document.addEventListener('keydown',e=>{if(e.key==='Escape'){palette.hidden=true;colorButton.setAttribute('aria-expanded','false');}});
document.querySelector('#align').onchange=e=>command(c=>c.setTextAlign(e.target.value));
document.querySelector('#image').onclick=()=>document.querySelector('#file').click();
document.querySelector('#file').onchange=e=>{for(const file of e.target.files)insertImage(file);e.target.value='';};
document.addEventListener('focusin',()=>{editing=true;send({type:'focus',focused:true});});
document.addEventListener('focusout',()=>setTimeout(()=>{if(!document.hasFocus()){editing=false;send({type:'focus',focused:false});}},0));
window.addEventListener('blur',()=>{editing=false;send({type:'focus',focused:false});});
window.SandtrayReport={
 load(value){closeFormattingMenu();palette.hidden=true;colorSelection=null;suppressed=true;publicEditor?.destroy();publicEditor=makeEditor(document.querySelector('#public'));active=publicEditor;
   publicEditor.commands.setContent(cleanHTML(window,value.html),{emitUpdate:false});
   readOnly=!!value.readOnly;publicEditor.setEditable(!readOnly);
   document.body.classList.toggle('readonly',readOnly);document.body.classList.toggle('light',!!value.light);
   suppressed=false;send({type:'ready'});snapshot();},
 status(value){document.querySelector('#save-status').textContent=value.text||'';},
 snapshot, suspend(value){document.body.classList.toggle('suspended',value);if(value){closeFormattingMenu();document.activeElement?.blur();send({type:'focus',focused:false});}},
 blur(){document.activeElement?.blur();send({type:'focus',focused:false});},
};

// View-only scaling stays outside the ProseMirror document and bridge payload.
let documentZoom=100;
function fitDocumentWidth(){
  const paper=document.querySelector('#public');
  const viewport=document.querySelector('#document-viewport');
  const styles=getComputedStyle(viewport);
  const available=viewport.clientWidth-parseFloat(styles.paddingLeft)-parseFloat(styles.paddingRight);
  if(available>0&&paper.offsetWidth>0)
    paper.style.zoom=String(available/paper.offsetWidth*documentZoom/100);
}
function setDocumentZoom(value){
  documentZoom=Math.max(50,Math.min(100,value));
  fitDocumentWidth();
  document.querySelector('#zoom-value').textContent=documentZoom+'%';
  document.querySelector('#zoom-out').disabled=documentZoom===50;
  document.querySelector('#zoom-in').disabled=documentZoom===100;
}
for(const [id,delta] of [['zoom-out',-10],['zoom-in',10]]){
  const button=document.querySelector('#'+id);button.onmousedown=e=>e.preventDefault();
  button.onclick=()=>setDocumentZoom(documentZoom+delta);
}
function positionDocumentChrome(){document.documentElement.style.setProperty('--toolbar-height',document.querySelector('#toolbar').getBoundingClientRect().height+'px');}
new ResizeObserver(positionDocumentChrome).observe(document.querySelector('#toolbar'));
// Preserve the A4 document dimensions; adapt only its display scale to the pane.
let lastDocumentViewportWidth=0;
new ResizeObserver(()=>{
  const width=document.querySelector('#document-viewport').clientWidth;
  if(width!==lastDocumentViewportWidth){lastDocumentViewportWidth=width;fitDocumentWidth();}
}).observe(document.querySelector('#document-viewport'));
setDocumentZoom(100);positionDocumentChrome();

// Native <select> popups use the offscreen macOS host window's coordinates.
// Render formatting menus inside the WebView so they remain beside the button.
let openFormattingMenu=null;
function closeFormattingMenu(){
  if(!openFormattingMenu)return;
  openFormattingMenu.menu.hidden=true;
  openFormattingMenu.button.setAttribute('aria-expanded','false');
  openFormattingMenu=null;
}
for(const select of document.querySelectorAll('#toolbar select')){
  const button=document.createElement('button');button.type='button';button.id=select.id;
  button.className='format-dropdown';button.title=select.title;button.setAttribute('aria-label',select.title);
  button.setAttribute('aria-haspopup','listbox');button.setAttribute('aria-expanded','false');
  const menu=document.createElement('div');menu.className='format-menu';menu.id=select.id+'-menu';
  menu.setAttribute('role','listbox');menu.setAttribute('aria-label',select.title);menu.hidden=true;
  button.setAttribute('aria-controls',menu.id);
  let selection;
  function choose(option){
    if(readOnly)return;
    if(selection)active.commands.setTextSelection(selection);
    select.value=option.value;select.onchange?.({target:select});
    button.textContent=option.textContent+' ▾';
    for(const item of menu.children)item.setAttribute('aria-selected',String(item.dataset.value===option.value));
    closeFormattingMenu();
  }
  for(const option of select.options){
    const item=document.createElement('button');item.type='button';item.setAttribute('role','option');
    item.dataset.value=option.value;item.textContent=option.textContent;
    item.setAttribute('aria-selected',String(option.selected));item.tabIndex=-1;
    if(select.id==='font')item.style.fontFamily=option.value;
    item.onmousedown=e=>e.preventDefault();item.onclick=()=>choose(option);
    item.onkeydown=e=>{
      if(e.key==='ArrowDown'||e.key==='ArrowUp'){
        e.preventDefault();const items=[...menu.children];items[(items.indexOf(item)+(e.key==='ArrowDown'?1:items.length-1))%items.length].focus();
      }else if(e.key==='Escape'){e.preventDefault();closeFormattingMenu();button.focus();}
      else if(e.key==='Tab')closeFormattingMenu();
    };
    menu.appendChild(item);
  }
  button.textContent=select.selectedOptions[0].textContent+' ▾';
  button.onmousedown=e=>e.preventDefault();
  function open(){
    if(readOnly)return;
    const wasOpen=openFormattingMenu?.button===button;closeFormattingMenu();if(wasOpen)return;
    palette.hidden=true;colorButton.setAttribute('aria-expanded','false');
    selection={from:active.state.selection.from,to:active.state.selection.to};
    menu.hidden=false;button.setAttribute('aria-expanded','true');
    const bounds=button.getBoundingClientRect();
    menu.style.left=Math.max(6,Math.min(bounds.left,window.innerWidth-menu.offsetWidth-6))+'px';
    menu.style.top=Math.max(6,Math.min(bounds.bottom+4,window.innerHeight-menu.offsetHeight-6))+'px';
    openFormattingMenu={button,menu};
  }
  button.onclick=open;
  button.onkeydown=e=>{if(e.key==='ArrowDown'||e.key==='ArrowUp'){e.preventDefault();if(openFormattingMenu?.button!==button)open();menu.querySelector('[aria-selected=true]')?.focus();}else if(e.key==='Escape')closeFormattingMenu();};
  select.replaceWith(button);document.body.appendChild(menu);
}
document.addEventListener('mousedown',e=>{if(openFormattingMenu&&!openFormattingMenu.menu.contains(e.target)&&e.target!==openFormattingMenu.button)closeFormattingMenu();});
window.addEventListener('resize',closeFormattingMenu);window.addEventListener('blur',closeFormattingMenu);
document.addEventListener('scroll',e=>{if(!e.target.closest?.('.format-menu'))closeFormattingMenu();},true);

document.addEventListener('keydown',e=>{if(e.key==='Escape'||e.key==='Tab')closeFormattingMenu();});
