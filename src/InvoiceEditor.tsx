import {useState} from 'react';
import {Check,Save,Archive,ScanLine,FileText,ChevronDown} from 'lucide-react';
import type {Category,Invoice} from './types';
import {categories,categoryOrder,money,parseCents,validateInvoice} from './domain';
import {Preview} from './Preview';

export function InvoiceEditor({invoice,busy,onChange,onSave,onArchive,onOcr,onResolve,onError}:{invoice:Invoice;busy:boolean;onChange:(invoice:Invoice)=>void;onSave:()=>void;onArchive:()=>void;onOcr:()=>void;onResolve:(useCandidate:boolean)=>void;onError:(error:unknown)=>void}){
  const [amount,setAmount]=useState((invoice.amountCents/100).toFixed(2));const [validation,setValidation]=useState(false);
  const patch=(data:Partial<Invoice>)=>onChange({...invoice,...data});const errors=validation?validateInvoice(invoice):[];
  return <div className="review-workbench"><header className="review-heading"><div><h2><FileText size={18}/>{invoice.state==='archived'?'归档编辑':'发票核对'}</h2><p title={invoice.fileName}>{invoice.fileName}</p></div><span className={`badge ${invoice.duplicateOf?'warning':''}`}>{invoice.duplicateOf?'待处理冲突':invoice.state==='archived'?'已归档':'待核对'}</span></header>
    <Preview invoice={invoice} onError={onError}/>
    <form className="invoice-form" onSubmit={event=>{event.preventDefault();setValidation(true);if(!validateInvoice(invoice).length)onArchive();}}>
      <fieldset disabled={busy||invoice.state==='deleted'}><legend>核对信息</legend><div className="form-grid">
        <label className="field full">业务分类<select value={invoice.category} onChange={e=>patch({category:e.target.value as Category})}>{categoryOrder.map(category=><option key={category} value={category}>{categories[category]}</option>)}</select></label>
        <label className="field">发票录入日期<input type="date" value={invoice.entryDate} onChange={e=>patch({entryDate:e.target.value})} required/></label>
        <label className="field">购买日期<input type="date" value={invoice.purchaseDate} onChange={e=>patch({purchaseDate:e.target.value})} required/></label>
        <label className="field">金额（元）<input inputMode="decimal" value={amount} aria-invalid={parseCents(amount)===null||invoice.amountCents<=0} onChange={e=>{setAmount(e.target.value);patch({amountCents:parseCents(e.target.value)??0});}} required/></label>
        <label className="field">支付方式<input list="payment-methods" value={invoice.paymentMethod} onChange={e=>patch({paymentMethod:e.target.value})} required/><datalist id="payment-methods"><option value="公务卡"/><option value="微信"/><option value="支付宝"/><option value="现金"/><option value="银行卡"/></datalist></label>
        <label className="field full">项目名称<input value={invoice.itemName} maxLength={300} onChange={e=>patch({itemName:e.target.value})} required/></label>
        <label className="field full">发票号码<input value={invoice.invoiceNumber} maxLength={128} onChange={e=>patch({invoiceNumber:e.target.value})}/></label>
        <label className="field">销售方名称<input value={invoice.sellerName} maxLength={300} onChange={e=>patch({sellerName:e.target.value})}/></label>
        <label className="field">销售方税号<input value={invoice.sellerTaxId} maxLength={128} onChange={e=>patch({sellerTaxId:e.target.value})}/></label>
      </div><label className="check-label"><input type="checkbox" checked={invoice.reviewed} onChange={e=>patch({reviewed:e.target.checked})}/><span>我已核对原件、日期、金额、支付方式与分类</span></label></fieldset>
      {errors.length>0&&<div className="validation error" role="alert">{errors.join('；')}</div>}
      {invoice.duplicateOf&&<section className="conflict"><h3>重复核对</h3><p>候选与已有记录 {invoice.duplicateOf.slice(0,8)} 冲突。合并仅更新核对信息，已有原件保持保存。</p><div className="button-row"><button type="button" disabled={busy} onClick={()=>onResolve(false)}>保留已有，合并来源</button><button type="button" disabled={busy||!invoice.reviewed} onClick={()=>{setValidation(true);if(!validateInvoice(invoice).length)onResolve(true);}}>采用已核对信息</button></div><label className="field">若确为不同发票，填写保留理由<input value={invoice.duplicateReason} onChange={e=>patch({duplicateReason:e.target.value})} placeholder="至少4字，归档时记录审计理由"/></label><p className="muted">相同文件不能重复归档。</p></section>}
      <div className="editor-actions"><button type="button" disabled={busy} onClick={onSave}><Save size={16}/>保存{invoice.state==='archived'?'修改':'草稿'}</button>{invoice.state==='draft'&&<><button type="button" disabled={busy} onClick={onOcr}><ScanLine size={16}/>云端 OCR / 重试</button><button className="primary" disabled={busy} type="submit"><Archive size={16}/>归档这张</button></>}{invoice.state==='archived'&&<span className="muted"><Check size={14}/>金额 {money(invoice.amountCents)}</span>}</div>
    </form>
    <details className="provenance"><summary><ChevronDown size={15}/>来源与核对提示（{invoice.sources.length}）</summary><ul>{invoice.warnings.map((warning,index)=><li key={index}>{warning}</li>)}</ul>{invoice.sources.map((source,index)=><div className="source" key={`${source.batchId}-${index}`}><strong>{source.containerName}</strong><span>{source.memberName}</span><small>{source.metadataKind} · 批次 {source.batchId.slice(0,8)}</small></div>)}<p className="hash">SHA-256 {invoice.blobHash}</p></details>
  </div>;
}
