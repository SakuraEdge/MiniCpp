// Optional: regenerate the native toolbar bitmaps from the same artwork used in the review.
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
const root=fileURLToPath(new URL('../',import.meta.url));
const require=createRequire(import.meta.url);let chromium;
try{({chromium}=require('playwright-core'));}catch{({chromium}=require(path.join(root,'.review-tools/node_modules/playwright-core')));}
const nodes=JSON.parse(await readFile(path.join(root,'design/vendor/lucide-subset.json'),'utf8'));
const folder=path.join(root,'src/Assets');await mkdir(folder,{recursive:true});
const basic=['file-plus-2','folder-open','save','undo-2','redo-2','search','replace','bug','square','step-forward','play','pause','plus','x'];
const builds=['compile','run','compile-and-run','rebuild'];
const browser=await chromium.launch({headless:true,executablePath:process.env.CHROME_PATH||'C:/Program Files/Google/Chrome/Application/chrome.exe'});
try{const page=await browser.newPage({viewport:{width:40,height:40},deviceScaleFactor:1,colorScheme:'light'});
  for(const name of [...basic,...builds]){
    let svg;if(builds.includes(name))svg=await readFile(path.join(root,'design/icons',name+'.svg'),'utf8');
    else svg=`<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="#424242" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">${nodes[name].map(([tag,attrs])=>`<${tag} ${Object.entries(attrs).map(([k,v])=>`${k}="${v}"`).join(' ')}/>`).join('')}</svg>`;
    await page.setContent(`<style>html,body{margin:0;background:transparent}svg{display:block;width:18px;height:18px}</style>${svg}`);
    await writeFile(path.join(folder,name+'.png'),await page.locator('svg').screenshot({omitBackground:true}));
  }
}finally{await browser.close();}
