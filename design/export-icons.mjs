// Rasterize the approved vector artwork at native sizes for Windows and the offline review.
import {readFile,writeFile} from 'node:fs/promises';
import {createRequire} from 'node:module';
import {fileURLToPath} from 'node:url';
import path from 'node:path';
const dir=path.dirname(fileURLToPath(import.meta.url)), req=createRequire(import.meta.url);
let chromium;try{({chromium}=req('playwright-core'));}catch{({chromium}=req('../.review-tools/node_modules/playwright-core'));}
const base=path.join(dir,'icons'), sizes=[16,24,32,48,64,128,256];
const browser=await chromium.launch({executablePath:process.env.CHROME_PATH||'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
try{
  const page=await browser.newPage({viewport:{width:300,height:300},deviceScaleFactor:1,colorScheme:'light'});
  for(const name of ['mini-cpp','cpp-file']){
    const art=await readFile(path.join(base,name+'.svg'),'utf8');
    const small=name==='cpp-file'?await readFile(path.join(base,'cpp-file-small.svg'),'utf8'):art;
    const entries=[], header=Buffer.alloc(6+16*sizes.length);header.writeUInt16LE(1,2);header.writeUInt16LE(sizes.length,4);
    let offset=header.length;
    for(const [i,size] of sizes.entries()){
      await page.setContent(`<style>html,body{margin:0;background:transparent}svg{display:block;width:${size}px;height:${size}px}</style>${size===16?small:art}`);
      const png=await page.locator('svg').screenshot({omitBackground:true});entries.push(png);
      if(size===128 || name==='cpp-file' && [16,24,32,48].includes(size))await writeFile(path.join(base,`${name}-${size}.png`),png);
      const p=6+i*16;header[p]=header[p+1]=size%256;header.writeUInt16LE(1,p+4);header.writeUInt16LE(32,p+6);header.writeUInt32LE(png.length,p+8);header.writeUInt32LE(offset,p+12);offset+=png.length;
    }
    await writeFile(path.join(base,name+'.ico'),Buffer.concat([header,...entries]));
  }
}finally{await browser.close();}
