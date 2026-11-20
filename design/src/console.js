// Local review fixtures only. Nothing typed here is executed by a shell or sent to the host.
function makeRunConsole(host,{icon,onFinish,onDismiss}) {
  const q=s=>host.querySelector(s);
  let session=null,origin=null,timer=0;
  const dim=flag=>root.querySelectorAll('.mc-reviewbar,[data-window],[data-identity]').forEach(el=>{el.inert=flag;});
  function later(fn,ms){const active=session;clearTimeout(timer);timer=setTimeout(()=>{if(session===active&&!active.ended)fn();},ms);}
  function status(label,detail='',error=false){q('[data-term-state]').textContent=label;const h=q('[data-term-hint]');h.textContent=detail;h.classList.toggle('mc-term-error',error);}
  function entry(kind,text){
    const event={kind,text:String(text)};session.events.push(event);
    const line=document.createElement('div');line.dataset.termRow=kind;line.textContent=event.text;line.setAttribute('aria-label',`${kind==='in'?'输入':'输出'}：${event.text}`);
    const log=q('[data-term-log]');log.append(line);log.scrollTop=log.scrollHeight;
  }
  function end(aborted=false){
    if(!session||session.ended)return;
    clearTimeout(timer);session.ended=true;
    q('[data-term-form]').hidden=true;
    status(aborted?'已中止':'已退出',aborted?'':'退出码 0');
    if(!aborted)q('[data-term-close]').focus({preventScroll:true});
    onFinish({mode:session.mode,aborted,events:aborted?[]:session.events.slice()});
  }
  function drain(){q('[data-term-form]').hidden=true;status('正在结束');later(()=>end(),140);}
  function reset(){clearTimeout(timer);session=null;origin=null;host.hidden=true;host.replaceChildren();dim(false);}
  function dismiss(){
    if(!session)return;const focus=origin;if(!session.ended)end(true);reset();onDismiss();
    if(focus?.isConnected)focus.focus({preventScroll:true});
  }
  function hint(){const s=session;return s.n===null?'等待输入 n（0–64）':`等待第 ${s.seen+1} / ${s.n} 个整数`;}
  function submit(){
    const s=session;if(!s||s.ended||q('[data-term-form]').hidden)return;
    const input=q('[data-term-input]'),raw=input.value;if(!raw.trim())return;
    // Validate before echoing so a bad pasted line cannot partially advance the sample.
    let n=s.n,seen=s.seen,sum=s.sum;const pending=[];
    for(const line of raw.replace(/\r\n?/g,'\n').split('\n')){
      if(n!==null&&seen===n)break;
      pending.push(['in',line]);
      for(const token of line.trim().split(/\s+/).filter(Boolean)){
        if(n!==null&&seen===n)break;
        if(!/^[+-]?\d+$/.test(token)){status('等待输入','请输入十进制整数。',true);input.focus();return;}
        const val=Number(token);
        if(n===null){if(!Number.isInteger(val)||val<0||val>64){status('等待输入','样例的 n 范围是 0–64。',true);input.focus();return;}n=val;}
        else {if(!Number.isInteger(val)||val< -2147483648||val>2147483647||sum+val< -2147483648||sum+val>2147483647){status('等待输入','数值或累计和超出了 int 范围。',true);input.focus();return;}sum+=val;seen++;if(s.mode==='prefix')pending.push(['out',String(sum)]);}
      }
    }
    Object.assign(s,{n,seen,sum});input.value='';pending.forEach(([kind,text])=>entry(kind,text));
    if(n!==null&&seen===n){if(s.mode==='sum')entry('out',sum);drain();}else{status('运行中',hint());input.focus({preventScroll:true});}
  }
  function open({mode,file}){
    reset();origin=document.activeElement;session={mode,n:null,seen:0,sum:0,events:[],ended:false};
    host.innerHTML=`<section class="mc-term" role="dialog" aria-modal="true" aria-labelledby="mc-terminal-name">
      <header class="mc-term-head"><span class="mc-term-emblem">${icon('square-terminal')}</span><strong id="mc-terminal-name">命令行</strong><span class="mc-term-filename" data-term-name></span><button type="button" class="mc-gbtn" data-term-close aria-label="关闭命令行" data-tip="关闭命令行">${icon('x')}</button></header>
      <div class="mc-term-body"><div class="mc-term-log" role="log" aria-label="命令行输入输出" aria-live="polite" data-term-log></div>
      <form class="mc-term-form" data-term-form><span aria-hidden="true">›</span><textarea rows="1" maxlength="4096" spellcheck="false" autocomplete="off" autocapitalize="off" inputmode="text" aria-label="标准输入" aria-describedby="mc-term-hint" data-term-input></textarea><button class="mc-gbtn" type="submit" data-term-send aria-label="提交输入" data-tip="提交输入 · Enter">${icon('corner-down-left')}</button></form></div>
      <footer class="mc-term-foot"><span class="mc-term-state" data-term-state></span><span id="mc-term-hint" data-term-hint aria-live="polite"></span></footer></section>`;
    const interactive=mode==='sum'||mode==='prefix';q('[data-term-form]').hidden=!interactive;
    q('[data-term-name]').textContent=file.replace(/\.cpp$/i,'')+'.exe';
    host.hidden=false;dim(true);icons();
    status('运行中',interactive?hint():mode==='file'?'文件输入输出':mode==='long'?'标准输出':'');
    (interactive?q('[data-term-input]'):q('[data-term-close]')).focus({preventScroll:true});
    if(mode==='long'){let i=0;const tick=()=>{entry('out',++i);if(i===16)drain();else later(tick,95);};later(tick,180);}
    else if(!interactive)later(()=>end(),mode==='file'?600:180);
  }
  host.addEventListener('submit',e=>{e.preventDefault();submit();});
  host.addEventListener('click',e=>{if(e.target.closest('[data-term-close]'))dismiss();});
  host.addEventListener('keydown',e=>{
    if(!session)return;e.stopPropagation();
    if(e.isComposing||e.keyCode===229)return;
    if(e.key==='Escape'){e.preventDefault();dismiss();return;}
    if(e.key==='Enter'&&!e.shiftKey&&e.target.matches('[data-term-input]')){e.preventDefault();submit();return;}
    if(e.key==='Tab'){const items=Array.from(host.querySelectorAll('button,textarea')).filter(n=>!n.closest('[hidden]'));if(e.shiftKey&&document.activeElement===items[0]||!e.shiftKey&&document.activeElement===items.at(-1)){e.preventDefault();(e.shiftKey?items.at(-1):items[0]).focus();}}
  });
  return {open,reset,isOpen:()=>session!==null};
}
