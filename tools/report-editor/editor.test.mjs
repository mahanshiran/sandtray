import test from 'node:test';import assert from 'node:assert/strict';import {JSDOM} from 'jsdom';
import {cleanHTML,bridgePayload} from './document.mjs';
test('keeps standard formatting while removing executable content and remote images',()=>{
 const window=new JSDOM('').window;
 const html=cleanHTML(window,'<h2>Report</h2><p><strong>你好</strong><span style="color:rgb(255, 0, 0);font-size:24px;position:fixed" onclick="alert(1)">red</span><img src="https://external.test/tracker"><script>alert(1)</script></p>');
 assert.match(html,/<h2>Report<\/h2>/);assert.match(html,/<strong>你好<\/strong>/);assert.match(html,/color:rgb\(255, 0, 0\)/);assert.doesNotMatch(html,/script|onclick|position:|external.test/);
});
test('embeds supported raster images and strips SVG payloads',()=>{
 const window=new JSDOM('').window;
 const html=cleanHTML(window,'<img src="data:image/png;base64,aGVsbG8="><img src="data:image/svg+xml;base64,aGVsbG8=">');
 assert.equal((html.match(/<img/g)||[]).length,1);
});
test('chunked bridge preserves images, Unicode and quotation marks',()=>{
 const payload={html:'<p>你好 "quoted"</p>'+ 'x'.repeat(100000)},packets=[];
 bridgePayload(payload,message=>packets.push(JSON.parse(message)));
 assert.equal(packets[0].type,'begin');assert.equal(packets.at(-1).type,'end');
 assert.deepEqual(JSON.parse(packets.filter(p=>p.type==='part').map(p=>p.data).join('')),payload);
});
