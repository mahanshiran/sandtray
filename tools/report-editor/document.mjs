import createDOMPurify from 'dompurify';
export const MAX_IMAGE_BYTES = 2 * 1024 * 1024;
export const MAX_DOCUMENT_BYTES = 6 * 1024 * 1024;
export function cleanHTML(window, html) {
  const purifier = createDOMPurify(window);
  const clean = purifier.sanitize(html || '', {
    ALLOWED_TAGS: ['p','br','h1','h2','h3','strong','b','em','i','u','s','span','ul','ol','li','blockquote','hr','img','table','tbody','thead','tr','td','th','colgroup','col'],
    ALLOWED_ATTR: ['style','src','alt','width','height','colspan','rowspan','colwidth'],
    ALLOW_DATA_ATTR: false,
  });
  const root = window.document.createElement('div'); root.innerHTML = clean;
  for (const image of root.querySelectorAll('img')) {
    if (!/^data:image\/(png|jpeg|webp);base64,[a-z0-9+/=]+$/i.test(image.getAttribute('src') || '')) image.remove();
  }
  for (const node of root.querySelectorAll('[style]')) {
    const allowed = ['color','background-color','font-family','font-size','text-align'];
    const style = allowed.map(key => {
      const value = node.style.getPropertyValue(key);
      return value && !/url|expression|[<>]/i.test(value) ? `${key}:${value}` : '';
    }).filter(Boolean).join(';');
    if (style) node.setAttribute('style', style); else node.removeAttribute('style');
  }
  return root.innerHTML;
}
export function bridgePayload(value, send) {
  const data = JSON.stringify(value), id = String(Date.now()) + Math.random().toString(36).slice(2);
  if (data.length > MAX_DOCUMENT_BYTES * 2) throw new Error('Report is too large. Remove some images.');
  send(JSON.stringify({type:'begin',id,length:data.length}));
  for (let i=0;i<data.length;i+=24000) send(JSON.stringify({type:'part',id,data:data.slice(i,i+24000)}));
  send(JSON.stringify({type:'end',id}));
}
