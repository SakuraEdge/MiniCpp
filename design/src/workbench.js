const source = [
  '#include <iostream>', 'using namespace std;', '', '// 读入 n 个整数，输出它们的和。',
  'int main() {', '    int n, x, sum = 0;', '    cin >> n;', '    for (int i = 0; i < n; ++i) {',
  '        cin >> x;', '        sum += x;', '    }', "    cout << sum << '\\n';", '    return 0;', '}'
];
const emptySource = ['#include <iostream>', '', 'int main() {', '    return 0;', '}'];
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
    const text = state.blank ? emptySource.slice() : source.slice();
    if (state.scene === 'error') text[9] = ceSamples[state.ce].code;
    return text;
  }
  function nameValue(name) { return name === 'n' ? 3 : frame()[name]; }
  function codeTokens(text, num) {
    if (text.startsWith('//')) return `<span class="wb-comment">${esc(text)}</span>`;
    if (text.startsWith('#')) return '<span class="wb-directive">#include</span> <span class="wb-string">&lt;iostream&gt;</span>';
    const vars = { n:1, x:1, sum:1, i:1 };
    return text.split(/('(?:\\.|[^'])*'|\b(?:int|using|namespace|for|return|main|cin|cout|std|n|x|sum|i)\b|\b\d+\b)/g).map(t => {
      if (vars[t] && state.scene==='debug' && nameValue(t)!==undefined) {
        return `<button type="button" class="wb-token" data-var="${t}" data-line-ref="${num}" aria-label="查看 ${t} 的值">${t}</button>`;
      }
      const cls = /^(?:int|using|namespace|for|return)$/.test(t) ? 'wb-key' : /^(?:main|cin|cout|std)$/.test(t) ? 'wb-name' : /^'/.test(t) ? 'wb-string' : /^\d+$/.test(t) ? 'wb-number' : '';
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
      const cmd=`g++ -std=c++${cfg} "${fileName()}" -o "${base}${debug?'.debug':''}.exe"`;
      const d=ceSamples[state.ce];
      body=state.result==='idle' && !debug && !error?`<div class="wb-empty">${icon('hammer')} 尚无构建记录</div>`:`<div class="wb-log">${state.lastBuild==='rebuilt' && !debug?'<div class="wb-log-note">已清理当前源文件的旧产物</div>':''}<code>${esc(cmd)}</code><div class="wb-log-note">${error?`<span class="wb-fail">${esc(fileName())}:${d.line}:${d.col}: ${esc(d.msg)}</span>`:debug?'调试构建成功 · 独立的调试副本':'编译成功 · 0 错误，0 警告'}</div></div>`;
      right=error?'失败':debug?'调试构建':'普通构建';
    } else if (error) {
      body=`<div class="wb-empty">${icon('circle-alert')} 编译失败，本次未启动程序。</div>`; right='未运行';
    } else if (debug || ['ran','builtRan'].includes(state.result)) {
      const done=!debug || state.step===6;
      body=`<div class="wb-io"><div><h3>标准输入 <small>stdin</small></h3><pre>${state.blank?'无输入':'3\n3 8 -2'}</pre></div><div><h3>标准输出 <small>stdout</small></h3>${done && !state.blank?'<pre>9</pre>':`<span class="wb-console-muted">${done?'无输出':'尚无输出'}</span>`}</div></div>`;
      right=debug?(state.running?'运行中':'断点会话'):'退出码 0';
    } else {
      body=`<div class="wb-empty">${icon('terminal')} ${state.result==='idle'?'等待运行':'编译成功，尚未运行'}</div>`;right='未运行';
    }
    return `<section class="wb-output" aria-label="输出与诊断"><div class="wb-output-head"><nav aria-label="输出面板">${nav}</nav><span class="wb-output-status">${right}</span></div>${body}</section>`;
  }
  function render() {
    const hot=host.querySelector('.wb-hot');const anchor=hot && state.running?`[data-var="${hot.dataset.var}"][data-line-ref="${hot.dataset.lineRef}"]`:null;
    hoverClear(); const debug=state.scene==='debug';
    host.innerHTML=`<div class="wb-filebar"><div class="wb-filetab">${icon('file-code-2')}<strong>${esc(fileName())}</strong></div><button type="button" class="wb-plus" data-act="new" data-tip="新建源代码 · Ctrl+N" aria-label="新建源代码">${icon('plus')}</button><span class="wb-filekind">单文件</span></div>
      <div class="wb-well${debug?' wb-split':''}"><section class="wb-editor" aria-label="${esc(fileName())} 源码示例">${lines().map((t,i)=>drawLine(t,i+1)).join('')}</section>${debug?inspector():''}</div>${output()}`;
    if(anchor) inspect(host.querySelector(anchor));
  }
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
  return {render,hoverClear,lines};
}
