// Session-only editor windows and a virtual save destination. No filesystem or browser storage.
const reviewFolders=['D:\\竞赛练习','C:\\Users\\Student\\Documents','C:\\Users\\Student\\Desktop'];
const filePath=(folder,name)=>folder?folder.replace(/\\$/,'')+'\\'+name:name;
function validLeaf(s){return !!s && !/[\u0000-\u001f<>:"/\\|?*]/.test(s) && !/[. ]$/.test(s) && !/^(?:CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)/i.test(s);}
function folderChoice(raw){
  const p=raw.trim().replace(/\//g,'\\');
  const drive=/^[a-z]:\\/i.test(p),unc=p.startsWith('\\\\');
  if(!drive&&!unc)return null;
  const parts=p.slice(drive?3:2).split('\\').filter(Boolean);
  if(unc&&parts.length<2||parts.some(s=>!validLeaf(s)))return null;
  return (drive?p[0].toUpperCase()+':\\':'\\\\')+parts.join('\\');
}
function makeEditorDesk(initial){
  let serial=0,lastFolder=reviewFolders[0];const windows=[],files=new Map();
  const idFor=(folder,name)=>filePath(folder,name).toLowerCase();
  function add(doc){doc.windowId=++serial;windows.push(doc);return doc;}
  function front(doc){const pos=windows.indexOf(doc);if(pos!==-1){windows.splice(pos,1);windows.push(doc);}return doc;}
  function nextSlot(){const slots=new Set(windows.map(w=>w.slot).filter(Boolean));let n=1;while(slots.has(n))n++;return n;}
  function nameCheck(doc,folder,name){const k=idFor(folder,name);return windows.some(other=>other!==doc&&other.dir&&idFor(other.dir,other.file)===k)||files.has(k)&&(!doc.dir||idFor(doc.dir,doc.file)!==k);}
  function save(doc,folder,name){doc.dir=lastFolder=folder;doc.file=name;doc.slot=0;doc.dirty=false;doc.savedContent=doc.content;files.set(idFor(folder,name),{dir:folder,file:name,content:doc.content,blank:doc.blank,runCase:doc.runCase});}
  add(initial);
  return {windows,files,add,front,nextSlot,nameCheck,save,get lastFolder(){return lastFolder;},remove(doc){const i=windows.indexOf(doc);if(i!==-1)windows.splice(i,1);return windows.at(-1);}};
}
