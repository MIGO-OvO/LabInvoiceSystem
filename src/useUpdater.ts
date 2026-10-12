import {useCallback,useEffect,useRef,useState} from 'react';
import {check,type Update} from '@tauri-apps/plugin-updater';
import {invoke} from '@tauri-apps/api/core';
import {isTauri} from './api';
import type {Settings} from './types';
export interface UpdateState {phase:'idle'|'checking'|'current'|'available'|'preparing'|'downloading'|'installing'|'error';version?:string;notes?:string;message?:string;downloaded:number;total?:number}
export function useUpdater(settings:Settings|undefined,save:(value:Settings)=>Promise<void>,flush:()=>Promise<void>){
  const [state,setState]=useState<UpdateState>({phase:'idle',downloaded:0});const pending=useRef<Update|null>(null);const checking=useRef(false);const config=useRef(settings);config.current=settings;
  const checkingUpdates=useCallback(async(manual=false)=>{if(!isTauri()||checking.current||['preparing','downloading','installing'].includes(state.phase))return;checking.current=true;setState({phase:'checking',downloaded:0});
    try{const update=await check({timeout:15000});await pending.current?.close();pending.current=null;
      if(!update){setState({phase:manual?'current':'idle',message:'当前已是最新版本',downloaded:0});return;}
      if(!manual&&(config.current?.skippedVersion===update.version||(config.current?.remindAfter??0)>Date.now()/1000)){await update.close();setState({phase:'idle',downloaded:0});return;}
      pending.current=update;setState({phase:'available',version:update.version,notes:update.body,downloaded:0});
    }catch{setState({phase:manual?'error':'idle',message:manual?'暂时无法检查更新，请稍后重试。归档功能可继续使用。':undefined,downloaded:0});}finally{checking.current=false;}
  },[state.phase]);
  const checker=useRef(checkingUpdates);checker.current=checkingUpdates;
  useEffect(()=>{if(!isTauri())return;const startup=setTimeout(()=>void checker.current(false),4500);const periodic=setInterval(()=>void checker.current(false),12*60*60*1000);return()=>{clearTimeout(startup);clearInterval(periodic);void pending.current?.close();};},[]);
  const install=async()=>{const update=pending.current;if(!update)return;setState(current=>({...current,phase:'preparing'}));let downloaded=0;let total:number|undefined;
    try{await flush();await invoke('prepare_for_update');setState(current=>({...current,phase:'downloading'}));await update.downloadAndInstall(event=>{if(event.event==='Started'){total=event.data.contentLength;}else if(event.event==='Progress'){downloaded+=event.data.chunkLength;}setState(current=>({...current,phase:event.event==='Finished'?'installing':'downloading',downloaded,total}));},{timeout:120000});await invoke('restart_after_update');}
    catch{await invoke('cancel_update_preparation').catch(()=>{});await pending.current?.close().catch(()=>{});pending.current=null;setState({phase:'error',downloaded:0,message:'更新未完成，操作锁已释放，已保存的归档和草稿可继续使用。请重新检查后重试。'});}
  };
  const dismiss=async(skip:boolean)=>{if(config.current){await save({...config.current,skippedVersion:skip?(pending.current?.version??''):config.current.skippedVersion,remindAfter:skip?0:Math.floor(Date.now()/1000)+24*60*60});}await pending.current?.close();pending.current=null;setState({phase:'idle',downloaded:0});};
  return{state,check:checkingUpdates,install,later:()=>dismiss(false),skip:()=>dismiss(true)};
}
