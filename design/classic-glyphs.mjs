// One pixel grid supplies the in-review artwork and the four standalone icon files.
const size=18;
const palette={edge:['#494b4d','#d0d1d2'],paper:['#fbfbfb','#292a2b'],tile:['#c4c6c8','#787a7c'],key:['#8295a4','#9cabb6'],strip:['#e1e1e1','#515253']};
const cells=[[0,0],[10,0],[0,10],[10,10]];
const rect=(x,y,w,h,fill,ring=false)=>({x,y,w,h,fill,ring});
const grid=filled=>cells.flatMap(([x,y],i)=>filled?[rect(x,y,8,8,'edge'),rect(x+1,y+1,6,6,'paper'),rect(x+2,y+2,4,4,i===0?'key':'tile')]:[rect(x,y,8,8,'edge',true)]);
const windowTop=[rect(1,1,16,16,'edge'),rect(2,2,14,14,'paper'),rect(2,2,14,2,'strip'),rect(2,4,14,1,'edge')];
const shapes={
  compile:{file:'compile',title:'编译',rects:grid(true)},
  run:{file:'run',title:'运行',rects:[...windowTop,rect(4,12,4,1,'key')]},
  buildRun:{file:'compile-and-run',title:'编译并运行',rects:[...windowTop,rect(4,7,4,3,'key'),rect(10,7,4,3,'tile'),rect(4,12,4,3,'tile'),rect(10,12,4,3,'tile')]},
  rebuild:{file:'rebuild',title:'全部重新编译',rects:grid(false)}
};
export function classicArtwork(){
  const html={}, files={}, css=[];
  const scope='#mini-cpp-review .mc-classic';
  const tokens=Object.entries(palette).map(([n,p])=>`--glyph-${n}:light-dark(${p.join(',')});`).join('');
  css.push(`${scope}{${tokens} position:relative;display:inline-block;width:18px;height:18px;flex:0 0 auto;vertical-align:middle;pointer-events:none}`,
    `${scope}>b{position:absolute;display:block;border-radius:0;margin:0;padding:0;box-sizing:border-box}`);
  for(const [kind,{rects,file,title}] of Object.entries(shapes)){
    const id=`classic-${kind}`;
    html[id]=`<span class="mc-classic" data-classic="${kind}" aria-hidden="true">${'<b></b>'.repeat(rects.length)}</span>`;
    const svg=[];
    rects.forEach(({x,y,w,h,fill,ring},i)=>{
      css.push(`${scope}[data-classic="${kind}"]>b:nth-child(${i+1}){left:${x/size*100}%;top:${y/size*100}%;width:${w/size*100}%;height:${h/size*100}%;${ring?'border:1px solid':'background:'} var(--glyph-${fill})}`);
      svg.push(ring?`  <rect x="${x+.5}" y="${y+.5}" width="${w-1}" height="${h-1}" fill="none" stroke="var(--${fill})"/>`:`  <rect x="${x}" y="${y}" width="${w}" height="${h}" fill="var(--${fill})"/>`);
    });
    const theme=i=>Object.entries(palette).map(([n,p])=>`--${n}:${p[i]}`).join(';');
    files[`${file}.svg`]=`<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 18 18" width="18" height="18" role="img" aria-labelledby="title" shape-rendering="crispEdges">\n  <title id="title">${title}</title>\n  <style>svg{${theme(0)}}@media(prefers-color-scheme:dark){svg{${theme(1)}}}</style>\n${svg.join('\n')}\n</svg>\n`;
  }
  return {html,files,css:css.join('\n')};
}
