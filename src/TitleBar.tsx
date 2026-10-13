import {useEffect,useState} from 'react';
import {getCurrentWindow} from '@tauri-apps/api/window';
import type {UnlistenFn} from '@tauri-apps/api/event';
import {Minus,Square,Copy,X} from 'lucide-react';
import {isTauri} from './api';
import brandImage from '../app-icon.svg';

export function TitleBar({onError}:{onError:(error:unknown)=>void}) {
  const [maximized,setMaximized]=useState(false);
  const [focused,setFocused]=useState(true);
  const [fullscreen,setFullscreen]=useState(false);
  const macOS=/Mac/.test(navigator.platform);
  useEffect(()=>{
    if(!isTauri())return;
    const window=getCurrentWindow();let active=true;const subscriptions:UnlistenFn[]=[];
    const retain=(unlisten:UnlistenFn)=>active?subscriptions.push(unlisten):unlisten();
    const sync=async()=>{const [max,full]=await Promise.all([window.isMaximized(),window.isFullscreen()]);if(active){setMaximized(max);setFullscreen(full);}};
    void sync().catch(onError);
    void window.onResized(()=>void sync().catch(onError)).then(retain).catch(onError);
    void window.onFocusChanged(event=>setFocused(event.payload)).then(retain).catch(onError);
    return()=>{active=false;subscriptions.forEach(unlisten=>unlisten());};
  },[onError]);
  const perform=(action:()=>Promise<void>)=>void action().catch(onError);
  return <header className={`titlebar ${macOS?'macos':''} ${focused?'':'unfocused'} ${fullscreen?'fullscreen':''}`} aria-label="应用标题栏">
    <div className="titlebar-drag" data-tauri-drag-region>
      <img src={brandImage} alt=""/>
      <span>LabInvoiceSystem</span>
    </div>
    {!macOS&&<div className="window-controls" aria-label="窗口控制">
      <button className="window-control" aria-label="最小化窗口" title="最小化" onClick={()=>perform(()=>getCurrentWindow().minimize())}><Minus size={15}/></button>
      <button className="window-control" aria-label={maximized?'还原窗口':'最大化窗口'} title={maximized?'还原':'最大化'} onClick={()=>perform(()=>getCurrentWindow().toggleMaximize())}>{maximized?<Copy size={13}/>:<Square size={13}/>}</button>
      <button className="window-control window-close" aria-label="关闭窗口" title="关闭" onClick={()=>perform(()=>getCurrentWindow().close())}><X size={18}/></button>
    </div>}
  </header>;
}
