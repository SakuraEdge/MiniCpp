// Optional design checks: install playwright-core and point CHROME_PATH to Chromium.
import { createRequire } from 'node:module';
import assert from 'node:assert/strict';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
const require=createRequire(import.meta.url);
let chromium;try{({chromium}=require('playwright-core'));}catch{({chromium}=require('../.review-tools/node_modules/playwright-core'));}
const dir=path.dirname(fileURLToPath(import.meta.url));
const browser=await chromium.launch({executablePath:process.env.CHROME_PATH||'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
const img=path.join(dir,'screens'); const qa=path.join(dir,'.qa');
const errors=[];
try {
  await Promise.all([mkdir(img,{recursive:true}),mkdir(qa,{recursive:true})]);
  const page=await browser.newPage({viewport:{width:1140,height:810},deviceScaleFactor:1,colorScheme:'light'});
  page.setDefaultTimeout(7000);
  page.on('pageerror',e=>errors.push(e.message));
  await page.goto(pathToFileURL(path.join(dir,'review.html')).href);
  const q=s=>page.locator(s);
  const click=s=>q(s).click();
  const scene=id=>click(`[data-scene="${id}"]`);
  const act=async id=>{const m=q(`[data-dropdown] [data-act="${id}"]:visible`);if(await m.count())return m.click();return q(`[data-act="${id}"]:visible`).first().click();};
  const panel=id=>click(`[data-panel="${id}"]`);
  const text=s=>q(s).innerText();
  const has=async(s,re)=>assert.match(await text(s),re);
  const noMissing=async()=>assert.deepEqual(await q('i[data-lucide]:visible').evaluateAll(nodes=>nodes.map(node=>node.dataset.lucide)),[],'Missing visible glyph');
  const assertFit=async()=>assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true,'Horizontal overflow');
  const capture=async(name)=>{await page.mouse.move(1,1);await q('#mini-cpp-review').screenshot({path:path.join(img,`${name}.png`),animations:'disabled'});};
  assert.deepEqual(await q('[data-menu]').allTextContents(),['文件','编辑','搜索','视图','项目','运行','工具','CVS','窗口','帮助']);
  const glyphs=q('[data-buildtools] button');assert.deepEqual(await glyphs.allTextContents(),['','','','']);
  assert.deepEqual(await glyphs.evaluateAll(bs=>bs.map(b=>[b.offsetWidth,b.offsetHeight])),Array(4).fill([28,28]));
  assert.equal(await q('.mc-tip').count(),0);const f11=q('[data-buildtools] [data-act="buildRun"]'),before=await f11.boundingBox();await f11.hover();await q('.mc-tip').waitFor();await has('.mc-tip',/编译并运行 · F11/);assert.deepEqual(await f11.boundingBox(),before);
  const top=await q('[data-window]').boundingBox(),hint=await q('.mc-tip').boundingBox();await page.screenshot({path:path.join(img,'toolbar-hover.png'),clip:{x:top.x,y:top.y,width:top.width,height:hint.y+hint.height-top.y+4}});
  await page.mouse.move(1,1);await q('.mc-tip').waitFor({state:'detached'});
  for(const id of ['file','edit','search','view','project','run','tools','cvs','window','help']){await click(`[data-menu="${id}"]`);await noMissing();await page.keyboard.press('Escape');}
  await scene('debug');await q('.wb-side').waitFor();await noMissing();await assertFit();
  for(const [name,v] of Object.entries({n:3,i:1,x:8,sum:3}))await has(`[data-local="${name}"] td:last-child`,new RegExp(String(v)+'$'));
  await q('[data-code-line="10"] [data-var="sum"]').hover();await has('.wb-hover',/int sum = 3/);
  await q('#mini-cpp-review').screenshot({path:path.join(img,'debug.png'),animations:'disabled'});
  await act('step');await has('[data-local="sum"] td:last-child',/3 → 11/);await has('[data-position]',/行 8/);
  await q('[data-code-line="10"] [data-var="sum"]').focus();await has('.wb-hover',/int sum = 11/);
  await act('continue');await has('.wb-side header',/运行中/);await act('pause');await has('.wb-side header',/已暂停/);
  await scene('debug');await act('continue');await page.waitForFunction(()=>document.querySelector('.wb-framevalue code')?.textContent==='main.cpp:10' && document.querySelector('[data-local="x"] b')?.textContent==='-2' && document.querySelector('.wb-side header')?.textContent.includes('已暂停'));
  await act('continue');await q('.wb-side').waitFor({state:'detached'});await has('.wb-io>div:nth-child(2)',/9/);
  await scene('error');await has('.wb-diag-line',/main.cpp:11:5\s*error: expected ';' before '}' token/);await click('[data-jump="10"]');await has('[data-position]',/行 10，列 17/);assert.equal(await q('[data-code-line="10"].wb-picked').count(),1);await capture('diagnostic');
  await click('[data-jump="11"]');await has('[data-position]',/行 11，列 5/);await act('buildRun');await has('.wb-diag',/中文建议/);
  await q('[data-case]').selectOption('name');await has('.wb-diag-line',/10:16\s*error: 'xx' was not declared/);await has('.wb-unknown',/xx/);
  await scene('edit');await capture('editor');assert.equal(await q('.wb-side').count(),0);
  for(const std of ['98','11','14']){await q('[data-std]').selectOption(std);await q('[data-o2]').check();await act('compile');await has('.wb-log',new RegExp('g\\+\\+ -std=c\\+\\+'+std+' -O2'));}
  await q('[data-o2]').uncheck();await act('run');await has('.wb-io>div:nth-child(2)',/9/);await panel('log');await has('.wb-log',/-O2/);
  await act('rebuild');await has('.wb-log',/清理/);await has('.wb-log',/-O0/);await act('buildRun');await has('.wb-io>div:nth-child(2)',/9/);
  await click('[data-menu="search"]');await noMissing();await act('replace');await has('[data-match]',/2 处/);await click('[data-dlg="replaceAll"]');await has('[data-match]',/已替换 2 处/);await has('[data-code-preview]',/total/);await click('[data-dlg="undoReplace"]');await has('[data-code-preview]',/sum/);await click('.mc-dialog header [data-dlg="close"]');
  await click('[data-menu="edit"]');await act('format');await click('[data-dlg="applyDemo"]');await has('[data-mod]',/\n    sum/);await click('.mc-dialog header [data-dlg="close"]');
  await act('new');await q('[data-filename]').fill('round1.cpp');await click('[data-dlg="confirmFile"]');await has('.wb-filetab',/round1.cpp/);assert.equal(await q('.wb-row').count(),5);
  await act('run');await has('[data-status]',/先编译/);await act('buildRun');await has('.wb-io>div:nth-child(2)',/无输出/);
  await scene('icons');await noMissing();assert.equal(await q('[data-spec-actions] .mc-glyph').count(),4);await capture('icons');
  for(const width of [1024,736,590,420,360,320]){
    await page.setViewportSize({width,height:900});
    for(const id of ['debug','error','icons','edit']){await scene(id);await assertFit();await noMissing();}
  }
  await page.setViewportSize({width:1024,height:800});await page.emulateMedia({colorScheme:'dark'});await scene('debug');await q('#mini-cpp-review').screenshot({path:path.join(qa,'dark.png')});
  await page.setViewportSize({width:320,height:900});await scene('icons');await q('#mini-cpp-review').screenshot({path:path.join(qa,'narrow-icons.png')});
  // Export Windows icon artwork from the vector at its actual sizes, with alpha.
  const logo=await readFile(path.join(dir,'icons/mini-cpp.svg'),'utf8'), entries=[];
  await page.setViewportSize({width:320,height:320});
  for(const size of [16,24,32,48,64,128,256]) {
    await page.setContent(`<style>html,body{margin:0;background:transparent}svg{display:block;width:${size}px;height:${size}px}</style>${logo}`);
    const png=await page.locator('svg').screenshot({omitBackground:true});entries.push({size,png});
    if(size===128)await writeFile(path.join(dir,'icons/mini-cpp-128.png'),png);
  }
  const hdr=Buffer.alloc(6+16*entries.length);hdr.writeUInt16LE(1,2);hdr.writeUInt16LE(entries.length,4);
  let offset=hdr.length;for(const [{size,png},i] of entries.map((v,i)=>[v,i])){const p=6+i*16;hdr[p]=hdr[p+1]=size%256;hdr.writeUInt16LE(1,p+4);hdr.writeUInt16LE(32,p+6);hdr.writeUInt32LE(png.length,p+8);hdr.writeUInt32LE(offset,p+12);offset+=png.length;}
  await writeFile(path.join(dir,'icons/mini-cpp.ico'),Buffer.concat([hdr,...entries.map(e=>e.png)]));
  assert.deepEqual(errors,[]);console.log('PASS: debug, hover, pause, continue, four builds, standards/O2, CE locations, files, replace/undo, indent, icons, 6 viewport widths and dark rendering.');
}finally {await browser.close();}
