const source = [
  '#include <iostream>', 'using namespace std;', '', '// 读入 n 个整数，输出它们的和。',
  'int main() {', '    int n, x, sum = 0;', '    cin >> n;', '    for (int i = 0; i < n; ++i) {',
  '        cin >> x;', '        sum += x;', '    }', "    cout << sum << '\\n';", '    return 0;', '}'
];
const emptySource = ['#include <iostream>', '', 'int main() {', '    return 0;', '}'];
const runExamples={
  sum:source,
  prefix:[...source.slice(0,3),'// 每读入一个整数，就输出当前总和。',...source.slice(4,10),'        cout << sum << endl;',source[10],...source.slice(12)],
  long:['#include <iostream>','using namespace std;','','// 逐行输出 1 到 16。','int main() {','    for (int i = 1; i <= 16; ++i) {','        cout << i << endl;','    }','    return 0;','}'],
  file:['#include <fstream>','using namespace std;','','int main() {','    ifstream fin("sum.in");','    ofstream fout("sum.out");','    if (!fin || !fout) return 1;','    int n, x, sum = 0;','    fin >> n;','    for (int i = 0; i < n; ++i) {','        fin >> x;','        sum += x;','    }',"    fout << sum << '\\n';",'    return 0;','}']
};
function draftFixture(text){const value=(text||'').replace(/\r\n?/g,'\n').trim();if(value===emptySource.join('\n'))return 'empty';return Object.keys(runExamples).find(k=>runExamples[k].join('\n')===value)||null;}
const stops = [
  { line: 10, col: 9, i: 1, x: 8, sum: 3, changed: '', before: '' },
  { line: 8, col: 5, i: 1, x: 8, sum: 11, changed: 'sum', before: '3' },
  { line: 9, col: 9, i: 2, x: 8, sum: 11, changed: 'i', before: '1' },
  { line: 10, col: 9, i: 2, x: -2, sum: 11, changed: 'x', before: '8' },
  { line: 8, col: 5, i: 2, x: -2, sum: 9, changed: 'sum', before: '11' },
  { line: 12, col: 5, x: -2, sum: 9, changed: '', before: '' },
  { line: 13, col: 5, x: -2, sum: 9, changed: '', before: '' }
];
const ceSamples = {
  semicolon:{line:11,col:5,hintLine:10,hintCol:17,code:'        sum += x',msg:"error: expected ';' before '}' token",advice:'第 11 行的 <code>}</code> 前缺少分号。可能是上一行 <code>sum += x</code> 的末尾漏写了 <code>;</code>。'},
  name:{line:10,col:16,hintLine:10,hintCol:16,code:'        sum += xx;',msg:"error: 'xx' was not declared in this scope",advice:'第 10 行的 <code>xx</code> 未声明。上一行读入的是 <code>x</code>，请检查是否多写了一个 <code>x</code>。'}
};
function makeWorkbench(host) {
  const frame = () => stops[state.step];
  function lines() {
    const text = state.blank ? state.content.split('\n') : state.scene==='edit'?runExamples[state.runCase].slice():source.slice();
    if (state.scene === 'error') text[9] = ceSamples[state.ce].code;
    return text;
  }
  function nameValue(name) { return name === 'n' ? 3 : frame()[name]; }
  function codeTokens(text, num) {
    if (text.startsWith('//')) return `<span class="wb-comment">${esc(text)}</span>`;
    if (text.startsWith('#')) return `<span class="wb-directive">#include</span> <span class="wb-string">${esc(text.slice(9))}</span>`;
    const vars = { n:1, x:1, sum:1, i:1 };
    return text.split(/('(?:\\.|[^'])*'|"(?:\\.|[^"])*"|\b(?:int|using|namespace|for|if|return|main|cin|cout|endl|ifstream|ofstream|std|n|x|sum|i)\b|\b\d+\b)/g).map(t => {
      if (vars[t] && state.scene==='debug' && nameValue(t)!==undefined) {
        return `<button type="button" class="wb-token" data-var="${t}" data-line-ref="${num}" aria-label="查看 ${t} 的值">${t}</button>`;
      }
      const cls = /^(?:int|using|namespace|for|if|return)$/.test(t) ? 'wb-key' : /^(?:main|cin|cout|endl|ifstream|ofstream|std)$/.test(t) ? 'wb-name' : /^['"]/.test(t) ? 'wb-string' : /^\d+$/.test(t) ? 'wb-number' : '';
      return cls ? `<span class="${cls}">${esc(t)}</span>` : esc(t);
    }).join('');
  }
  function drawLine(text, n) {
    const debug = state.scene==='debug', error = state.scene==='error';
    const current = debug && !state.running && n===frame().line;
    const bp = !state.blank && n===10 && state.breakpoint && debug;
    const picked = error && n===state.targetLine;
    const invalid = error && n===ceSamples[state.ce].line;
    const cls = `${current?' wb-at':''}${picked?' wb-picked':''}${invalid?' wb-invalid':''}`;
    const gutter = !state.blank && n===10 && debug ? `<button class="wb-bp-btn" type="button" data-act="breakpoint" aria-pressed="${bp}" aria-label="${bp?'移除':'添加'}第 10 行断点" data-tip="${bp?'移除':'添加'}断点"><span class="wb-break ${bp?'wb-solid':''}"></span></button>` : '';
    const marker = current ? '<span class="wb-arrow" aria-label="下一条将执行的语句">➜</span>' : invalid ? '<span class="wb-errmark" aria-label="编译器报错位置">!</span>' : '';
    const code=error && state.ce==='name' && n===10 ? codeTokens(text,n).replace('xx','<span class="wb-unknown">xx</span>') : codeTokens(text,n);
    return `<div class="wb-row${cls}" data-code-line="${n}"><span class="wb-gutter">${gutter}${marker}</span><span class="wb-lno" aria-hidden="true">${n}</span><code class="wb-line">${code}${error && state.ce==='semicolon' && n===10 && state.targetLine===10 ? '<span class="wb-insert" aria-label="建议补分号的位置"></span>':''}</code></div>`;
  }
  function inspector() {
    const f=frame(), running=state.running;
    const vars=['n','i','x','sum'].filter(v=>nameValue(v)!==undefined);
    const rows=vars.map(v => `<tr data-local="${v}"${v===f.changed?' class="wb-changed"':''}><td><code>${v}</code></td><td>int</td><td>${v===f.changed?`<small>${f.before} → </small>`:''}<b>${nameValue(v)}</b></td></tr>`).join('');
    return `<aside class="wb-side" aria-label="调试变量侧边栏">
      <header><span class="wb-side-title">${icon('bug')} 调试器</span><span class="wb-tag ${running?'':'wb-warn'}">${running?'运行中':'已暂停'}</span></header>
      <div class="wb-frame"><div class="wb-framelabel">${running?'最近采样':'当前栈帧'}<span>01</span></div><div class="wb-framevalue"><b>main()</b><code>${esc(fileName())}:${f.line}</code></div></div>
      <div class="wb-locals"><h3>局部变量 <span>${vars.length}</span></h3><table aria-label="${running?'最近采样的变量快照':'当前变量值'}"><thead><tr><th>名称</th><th>类型</th><th>值</th></tr></thead><tbody>${rows}</tbody></table></div>
      <div class="wb-debugbar">${running?`<button type="button" data-act="pause" class="mc-gbtn" aria-label="暂停" data-tip="暂停">${icon('pause')}</button>`:`<button type="button" data-act="step" class="mc-gbtn" aria-label="单步" data-tip="单步执行">${icon('step-forward')}</button><button type="button" data-act="continue" class="mc-gbtn" aria-label="继续" data-tip="继续执行">${icon('play')}</button>`}<button type="button" data-act="stop" class="mc-gbtn wb-stop" aria-label="停止调试" data-tip="停止调试">${icon('square')}</button></div>
    </aside>`;
  }
  function diagnostics() {
    const d=ceSamples[state.ce];
    return `<div class="wb-diag" aria-label="编译诊断"><div class="wb-diag-line"><span class="wb-tag wb-red">错误</span><button class="wb-link" type="button" data-jump="${d.line}" data-tip="定位编译器报告的位置">${esc(fileName())}:${d.line}:${d.col}</button><code>${esc(d.msg)}</code></div>
      <div class="wb-advice">${icon('lightbulb')}<div><strong>中文建议</strong><span> ${d.advice}</span><button class="wb-link" type="button" data-jump="${d.hintLine}">检查第 ${d.hintLine} 行 · 第 ${d.hintCol} 列 ${icon('arrow-up-right')}</button></div></div></div>`;
  }
  function finishedOutput(record){
    if(record.mode==='file')return `<div class="wb-empty wb-file-done" data-file-done>${icon('circle-check')} 已完成文件输入输出</div>`;
    const all=record.events;
    if(!all.length)return `<div class="wb-empty">${icon('terminal')} 无输入输出</div>`;
    const row=({kind,text})=>`<li data-record-row="${kind}"><span>${kind==='in'?'输入':'输出'}</span><code>${esc(text.length>120?text.slice(0,119)+'…':text)}</code></li>`;
    const ellipsis=`<li class="wb-ellipsis" data-record-ellipsis><span>…</span><small>省略 ${all.length-4} 行</small></li>`;
    return `<ol class="wb-record" data-run-record aria-label="本次运行的输入输出记录">${all.length>10?all.slice(0,3).map(row).join('')+ellipsis+row(all.at(-1)):all.map(row).join('')}</ol>`;
  }
  function output() {
    const debug=state.scene==='debug', error=state.scene==='error', pan=state.panel;
    const tabs=[['console','square-terminal','运行输出'],['log','list','编译日志'],['issues','circle-alert','问题']];
    const nav=tabs.map(([id,ic,label])=>`<button type="button" class="wb-ptab" data-panel="${id}" aria-pressed="${pan===id}">${icon(ic)}${label}${id==='issues'?`<span class="wb-count ${error?'wb-has':''}">${error?1:0}</span>`:''}</button>`).join('');
    let body, right;
    if (pan==='issues') {
      body=error?diagnostics():`<div class="wb-empty">${icon('circle-check')} 暂无诊断</div>`; right=error?'1 个错误':'0 个错误';
    } else if (pan==='log') {
      const cfg = debug ? `${state.debugStd} -g -O0` : `${state.lastStd} ${state.lastO2?'-O2':'-O0'}`;
      const base=fileName().replace(/\.cpp$/i,'');
      const cwd=state.blank?state.dir:null;
      const cmd=`g++ -std=c++${cfg} "${filePath(cwd,fileName())}" -o "${filePath(cwd,base+(debug?'.debug':'')+'.exe')}"`;
      const d=ceSamples[state.ce];
      body=state.result==='idle' && !debug && !error?`<div class="wb-empty">${icon('classic-compile')} 尚无构建记录</div>`:`<div class="wb-log">${state.lastBuild==='rebuilt' && !debug?'<div class="wb-log-note">已清理当前源文件的旧产物</div>':''}<code>${esc(cmd)}</code><div class="wb-log-note">${error?`<span class="wb-fail">${esc(fileName())}:${d.line}:${d.col}: ${esc(d.msg)}</span>`:debug?'调试构建成功 · 独立的调试副本':'编译成功 · 0 错误，0 警告'}</div></div>`;
      if(state.result==='preview')body=`<div class="wb-log"><code>${esc(cmd)}</code><div class="wb-log-note">命令预览 · 未调用编译器</div></div>`;
      right=error?'失败':state.result==='preview'?'未执行':debug?'调试构建':'普通构建';
    } else if (error) {
      body=`<div class="wb-empty">${icon('circle-alert')} 编译失败，本次未启动程序。</div>`; right='未运行';
    } else if (debug || state.runPhase==='active') {
      body=`<div class="wb-empty" data-run-pending>${icon('square-terminal')} ${debug?'调试未结束，暂无本次记录':'程序运行中，结束后显示本次记录'}</div>`;right=debug?'调试会话':'命令行中';
    } else if (state.runPhase==='aborted') {
      body=`<div class="wb-empty">${icon('square')} 运行已中止，未生成完成记录</div>`;right='已中止';
    } else if (state.record) {
      body=finishedOutput(state.record);right='退出码 0';
    } else {
      body=`<div class="wb-empty">${icon('terminal')} ${state.result==='idle'||state.result==='preview'?'等待运行':'编译成功，尚未运行'}</div>`;right='未运行';
    }
    return `<section class="wb-output" aria-label="输出与诊断"><div class="wb-output-head"><nav aria-label="输出面板">${nav}</nav><span class="wb-output-status">${right}</span></div>${body}</section>`;
  }
  function render() {
    const hot=host.querySelector('.wb-hot');const anchor=hot && state.running?`[data-var="${hot.dataset.var}"][data-line-ref="${hot.dataset.lineRef}"]`:null;
    hoverClear(); const debug=state.scene==='debug';
    const editor=state.blank?`<section class="wb-editor wb-draft-editor"><div class="wb-draft-gutter" aria-hidden="true"><pre data-draft-nums></pre></div><textarea wrap="off" spellcheck="false" autocapitalize="off" autocomplete="off" maxlength="131072" aria-label="${esc(fileName())} 源码" data-draft-code></textarea></section>`:`<section class="wb-editor" aria-label="${esc(fileName())} 源码示例">${lines().map((t,i)=>drawLine(t,i+1)).join('')}</section>`;
    host.innerHTML=`<div class="wb-filebar"><div class="wb-filetab">${icon('cpp-file')}<strong>${esc(fileName())}</strong><span class="wb-dirty" aria-label="未保存" data-dirty-dot ${state.dirty||!state.dir?'':'hidden'}></span></div><button type="button" class="wb-plus" data-act="new" data-tip="新建源代码窗口 · Ctrl+N" aria-label="新建源代码窗口">${icon('plus')}</button><span class="wb-filekind" data-file-kind>${state.blank?state.dir?(state.dirty?'已修改':'已保存'):'未保存':'单文件'}</span></div>
      <div class="wb-well${debug?' wb-split':''}">${editor}${debug?inspector():''}</div>${output()}`;
    if(state.blank){const field=host.querySelector('[data-draft-code]');field.value=state.content;field.setSelectionRange(state.caret,state.caretEnd??state.caret);lineNums(field);field.scrollTop=state.scrollTop||0;syncScroll(field);}
    if(anchor) inspect(host.querySelector(anchor));
  }
  function lineNums(el){const n=el.value.split('\n').length,pre=host.querySelector('[data-draft-nums]');if(pre.dataset.lines!==String(n)){pre.textContent=Array.from({length:n},(_,i)=>i+1).join('\n');pre.dataset.lines=n;}}
  function syncScroll(el){state.scrollTop=el.scrollTop;host.querySelector('[data-draft-nums]').style.transform=`translateY(-${el.scrollTop}px)`;}
  function caret(el){state.caret=el.selectionStart;state.caretEnd=el.selectionEnd;}
  function updateDraft(el){state.content=el.value;state.dirty=!state.dir||state.content!==state.savedContent;caret(el);lineNums(el);syncScroll(el);host.querySelector('[data-dirty-dot]').hidden=!state.dirty;host.querySelector('[data-file-kind]').textContent=state.dir?(state.dirty?'已修改':'已保存'):'未保存';notice='';status();icons();}
  function editDraft(op){
    const el=host.querySelector('[data-draft-code]');if(!el)return false;el.focus({preventScroll:true});
    if(op==='selectAll'){el.select();return true;}
    if(op==='undo'||op==='redo'){document.execCommand(op);return true;}
    const val=el.value,start=el.selectionStart,end=el.selectionEnd;let a=start,b=end,insert='',sel=false;
    if(op==='newline'){const prev=val.slice(0,a).split('\n').at(-1);insert='\n'+(prev.match(/^\s*/)?.[0]||'')+(/\{\s*$/.test(prev)?'    ':'');}
    else if(op==='indent'&&a===b){insert='    ';}
    else if(['indent','outdent','format'].includes(op)){
      a=val.lastIndexOf('\n',start-1)+1;const next=val.indexOf('\n',end);b=end>start&&val[end-1]==='\n'?end-1:next===-1?val.length:next;
      let depth=0;const inc=t=>(t.match(/\{/g)||[]).length-(t.match(/\}/g)||[]).length;
      if(op==='format')depth=Math.max(0,inc(val.slice(0,a)));
      insert=val.slice(a,b).split('\n').map(line=>{if(op==='indent')return '    '+line;if(op==='outdent')return line.replace(/^(?: {1,4}|\t)/,'');const s=line.trimStart();const pad=Math.max(0,depth-(s.startsWith('}')?1:0));depth=Math.max(0,depth+inc(s));return s&&!s.startsWith('#')?'    '.repeat(Math.min(128,pad))+s:s;}).join('\n');sel=end>start;
    }else return false;
    el.setSelectionRange(a,b);if(!document.execCommand('insertText',false,insert)){el.setRangeText(insert,a,b,'end');updateDraft(el);}if(sel)el.setSelectionRange(a,a+insert.length);caret(el);return true;
  }
  host.addEventListener('input',e=>{if(e.target.matches('[data-draft-code]'))updateDraft(e.target);});
  host.addEventListener('select',e=>{if(e.target.matches('[data-draft-code]'))caret(e.target);},true);
  host.addEventListener('scroll',e=>{if(e.target.matches?.('[data-draft-code]'))syncScroll(e.target);},true);
  let hover=null;
  function hoverClear() { if(hover) hover.remove(); hover=null; host.querySelector('.wb-hot')?.classList.remove('wb-hot'); host.querySelector('.wb-inspected')?.classList.remove('wb-inspected'); }
  function inspect(node) {
    if (state.scene!=='debug'||!node) return;
    hoverClear(); const name=node.dataset.var; node.classList.add('wb-hot'); host.querySelector(`[data-local="${name}"]`)?.classList.add('wb-inspected');
    hover=document.createElement('div'); hover.className='wb-hover'; hover.setAttribute('role','tooltip');
    hover.innerHTML=`<span>${state.running?'运行中 · 最近采样':'局部变量'}</span><code>int <b>${name}</b> = <b>${nameValue(name)}</b></code>`;
    root.append(hover); placeFloat(hover,node,true);
  }
  host.addEventListener('pointerover', e=>{const v=e.target.closest('[data-var]'); if(v && !v.contains(e.relatedTarget)) inspect(v);});
  host.addEventListener('pointerout', e=>{const v=e.target.closest('[data-var]'); if(v && !v.contains(e.relatedTarget) && document.activeElement!==v) hoverClear();});
  host.addEventListener('focusin',e=>{ if(e.target.matches('[data-var]')) inspect(e.target); });
  host.addEventListener('focusout',e=>{ if(e.target.matches('[data-var]')) hoverClear(); });
  host.addEventListener('click',e=>{ const v=e.target.closest('[data-var]'); if(v) inspect(v); });
  return {render,hoverClear,lines,editDraft,updateDraft};
}
