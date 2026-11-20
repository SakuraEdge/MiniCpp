const root = document.getElementById('mini-cpp-review');
const $ = (sel) => root.querySelector(sel);
const $$ = (sel) => Array.from(root.querySelectorAll(sel));
const esc = (s) => String(s).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const cppIcon=(size=16)=>`<img data-cpp-icon alt="" draggable="false" width="${size}" height="${size}" src="${cppRasters[size]||cppRasters[128]}">`;
const icon = (id) => id==='cpp-file'?cppIcon():classicGlyphs[id] || `<i data-lucide="${id}" aria-hidden="true"></i>`;
const icons = () => { if (globalThis.lucide?.createIcons) globalThis.lucide.createIcons({attrs:{width:16,height:16}}); };
const fileName = () => state.file;
const defaultState = {scene:'edit',std:'14',o2:false,debugStd:'14',lastStd:'14',lastO2:false,step:0,running:false,breakpoint:true,targetLine:11,ce:'semicolon',panel:'console',result:'compiled',lastBuild:'compiled',file:'main.cpp',blank:false,runCase:'prefix',runPhase:'idle',record:null};
const standards=['98','11','14','17'];
const freshState=()=>({...defaultState,followOS:false,slot:0,dir:reviewFolders[0],dirty:false,content:null,savedContent:null,caret:0,runCommand:null});
let state=freshState(), demoDoc=state, showingIcons=false, pendingFile=null;
const desk=makeEditorDesk(state);
const reviewScene=()=>showingIcons?'icons':state.scene;
const changeSet = [ ['compile','classic-compile','编译','F9','生成当前源文件的程序'], ['run','classic-run','运行','F10','运行已有程序，不编译'], ['buildRun','classic-buildRun','编译并运行','F11','编译成功后再运行'], ['rebuild','classic-rebuild','全部重新编译','F12','清理旧产物并完整编译'] ];
const menus = {
  file:[['new','file-plus-2','新建源代码','Ctrl+N'],['open','folder-open','打开源代码…','Ctrl+O'],null,['save','save','保存','Ctrl+S'],['saveAs','file-pen-line','另存为…','Ctrl+Shift+S']],
  edit:[['undo','undo-2','撤销','Ctrl+Z'],['redo','redo-2','重做','Ctrl+Y'],['selectAll','text-select','全选','Ctrl+A'],null,['indent','indent-increase','增加缩进','Tab'],['outdent','indent-decrease','减少缩进','Shift+Tab'],['format','align-left','自动缩进选区','Ctrl+Tab'],['newline','corner-down-left','换行并继承缩进','Enter']],
  search:[['find','search','查找…','Ctrl+F'],['replace','replace','替换…','Ctrl+H']],
  view:[['console','square-terminal','运行输出',''],['log','list','编译日志',''],['issues','circle-alert','问题',''],null,['followOS','monitor','跟随系统外观','']],
  project:[['sourceInfo','cpp-file','当前源文件属性…','']],
  run:[...changeSet,null,['debug','bug','开始调试',''],['continue','play','继续',''],['pause','pause','暂停',''],['step','step-forward','单步',''],['stop','square','停止调试','']],
  tools:[['selectStd','settings-2','语言标准…',''],['toggleO2','gauge','O2 优化','']],
  cvs:[['noCvs','git-branch','当前文件未关联 CVS 仓库','']],
  help:[['shortcuts','keyboard','快捷键…',''],['about','info','关于 Mini C++…','']]
};
const names={edit:'编辑器',debug:'断点调试',error:'编译报错',icons:'图标稿'};
const work=makeWorkbench($('[data-workbench]'));
const dlg=$('[data-dialog]'), pop=$('[data-dropdown]');
let menu='', dialog='', returnTo=null, tip=null, tipTimer=0, runTimer=0, notice='', savedSignature='';
const say = (msg) => { $('[data-announce]').textContent=msg; };
const terminal=makeRunConsole($('[data-terminal]'),{icon,onFinish(record){
  state.runPhase=record.aborted?'aborted':'done';state.record=record.aborted?null:record;
  if(record.aborted){state.result='compiled';notice='运行已中止';}
  else{state.result=state.runCommand==='buildRun'?'builtRan':'ran';notice='运行结束 · 退出码 0';}
  paint();say(notice);persist();
},onDismiss:clearTip});
const caseFiles={prefix:'main.cpp',sum:'main.cpp',long:'numbers.cpp',file:'file_sum.cpp'};
function applyRunCase(id){Object.assign(state,{runCase:id,runPhase:'idle',record:null,blank:false,file:caseFiles[id],result:'compiled',lastBuild:'compiled',lastStd:state.std,lastO2:state.o2,panel:'console'});}
function persist() {
  const modelContent={scene:names[reviewScene()],standard:'C++'+state.std,optimizeO2:state.o2};
  // Only review choices are saved; text and file names entered in dialogs stay local.
  const privateContent={scene:reviewScene(),standard:state.std,o2:state.o2,step:state.step,runCase:state.runCase,followOS:state.followOS};
  savedSignature=JSON.stringify(privateContent);
  try { window.openai?.setWidgetState?.({modelContent,privateContent})?.catch?.(()=>{}); } catch { /* optional host */ }
}
function fromHost(saved) {
  const p=saved?.privateContent; if(!p || typeof p!=='object'||JSON.stringify(p)===savedSignature) return;
  clearTimeout(runTimer); closeMenu(); closeDialog(false); notice='';
  if (standards.includes(p.standard)) state.std=p.standard;
  if (typeof p.o2==='boolean') state.o2=p.o2;
  if (typeof p.followOS==='boolean') state.followOS=p.followOS;
  setScene(Object.hasOwn(names,p.scene)?p.scene:'edit',false);
  if(state.scene==='edit'&&!state.blank&&Object.hasOwn(caseFiles,p.runCase))applyRunCase(p.runCase);
  if (state.scene==='debug' && Number.isInteger(p.step)) state.step=Math.max(0,Math.min(stops.length-1,p.step));
  paint();
}
function placeFloat(el,anchor,above=false) {
  const a=anchor.getBoundingClientRect(), r=root.getBoundingClientRect();
  const x=Math.max(4,Math.min(r.width-el.offsetWidth-4,a.left-r.left));
  const up=a.top-r.top-el.offsetHeight-5, down=a.bottom-r.top+5;
  const y=above && up>=2 || down+el.offsetHeight>r.height && up>=2 ? up : down;
  el.style.left=x+'px'; el.style.top=y+'px';
}
function clearTip(){clearTimeout(tipTimer);tip?.remove();tip=null;}
function showTip(node) {
  clearTip(); if (!node || dialog && !dlg.contains(node) || menu) return;
  tipTimer=setTimeout(()=>{if(!node.isConnected)return;tip=document.createElement('span');tip.className='mc-tip';tip.setAttribute('role','tooltip');tip.textContent=node.dataset.tip;root.append(tip);placeFloat(tip,node);},280);
}
function closeMenu() { menu='';pop.hidden=true;pop.replaceChildren(); $$('[data-menu]').forEach(b=>b.setAttribute('aria-expanded','false')); }
function openMenu(node) {
  if(menu===node.dataset.menu) { closeMenu(); return; }
  closeMenu(); clearTip(); work.hoverClear(); menu=node.dataset.menu;
  const disabled=a=>a==='noCvs'||['continue','pause','step','stop'].includes(a) && (state.scene!=='debug' || (a==='pause'?!state.running:a==='stop'?false:state.running));
  const toggleValue=a=>a==='followOS'?state.followOS:a==='toggleO2'?state.o2:null;
  pop.innerHTML=menu==='window'?desk.windows.map(w=>`<button type="button" data-activate-window="${w.windowId}" aria-current="${w===state}">${cppIcon()}<span>${esc(w.file)}</span><kbd>${w===state?'当前':w.dirty||!w.dir?'未保存':''}</kbd></button>`).join('')+'<hr><button type="button" data-act="closeWindow" '+(desk.windows.length<2?'disabled':'')+'>'+icon('x')+'<span>关闭当前窗口</span><kbd>Ctrl+W</kbd></button>':menus[menu].map(item=>{if(!item)return '<hr>';const [id,glyph,label,key]=item,toggle=toggleValue(id);return `<button type="button" data-act="${id}" ${disabled(id)?'disabled':''} ${toggle===null?'':`aria-pressed="${toggle}"`}>${icon(glyph)}<span>${label}</span><kbd>${toggle===null?key:toggle?'开':'关'}</kbd></button>`;}).join('');
  pop.hidden=false; node.setAttribute('aria-expanded','true'); const a=node.getBoundingClientRect(), r=node.closest('.mc-menustrip').getBoundingClientRect();
  pop.style.left=Math.max(0,Math.min(a.left-r.left,r.width-pop.offsetWidth))+'px';pop.style.top=(a.bottom-r.top)+'px';icons();
}
function closeDialog(restore=true) {
  if(!dialog) return; dialog='';pendingFile=null;clearTip();dlg.hidden=true;dlg.replaceChildren();dimSheet(false);
  if(restore && returnTo?.isConnected) returnTo.focus({preventScroll:true}); returnTo=null;
}
function dimSheet(flag){$$('.mc-reviewbar,[data-desktop],[data-identity]').forEach(el=>{el.inert=flag;});}
function cancelSheet(){const request=pendingFile;closeDialog();if(!request)return;const action={compile:'编译',buildRun:'编译并运行',rebuild:'重新编译',debug:'调试'}[request.after];notice=action?'已取消，未开始'+action:'已取消保存';status();icons();say(notice);}
function sheet(id,title,contents,focusSel) {
  if(!dialog) returnTo=root.contains(document.activeElement)?document.activeElement:null;
  dialog=id; clearTip(); work.hoverClear(); closeMenu();
  dlg.innerHTML=`<section class="mc-dialog" role="dialog" aria-modal="true" aria-label="${esc(title)}"><header><h2>${title}</h2><button type="button" class="mc-quiet" data-dlg="close" aria-label="关闭对话框">${icon('x')}</button></header>${contents}</section>`;
  const actionGlyphs={confirmFile:'save',close:'x',discardWindow:'trash-2',saveClose:'save',undoReplace:'undo-2',replaceAll:'replace',demoReset:'rotate-ccw',sampleDebug:'bug',applyDemo:{undo:'undo-2',redo:'redo-2',selectAll:'text-select',format:'align-left',indent:'indent-increase',outdent:'indent-decrease',newline:'corner-down-left'}[id]};
  dlg.querySelector('header button').dataset.tip='关闭';
  for(const b of dlg.querySelectorAll('.mc-dialog-actions button')){const hint=b.textContent.trim(),ic=actionGlyphs[b.dataset.dlg];if(!ic)continue;b.setAttribute('aria-label',hint);b.dataset.tip=hint;b.className='mc-gbtn';b.innerHTML=icon(ic);}
  dlg.hidden=false;dimSheet(true);icons();dlg.querySelector(focusSel||'button').focus({preventScroll:true});
}
function drawIdentity(){
  const mark='<span class="mc-mark" aria-hidden="true"><b></b><i></i><i></i></span>';
  $$('[data-cpp-hero]').forEach(el=>{el.innerHTML=cppIcon(68);});
  $('[data-cpp-sizes]').innerHTML=[48,32,24,16].map(n=>`<span>${cppIcon(n)}${n} px</span>`).join('');
  $('[data-sizes]').innerHTML=[48,32,24,16].map(n=>`<span style="--sw:${n}px">${mark.replace('class="mc-mark"','class="mc-mark" style="--mark-size:var(--sw)"')}${n} px</span>`).join('');
  $('[data-spec-actions]').innerHTML=changeSet.map(c=>`<div class="mc-glyph"><div class="mc-glyph-view" aria-hidden="true"><span>${icon(c[1])}</span><span>${icon(c[1])}</span></div><strong>${c[2]}</strong><small>${c[4]}</small></div>`).join('');
}
function drawTools(){
  const groups=[['filetools',[menus.file[0],menus.file[1],menus.file[3]]],['edittools',[menus.edit[0],menus.edit[1],...menus.search]],['buildtools',changeSet],['debugtools',[menus.run.find(v=>v?.[0]==='debug')]]];
  groups.forEach(([key,items])=>{$(`[data-${key}]`).innerHTML=items.map(([a,ic,name,k])=>`<button class="mc-gbtn" type="button" data-act="${a}" aria-label="${name.replace('…','')}" data-tip="${name.replace('…','')}${k?' · '+k:''}">${icon(ic)}</button>`).join('');});
}
function status() {
  const s=state; let ico='circle-check', text='就绪', pos='行 1，列 1';
  if(s.scene==='debug'){ico=s.running?'play':'pause';text=s.running?'运行中 · 变量为最近采样值':'调试暂停 · -g -O0';const f=stops[s.step];pos=s.running?'采样：行 '+f.line:`行 ${f.line}，列 ${f.col}`;}
  if(s.scene==='error'){const d=ceSamples[s.ce];ico='circle-alert';text='编译失败 · 1 个错误';pos=`行 ${s.targetLine}，列 ${s.targetLine===d.hintLine?d.hintCol:d.col}`;}
  if(s.scene==='edit' && s.result!=='idle') text=({compiled:'编译成功',ran:'运行结束',builtRan:'编译并运行完成',rebuilt:'全部重新编译完成'})[s.result];
  if(s.runPhase==='active'){ico='square-terminal';text='命令行运行中';}
  if(s.runPhase==='aborted'){ico='square';text='已中止';}
  if(s.blank){const preceding=(s.content||'').slice(0,s.caret).split('\n');pos=`行 ${preceding.length}，列 ${preceding.at(-1).length+1}`;}
  if(s.blank&&s.result==='idle')text=s.dir?(s.dirty?'已修改，尚未保存':'就绪'):'未保存';
  if(s.blank&&s.result==='preview'){ico='list';text='编译参数预览';}
  $('[data-status]').innerHTML=icon(ico)+esc(notice||text); $('[data-position]').textContent=pos;
}
function paintStack(){
  const behind=desk.windows.slice(0,-1).slice(-2), shell=$('[data-desktop]');
  shell.style.setProperty('--layers',behind.length);shell.dataset.count=desk.windows.length;
  $('[data-backstack]').innerHTML=behind.map((w,i)=>`<div class="mc-backwin" style="--layer:${i}"><button type="button" class="mc-backtitle" data-activate-window="${w.windowId}" aria-label="切换到 ${esc(w.file)}">${cppIcon()}<span>${esc(w.file)}</span><span class="mc-backsuffix">- Mini C++</span></button></div>`).join('');
  $('[data-window]').dataset.windowId=state.windowId;$('[data-window]').setAttribute('aria-label',fileName()+' · Mini C++');
  $('.mc-caption [data-act="closeWindow"]').disabled=desk.windows.length<=1;
}
function activateDoc(doc,focus=true){
  if(!doc||!desk.windows.includes(doc))return;
  clearTimeout(runTimer);state.running=false;work.hoverClear();closeMenu();closeDialog(false);clearTip();
  notice='';showingIcons=false;state=desk.front(doc);paint();
  if(focus){const editor=$('[data-draft-code]');if(editor){editor.focus({preventScroll:true});editor.setSelectionRange(state.caret,state.caretEnd??state.caret);}else $('.wb-plus')?.focus({preventScroll:true});}
}
function newWindow(){
  const slot=desk.nextSlot();const doc=desk.add({...freshState(),std:state.std,o2:state.o2,followOS:state.followOS,blank:true,slot,dir:null,file:'未命名'+slot,content:emptySource.join('\n'),result:'idle',lastBuild:'idle'});
  activateDoc(doc);say(doc.file+'，未保存');
}
function removeWindow(){if(desk.windows.length<2)return;const old=state,next=desk.remove(old);if(old===demoDoc)demoDoc=null;activateDoc(next);say('当前窗口：'+fileName());}
function requestClose(){if(desk.windows.length<2)return;if(!state.dir||state.dirty){sheet('closeWindow','关闭窗口',`<p>要保存「${esc(fileName())}」吗？</p><div class="mc-dialog-actions"><button type="button" data-dlg="close">取消</button><button type="button" data-dlg="discardWindow">不保存并关闭</button><button type="button" data-dlg="saveClose">保存并关闭</button></div>`,'[data-dlg="close"]');}else removeWindow();}
function paint() {
  clearTip(); const id=reviewScene();
  root.dataset.followOs=String(state.followOS);
  $$('[data-scene]').forEach(b=>b.setAttribute('aria-pressed',String(b.dataset.scene===id)));
  $('[data-desktop]').hidden=id==='icons'; $('[data-identity]').hidden=id!=='icons';paintStack();
  $('[data-titlefile]').textContent=fileName();
  $('[data-case]').hidden=id!=='error';$('[data-case]').value=state.ce;
  $('[data-run-case]').hidden=id!=='edit'||state.blank;$('[data-run-case]').value=state.runCase;
  $('[data-std]').value=state.std; $('[data-o2]').checked=state.o2;
  if(id!=='icons') {work.render();status();} else work.hoverClear();
  icons();
}
function setScene(id,repaint=true) {
  clearTimeout(runTimer);terminal.reset();closeMenu();closeDialog(false);clearTip();notice='';
  state.running=false;showingIcons=id==='icons';
  if(!showingIcons&&!(id==='edit'&&state.blank)){
    const {std,o2,followOS}=state;if(!demoDoc)demoDoc=desk.add(freshState());state=desk.front(demoDoc);
    Object.assign(state,{std,o2,followOS,scene:id,running:false,step:0,breakpoint:true,blank:false,file:'main.cpp',dir:reviewFolders[0],dirty:false,slot:0,targetLine:11,ce:'semicolon',debugStd:std,lastStd:std,lastO2:o2,panel:id==='error'?'issues':'console',result:id==='error'?'idle':'compiled',lastBuild:'compiled',runCase:id==='edit'?'prefix':'sum',runPhase:'idle',record:null});
  }
  if(repaint){paint();say(names[id]);persist();}
}
function finishDebug(message) {
  clearTimeout(runTimer); state.scene='edit';state.running=false;state.result='ran';state.panel='console';notice=message||'调试结束 · 退出码 0';
  state.runPhase='done';state.record={mode:'sum',events:[{kind:'in',text:'3'},{kind:'in',text:'3 8 -2'},{kind:'out',text:'9'}]};
}
function nextSample() {
  if(state.scene!=='debug'||!state.running) return;
  if(state.step<stops.length-1){state.step++;if(state.breakpoint && stops[state.step].line===10){state.running=false;notice='';}else runTimer=setTimeout(nextSample,720);} else finishDebug();
  paint();persist();
}
function build(a) {
  if(a!=='run'&&(!state.dir||state.dirty))return fileSheet('beforeBuild',a);
  const hadBuild=state.result!=='idle'&&state.result!=='preview';
  if(a==='run' && (!hadBuild||state.scene==='error')) {notice=state.scene==='error'?'源代码未通过编译，未运行旧程序':'尚无可执行文件，请先编译';state.panel=state.scene==='error'?'issues':'console';paint();say(notice);return;}
  clearTimeout(runTimer);state.running=false;
  if(a!=='run'){state.lastStd=state.std;state.lastO2=state.o2;state.lastBuild=a==='rebuild'?'rebuilt':a==='buildRun'?'builtRan':'compiled';}
  const fixture=state.blank?(a==='run'?state.builtFixture:draftFixture(state.content)):state.runCase;
  if(a!=='run')state.builtFixture=fixture;
  if(state.blank&&!fixture){state.result='preview';state.runPhase='idle';state.record=null;state.panel='log';notice='仅预览命令，未执行自定义代码';}
  else if(state.scene==='error'){state.panel='issues';notice='编译失败，未启动程序';}
  else if(a==='run'||a==='buildRun'){
    Object.assign(state,{scene:'edit',runPhase:'active',runCommand:a,record:null,panel:'console',result:'compiled'});
    paint();terminal.open({mode:fixture,file:fileName()});say('命令行运行中');return;
  } else {state.scene='edit';state.result=a==='compile'?'compiled':'rebuilt';state.runPhase='idle';state.panel='log';}
  paint();say($('[data-status]').textContent);persist();
}
const editOps=['undo','redo','find','replace','selectAll','indent','outdent','format','newline'];
function act(a) {
  closeMenu();clearTip();notice='';
  if(['console','log','issues'].includes(a)){state.panel=a;paint();return;}
  if(a==='new')return newWindow();
  if(a==='closeWindow')return requestClose();
  if(a==='selectStd'){$('[data-std]').focus();try{$('[data-std]').showPicker?.();}catch{}return;}
  if(a==='toggleO2'){$('[data-o2]').click();return;}
  if(a==='followOS'){state.followOS=!state.followOS;paint();persist();return;}
  if(['sourceInfo','about','shortcuts'].includes(a)){
    const tit={sourceInfo:'源文件属性',about:'关于 Mini C++',shortcuts:'快捷键'};
    const body=a==='sourceInfo'?`<p class="mc-source-id">${cppIcon(24)}<code>${esc(fileName())}</code></p><p>C++${state.std} · UTF-8 · 单文件</p><p class="mc-pathline">${state.dir?esc(filePath(state.dir,state.file)):'未保存 · 当前内容仅在内存中'}</p>`:a==='about'?`<p>Mini C++ · 设计审查 ${$('.mc-rid').textContent}</p>`:`<div class="mc-keylist">${[...changeSet,...menus.search,menus.edit[6]].map(c=>`<div><span>${c[2]}</span><kbd>${c[3]}</kbd></div>`).join('')}</div>`;
    sheet(a,tit[a],body);return;
  }
  if(['compile','run','buildRun','rebuild'].includes(a)) return build(a);
  if(['open','saveAs'].includes(a)) return fileSheet(a);
  if(editOps.includes(a)){if(state.blank&&!['find','replace'].includes(a)&&work.editDraft(a))return;return editingSheet(a);}
  if(a==='save') {if(!state.dir)return fileSheet('save');desk.save(state,state.dir,state.file);notice='已保存（审查稿）';paint();say(notice);return;}
  if(a==='debug') {
    if(!state.dir||state.dirty)return fileSheet('beforeBuild','debug');
    if(state.scene==='error'){state.panel='issues'; notice='编译失败，无法启动调试';paint();return;}
    if(state.blank||state.runCase!=='sum'){sheet('debugSample','调试样例','<p>断点预览：求总和 · main.cpp:10</p><div class="mc-dialog-actions"><button type="button" class="mc-sm-btn" data-dlg="sampleDebug">打开求和调试样例</button></div>','[data-dlg="sampleDebug"]');return;}
    return setScene('debug');
  }
  if(state.scene!=='debug') return;
  if(a==='breakpoint') state.breakpoint=!state.breakpoint;
  if(a==='step'&&!state.running){if(state.step<stops.length-1) state.step++; else finishDebug();}
  if(a==='continue'&&!state.running){state.running=true;runTimer=setTimeout(nextSample,720);}
  if(a==='pause'&&state.running){clearTimeout(runTimer);state.running=false;}
  if(a==='stop'){clearTimeout(runTimer);state.scene='edit';state.running=false;state.runPhase='aborted';state.record=null;state.panel='log';state.result='compiled';notice='已停止调试';}
  paint();say($('[data-status]').textContent);persist();
}
function fileSheet(id,after=null) {
  if(id==='open'){
    const saved=Array.from(desk.files.values());
    const rows=saved.map((r,i)=>`<button type="button" class="mc-saved-file" data-open-entry="${i}">${cppIcon(24)}<span><strong>${esc(r.file)}</strong><small>${esc(r.dir)}</small></span></button>`).join('');
    sheet(id,'打开源代码',`<p>示例文件</p><button type="button" class="mc-sm-btn" data-dlg="openSample">${cppIcon(24)} main.cpp</button>${rows?'<p>本次审查中保存的文件</p>'+rows:''}`,'[data-dlg="openSample"]');return;
  }
  pendingFile={owner:state,after};
  const gated=id==='beforeBuild',folder=state.dir||desk.lastFolder,name=state.dir?state.file:state.file+'.cpp';
  notice='等待保存源文件';status();icons();
  const choices=[...new Set([folder,...reviewFolders])];
  const action={compile:'保存并编译',buildRun:'保存、编译并运行',rebuild:'保存并重新编译',debug:'保存并调试',closeWindow:'保存并关闭'}[after]||'保存';
  const message=gated?`<p class="mc-save-note">${icon('file-pen-line')}<span>「${esc(fileName())}」${state.dir?'有尚未保存的修改':'尚未保存'}。</span></p>`:'';
  sheet(id,gated?'编译前保存源文件':id==='saveAs'?'另存为':'保存源文件',`${message}<label>保存位置<select data-save-location>${choices.map(v=>`<option value="${esc(v)}">${esc(v)}</option>`).join('')}<option value="custom">其他文件夹…</option></select></label><label data-custom-location hidden>文件夹路径<input type="text" data-save-folder maxlength="250" autocomplete="off" placeholder="D:\\比赛"></label><label>文件名<input type="text" data-filename autocomplete="off" maxlength="110" value="${esc(name)}"></label><p class="mc-save-path">${cppIcon()}<code data-destination></code></p><p data-file-warning role="alert" hidden></p><div class="mc-dialog-actions"><span class="mc-virtual-label">模拟位置 · 不写入磁盘</span><button type="button" data-dlg="close">取消</button><button type="button" data-dlg="confirmFile">${action}</button></div>`,'[data-filename]');
  const inp=$('[data-filename]');inp.setSelectionRange(0,Math.max(0,inp.value.lastIndexOf('.')));previewLocation();
}
function selection(){let name=$('[data-filename]').value.trim();if(name&&!name.includes('.'))name+='.cpp';const opt=$('[data-save-location]').value;return {name,folder:folderChoice(opt==='custom'?$('[data-save-folder]').value:opt)};}
function previewLocation(){if(!pendingFile||!$('[data-filename]'))return;const {name,folder}=selection();$('[data-destination]').textContent=folder&&name?filePath(folder,name):'请选择位置和文件名';$('[data-file-warning]').hidden=true;}
function confirmFile(){
  const request=pendingFile;if(!request||request.owner!==state)return;
  const {name,folder}=selection();
  const invalid=(msg,sel)=>{const w=$('[data-file-warning]');w.hidden=false;w.textContent=msg;$(sel).focus();};
  if(!folder)return invalid('请输入完整的文件夹路径，例如 D:\\比赛。','[data-save-folder]');
  if(!validLeaf(name)||!/^.+\.cpp$/i.test(name))return invalid('请输入有效的 .cpp 文件名，不含目录。','[data-filename]');
  if(desk.nameCheck(state,folder,name))return invalid('该位置已使用这个文件名，请更换名称或位置。','[data-filename]');
  if(demoDoc===state&&(state.file!==name||state.dir!==folder))demoDoc=null;
  if(filePath(folder,name)!==filePath(state.dir,state.file))Object.assign(state,{result:'idle',lastBuild:'idle',builtFixture:null,record:null,runPhase:'idle'});
  desk.save(state,folder,name);closeDialog(false);notice='已保存（审查稿）';
  if(request.after==='closeWindow')return removeWindow();
  if(request.after)return act(request.after);
  paint();say(notice);
}
function openSaved(index){const r=Array.from(desk.files.values())[index];if(!r)return;const existing=desk.windows.find(w=>w.dir===r.dir&&w.file===r.file);closeDialog(false);activateDoc(existing||desk.add({...freshState(),std:state.std,o2:state.o2,followOS:state.followOS,...r,savedContent:r.content,result:'idle',lastBuild:'idle'}));}
const specimen=['if (x > 0) {','sum += x;','cout << sum;', '}'];
let draft=null;
const inv = {indent:'增加缩进',outdent:'减少缩进',format:'自动缩进选区',newline:'换行并继承缩进',selectAll:'全选',undo:'撤销',redo:'重做'};
function editingSheet(id){
  const search=id==='find'||id==='replace';
  if(search){draft={text:state.blank?state.content:specimen.join('\n'),prev:null};
    sheet(id,id==='find'?'查找':'查找和替换',`<label>查找<input type="text" data-find value="sum" maxlength="80"></label>${id==='replace'?'<label>替换为<input type="text" data-replace value="total" maxlength="80"></label>':''}<p data-match></p><pre class="mc-example" data-code-preview></pre><div class="mc-dialog-actions">${id==='replace'?'<button type="button" class="mc-quiet" data-dlg="undoReplace" disabled>撤销</button><button type="button" class="mc-tool mc-main" data-dlg="replaceAll">全部替换</button>':''}<button type="button" class="mc-sm-btn" data-dlg="close">关闭</button></div>`,'[data-find]');findPreview();return;
  }
  const indented=['if (x > 0) {','    sum += x;', '    cout << sum;', '}'];
  const cases={
    indent:[specimen.slice(1,3).join('\n'),indented.slice(1,3).join('\n'),'Tab'],
    outdent:[indented.slice(1,3).join('\n'),specimen.slice(1,3).join('\n'),'Shift+Tab'],
    format:[specimen.join('\n'),indented.join('\n'),'Ctrl+Tab'],
    newline:['    if (x > 0) {▏','    if (x > 0) {\n        ▏','Enter'],
    selectAll:[specimen.join('\n'),specimen.join('\n'),'Ctrl+A'],
    undo:['    sum += value;', '    sum += x;', 'Ctrl+Z'],redo:['    sum += x;', '    sum += value;', 'Ctrl+Y']
  };
  const [before,after,key]=cases[id];draft={before,after,applied:false};
  sheet(id,inv[id],`<p>编辑片段 · <kbd>${key}</kbd></p><pre class="mc-example" data-mod></pre><div class="mc-dialog-actions"><button type="button" class="mc-sm-btn" data-dlg="demoReset">还原片段</button><button type="button" class="mc-tool mc-main" data-dlg="applyDemo">${inv[id]}</button></div>`,'[data-dlg="applyDemo"]');
  dlg.querySelector('[data-mod]').textContent=before;
}
function applyDemo(apply){
  draft.applied=apply;const el=dlg.querySelector('[data-mod]');el.textContent=apply?draft.after:draft.before;
  el.dataset.selected=String(dialog==='selectAll'&&apply||['indent','outdent','format'].includes(dialog));
  dlg.querySelector('[data-dlg="applyDemo"]').disabled=apply;
}
function findPreview(message=''){
  if(!dlg.querySelector('[data-find]'))return;
  const f=dlg.querySelector('[data-find]').value, pieces=f?draft.text.split(f):[draft.text];
  dlg.querySelector('[data-match]').textContent=message||`编辑片段 · ${pieces.length-1} 处匹配`;
  dlg.querySelector('[data-code-preview]').innerHTML=pieces.map(esc).join('<mark>'+esc(f)+'</mark>');
  const r=dlg.querySelector('[data-dlg="replaceAll"]');if(r)r.disabled=pieces.length<2;
  const u=dlg.querySelector('[data-dlg="undoReplace"]');if(u)u.disabled=draft.prev===null;
}
function dlgAct(a){
  if(a==='close')return cancelSheet();
  if(a==='discardWindow'){closeDialog(false);return removeWindow();}
  if(a==='saveClose'){closeDialog(false);return fileSheet('save','closeWindow');}
  if(a==='sampleDebug')return setScene('debug');
  if(a==='openSample'){if(!demoDoc)demoDoc=desk.add(freshState());activateDoc(demoDoc,false);return setScene('edit');}
  if(a==='confirmFile')return confirmFile();
  if(a==='applyDemo'||a==='demoReset')return applyDemo(a==='applyDemo');
  if(a==='undoReplace'&&draft.prev!==null){draft.text=draft.prev;draft.prev=null;applyFindText();return findPreview();}
  if(a==='replaceAll'){const f=dlg.querySelector('[data-find]').value;if(!f)return;const bits=draft.text.split(f);draft.prev=draft.text;draft.text=bits.join(dlg.querySelector('[data-replace]').value);applyFindText();findPreview(`已替换 ${bits.length-1} 处`);}
}
function applyFindText(){const ta=$('[data-draft-code]');if(state.blank&&ta){ta.value=draft.text;work.updateDraft(ta);}}
root.addEventListener('click',e=>{
  const b=e.target.closest('button'); if(!b){if(e.target===dlg)cancelSheet();else if(!e.target.closest('[data-dropdown]'))closeMenu();return;}
  if(b.hasAttribute('data-activate-window'))return activateDoc(desk.windows.find(w=>w.windowId===Number(b.dataset.activateWindow)));
  if(b.hasAttribute('data-open-entry'))return openSaved(Number(b.dataset.openEntry));
  if(b.dataset.dlg)return dlgAct(b.dataset.dlg);
  if(b.dataset.scene)return setScene(b.dataset.scene);
  if(b.dataset.menu)return openMenu(b);
  if(b.dataset.act)return act(b.dataset.act);
  closeMenu();
  if(b.dataset.panel){state.panel=b.dataset.panel;notice='';paint();return;}
  if(b.dataset.jump){state.targetLine=Number(b.dataset.jump);paint();const row=$(`[data-code-line="${state.targetLine}"]`);row?.scrollIntoView({block:'nearest',behavior:'instant'});say($('[data-position]').textContent);}
});
root.addEventListener('input',e=>{if(e.target.matches('[data-find]'))findPreview();else if(e.target.matches('[data-filename],[data-save-folder]'))previewLocation();});
root.addEventListener('change',e=>{
  if(e.target.matches('[data-save-location]')){const custom=e.target.value==='custom';$('[data-custom-location]').hidden=!custom;if(custom)$('[data-save-folder]').focus();previewLocation();return;}
  if(e.target.matches('[data-run-case]')&&Object.hasOwn(caseFiles,e.target.value)){applyRunCase(e.target.value);notice='';paint();persist();return;}
  if(e.target.matches('[data-case]')&&Object.hasOwn(ceSamples,e.target.value)){state.ce=e.target.value;state.targetLine=ceSamples[state.ce].line;state.panel='issues';notice='';paint();return;}
  if(e.target.matches('[data-std]')) state.std=e.target.value;
  else if(e.target.matches('[data-o2]')) state.o2=e.target.checked; else return;
  notice=`下次构建：C++${state.std} · ${state.o2?'-O2':'-O0'}`;status();icons();say(notice);persist();
});
root.addEventListener('keydown',e=>{
  if(e.key==='Escape'){clearTip();work.hoverClear();if(dialog)cancelSheet();else if(menu){const old=menu;closeMenu();$(`[data-menu="${old}"]`).focus();}return;}
  if(dialog){
    if(e.key==='Enter'&&!e.isComposing&&e.target.matches('[data-filename],[data-save-folder]')){e.preventDefault();confirmFile();return;}
    if(e.key==='Tab'){const cs=Array.from(dlg.querySelectorAll('button:not(:disabled),input,select')).filter(el=>!el.closest('[hidden]'));if(e.shiftKey&&document.activeElement===cs[0]||!e.shiftKey&&document.activeElement===cs.at(-1)){e.preventDefault();(e.shiftKey?cs.at(-1):cs[0]).focus();}}
    if(inv[dialog] && (dialog==='format'&&e.ctrlKey&&e.key==='Tab'||dialog==='newline'&&e.key==='Enter'&&e.target.dataset.dlg==='applyDemo'||dialog==='selectAll'&&e.ctrlKey&&e.key.toLowerCase()==='a')) {e.preventDefault();applyDemo(true);}return;
  }
  if(showingIcons)return;
  if(e.target.matches('[data-draft-code]')){
    if(e.isComposing||e.keyCode===229)return;
    if(e.ctrlKey&&['a','z','y'].includes(e.key.toLowerCase()))return;
    if(e.key==='Enter'||e.key==='Tab'){e.preventDefault();work.editDraft(e.key==='Enter'?'newline':e.ctrlKey?'format':e.shiftKey?'outdent':'indent');return;}
  }
  if(e.altKey&&!e.ctrlKey){const target=$$('[data-menu]').find(b=>b.getAttribute('aria-keyshortcuts').slice(-1)===e.key.toUpperCase());if(target){e.preventDefault();target.focus();openMenu(target);return;}}
  if(menu && ['ArrowDown','ArrowUp'].includes(e.key)){const items=Array.from(pop.querySelectorAll('button:not(:disabled)'));if(items.length){e.preventDefault();let n=items.indexOf(document.activeElement)+(e.key==='ArrowDown'?1:-1);items[(n+items.length)%items.length].focus();}return;}
  const byKey={F9:'compile',F10:'run',F11:'buildRun',F12:'rebuild'};
  const byCtrl={n:'new',o:'open',s:e.shiftKey?'saveAs':'save',w:'closeWindow',f:'find',h:'replace',z:'undo',y:'redo',a:'selectAll',tab:'format'};
  const a=e.ctrlKey?byCtrl[e.key.toLowerCase()]:byKey[e.key];if(a){e.preventDefault();act(a);}
});
root.addEventListener('pointerover',e=>{const n=e.target.closest('[data-tip]');if(n&&!n.contains(e.relatedTarget))showTip(n);});
root.addEventListener('pointerout',e=>{const n=e.target.closest('[data-tip]');if(n&&!n.contains(e.relatedTarget))clearTip();});
root.addEventListener('focusin',e=>{const n=e.target.closest('[data-tip]');if(n&&e.target.matches(':focus-visible'))showTip(n);});
root.addEventListener('focusout',clearTip);
document.addEventListener('pointerdown',e=>{if(!root.contains(e.target)){closeMenu();work.hoverClear();clearTip();}});
drawTools();drawIdentity();paint();
if(window.openai?.widgetState)fromHost(window.openai.widgetState);
window.addEventListener('openai:set_globals',e=>{if(e.detail?.globals?.widgetState)fromHost(e.detail.globals.widgetState);});
if(globalThis.Tweak){
  const opts={spacing:'标准'};
  const update=()=>{root.dataset.density=opts.spacing==='紧凑'?'compact':'normal';};
  const tweak=new Tweak({container:$('[data-window]'),onChange:update});
  tweak.addSelect(opts,'spacing',{label:'代码行距',options:['标准','紧凑']});
}
