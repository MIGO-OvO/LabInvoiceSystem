import {useEffect,useRef,useState} from 'react';
import {ChevronLeft,ChevronRight,ZoomIn,ZoomOut,Maximize2,ExternalLink} from 'lucide-react';
import {api} from './api';
import type {Invoice} from './types';
import type {PDFDocumentProxy} from 'pdfjs-dist';
import workerUrl from 'pdfjs-dist/build/pdf.worker.min.mjs?url';

export function Preview({invoice,onError}:{invoice:Invoice;onError:(error:unknown)=>void}){
  const [pdf,setPdf]=useState<PDFDocumentProxy|null>(null);const [image,setImage]=useState('');const [page,setPage]=useState(1);const [zoom,setZoom]=useState(1);const [loading,setLoading]=useState(true);const [failure,setFailure]=useState('');const canvas=useRef<HTMLCanvasElement>(null);const viewport=useRef<HTMLDivElement>(null);const drag=useRef<{x:number;y:number;left:number;top:number}|null>(null);
  useEffect(()=>{let active=true;let document:PDFDocumentProxy|undefined;let objectUrl='';setPdf(null);setImage('');setPage(1);setZoom(1);setFailure('');setLoading(true);
    void api.read(invoice.id).then(async encoded=>{const bytes=Uint8Array.from(atob(encoded),c=>c.charCodeAt(0));if(!active)return;
      if(invoice.extension==='pdf'){const lib=await import('pdfjs-dist');lib.GlobalWorkerOptions.workerSrc=workerUrl;document=await lib.getDocument({data:bytes,useSystemFonts:true,enableXfa:false}).promise;if(active)setPdf(document);else await document.destroy();}
      else{objectUrl=URL.createObjectURL(new Blob([bytes],{type:invoice.extension==='png'?'image/png':'image/jpeg'}));if(active)setImage(objectUrl);}
      if(active)setLoading(false);
    }).catch(()=>{if(active){setLoading(false);setFailure('原件预览失败，请检查文件完整性，或使用默认应用打开。');}});
    return()=>{active=false;if(document)void document.destroy();if(objectUrl)URL.revokeObjectURL(objectUrl);};
  },[invoice.id,invoice.blobHash,invoice.extension]);
  useEffect(()=>{if(!pdf||!canvas.current)return;let active=true;let render:ReturnType<Awaited<ReturnType<PDFDocumentProxy['getPage']>>['render']>|undefined;
    void pdf.getPage(page).then(async current=>{if(!active||!canvas.current)return;let scale=Math.min(window.devicePixelRatio||1,2)*1.2;const base=current.getViewport({scale});if(base.width*base.height>12000000)scale*=Math.sqrt(12000000/(base.width*base.height));const view=current.getViewport({scale});const element=canvas.current;element.width=view.width;element.height=view.height;render=current.render({canvas:element,viewport:view});await render.promise;}).catch((error:unknown)=>{if(active&&!(error instanceof Error&&error.name==='RenderingCancelledException'))setFailure('此 PDF 页无法渲染，可使用默认应用查看。');});return()=>{active=false;render?.cancel();};
  },[pdf,page]);
  function reset(){setZoom(1);if(viewport.current){viewport.current.scrollLeft=0;viewport.current.scrollTop=0;}}
  return <section className="preview" aria-label="发票原件预览"><div className="preview-tools">
    <div className="inline"><button className="icon-button" aria-label="缩小原件" onClick={()=>setZoom(z=>Math.max(.1,z-.2))}><ZoomOut size={17}/></button><output>{Math.round(zoom*100)}%</output><button className="icon-button" aria-label="放大原件" onClick={()=>setZoom(z=>Math.min(5,z+.2))}><ZoomIn size={17}/></button><button className="icon-button" aria-label="适应窗口" onClick={reset}><Maximize2 size={17}/></button></div>
    {pdf&&<div className="inline"><button className="icon-button" aria-label="PDF 上一页" disabled={page<=1} onClick={()=>setPage(v=>v-1)}><ChevronLeft size={17}/></button><span>{page} / {pdf.numPages}</span><button className="icon-button" aria-label="PDF 下一页" disabled={page>=pdf.numPages} onClick={()=>setPage(v=>v+1)}><ChevronRight size={17}/></button></div>}
    <button className="icon-button" aria-label="使用默认应用打开原件" onClick={()=>void api.open(invoice.id).catch(onError)}><ExternalLink size={17}/></button>
  </div><div className="preview-viewport" ref={viewport} tabIndex={0} role="region" aria-label="发票原件预览，可滚动" onDoubleClick={reset} onWheel={event=>{if(event.ctrlKey||event.metaKey){event.preventDefault();setZoom(z=>Math.max(.1,Math.min(5,z+(event.deltaY<0?.1:-.1))));}}}
    onPointerDown={event=>{if(!viewport.current||event.button!==0)return;drag.current={x:event.clientX,y:event.clientY,left:viewport.current.scrollLeft,top:viewport.current.scrollTop};event.currentTarget.setPointerCapture(event.pointerId);}}
    onPointerMove={event=>{if(drag.current&&viewport.current){viewport.current.scrollLeft=drag.current.left+drag.current.x-event.clientX;viewport.current.scrollTop=drag.current.top+drag.current.y-event.clientY;}}} onPointerUp={()=>drag.current=null} onPointerCancel={()=>drag.current=null}>
    {loading?<p role="status">正在读取本地原件…</p>:failure?<p className="error">{failure}</p>:<div className="preview-sheet" style={{width:`${zoom*100}%`}}>{pdf?<canvas ref={canvas} aria-label={`PDF 原件第${page}页`}/>:<img src={image} alt="当前发票原件" draggable={false} onError={()=>setFailure('图像已损坏或不能解码，请检查原件并重新导入。')}/>}</div>}
  </div><p className="preview-hint">原件仅在本机预览 · Ctrl / ⌘ + 滚轮缩放 · 拖动平移 · 双击适应</p></section>;
}
