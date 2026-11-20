// Assemble the design deliverables; this is not a build system for the proposed IDE.
import { readFile, writeFile, mkdir, copyFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { classicArtwork } from './classic-glyphs.mjs';
const dir=path.dirname(fileURLToPath(import.meta.url));
const read=p=>readFile(path.join(dir,p),'utf8');
const [markup,presentation,workStyle,termStyle,term,work,controller,logo]=await Promise.all(['src/frame.html','src/presentation.css','src/workbench.css','src/console.css','src/console.js','src/workbench.js','src/controller.js','icons/mini-cpp.svg'].map(read));
const opts=process.argv.slice(2);
if(opts.includes('--import-icons')) {
  const vendor=path.resolve(dir,'../.review-tools/node_modules/lucide-static');
  const db=JSON.parse(await readFile(path.join(vendor,'icon-nodes.json'),'utf8'));
  const names=new Set([...((markup+work+term+controller).matchAll(/(?:data-lucide="|')([a-z][a-z0-9-]*)(?:"|')/g))].map(m=>m[1]).filter(n=>Object.hasOwn(db,n)));
  const subset=Object.fromEntries([...names].sort().map(n=>[n,db[n]]));
  await mkdir(path.join(dir,'vendor'),{recursive:true});
  await writeFile(path.join(dir,'vendor/lucide-subset.json'),JSON.stringify(subset,null,2)+'\n');
  await copyFile(path.join(vendor,'LICENSE'),path.join(dir,'vendor/LUCIDE-LICENSE.txt'));
}
const classic=classicArtwork();
await Promise.all(Object.entries(classic.files).map(([file,svg])=>writeFile(path.join(dir,'icons',file),svg)));
const subset=JSON.stringify(JSON.parse(await read('vendor/lucide-subset.json')));
const resources=`<style>\n${presentation}\n${workStyle}\n${termStyle}\n${classic.css}\n</style>`;
const fragment=`${markup}\n${resources}\n<script>\n(() => {\n'use strict';\nconst classicGlyphs=${JSON.stringify(classic.html)};\n${term}\n${work}\n${controller}\n})();\n</script>\n`;
if(opts.includes('--inline')) {
  const p=opts[opts.indexOf('--inline')+1];
  if(!p || !path.isAbsolute(p)) throw new Error('Provide an absolute path for the inline preview.');
  await writeFile(p,fragment);
}
// A small adapter paints the licensed Lucide node data in offline browsers.
const adapter=`const nodes=${subset};
window.lucide={createIcons(){const ns='http://www.w3.org/2000/svg'; for(const i of document.querySelectorAll('#mini-cpp-review i[data-lucide]')) {
 const name=i.dataset.lucide, data=nodes[name]; if(!data)continue;
 const svg=document.createElementNS(ns,'svg'); for(const [k,v] of Object.entries({class:'lucide lucide-'+name,'data-lucide':name,viewBox:'0 0 24 24',fill:'none',stroke:'currentColor','stroke-width':'2','stroke-linecap':'round','stroke-linejoin':'round','aria-hidden':'true'}))svg.setAttribute(k,v);
 for(const [tag,attrs] of data){const el=document.createElementNS(ns,tag);for(const [k,v] of Object.entries(attrs))el.setAttribute(k,v);svg.append(el);} i.replaceWith(svg);
}}};`;
const reviewId=markup.match(/class="mc-rid">(R\d+)</)[1];
const favicon='data:'+'image/svg+xml,'+encodeURIComponent(logo);
const standalone=`<!doctype html>\n<html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><meta name="color-scheme" content="light dark"><title>Mini C++ · 设计审查 ${reviewId}</title><link rel="icon" href="${favicon}"><style>html{color-scheme:light dark}body{margin:0;background:light-dark(#eef2f0,#131816);padding:clamp(10px,3.8vw,50px) clamp(8px,3vw,40px)}main{max-width:1152px;margin:0 auto}@media(max-width:570px){body{padding:9px 4px}}</style></head><body><main><script>(()=>{${adapter}})()</script>\n${fragment}</main></body></html>\n`;
await writeFile(path.join(dir,'review.html'),standalone);
console.log('Offline design:',Buffer.byteLength(standalone),'bytes. Inline design:',Buffer.byteLength(fragment),'bytes.');
