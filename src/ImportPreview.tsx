import {useState} from 'react';
import {ChevronDown} from 'lucide-react';
import type {ImportPreviewSource} from './types';
import {categories,categoryOrder,money} from './domain';

function SourcePreview({source,first}:{source:ImportPreviewSource;first:boolean}){
  const [open,setOpen]=useState(first);
  const metadata:Record<string,string>={'manifest-v1':'新格式明细','legacy-json':'旧 JSON 明细','legacy-excel':'旧 Excel 明细',filename:'文件名建议',manual:'待手工录入'};
  return <details className="preview-source" open={open} onToggle={event=>setOpen(event.currentTarget.open)}>
    <summary><ChevronDown size={16} className="preview-disclosure"/><strong>{source.containerName}</strong><span>{source.error?'无法导入':`${source.invoiceCount} 张原件 · 疑似重复 ${source.duplicateCount}`}</span></summary>
    {open&&<div>
      {source.error?<p className="error">{source.error}</p>:<>
        <ul className="preview-totals">{categoryOrder.filter(category=>source.categories[category]).map(category=><li key={category}>{categories[category]}：{source.categories[category]?.count} 张 · {money(source.categories[category]?.amountCents??0)}</li>)}</ul>
        <p className="muted">明细合计包含疑似重复，不能直接作为报账金额；缺失字段需核对。{source.unassignedRows>0&&` 有 ${source.unassignedRows} 行 Excel 需要在队列中人工匹配。`}</p>
        <div className="table-scroll" role="region" tabIndex={0} aria-label={`${source.containerName} 导入明细预览`}>
          <table><thead><tr><th scope="col">原件 / 项目</th><th scope="col">分类</th><th scope="col">录入 / 购买日期</th><th scope="col" className="numeric">金额</th><th scope="col">支付 / 发票号</th></tr></thead>
            <tbody>{source.invoices.map((invoice,index)=><tr key={index}>
              <td><strong>{invoice.fileName}</strong>{invoice.memberName!==invoice.fileName&&<small>{invoice.memberName}</small>}<small>{invoice.itemName||'项目待核对'}</small><small>{metadata[invoice.metadataKind]??'待核对'}{invoice.memberIndex!==null&&` · 包内条目 ${invoice.memberIndex+1}`}{invoice.duplicate&&' · 疑似重复'}</small></td>
              <td>{categories[invoice.category]}</td><td>{invoice.entryDate||'待填写'}<br/>{invoice.purchaseDate||'待填写'}</td>
              <td className="numeric">{invoice.amountCents?money(invoice.amountCents):'待核对'}</td><td>{invoice.paymentMethod||'待填写'}<br/>{invoice.invoiceNumber||'号码待核对'}</td>
            </tr>)}</tbody>
          </table>
        </div>
        {source.invoiceCount>source.invoices.length&&<p className="muted">预览展示前 {source.invoices.length} 张，汇总包含全部原件；完整字段在导入后的核对区显示。</p>}
      </>}
      {source.issues.length>0&&<ul className="issue-list">{source.issues.map((issue,index)=><li key={index}><strong>{issue.member}</strong><span>{issue.message}</span></li>)}</ul>}
    </div>}
  </details>;
}

export function ImportPreview({sources}:{sources:ImportPreviewSource[]}){
  return <div className="import-preview" role="region" tabIndex={0} aria-label="ZIP 来源与预览明细">{sources.map((source,index)=><SourcePreview key={index} source={source} first={index===0}/>)}</div>;
}
