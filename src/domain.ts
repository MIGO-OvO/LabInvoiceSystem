import type { Category, Invoice } from './types';
export const categories:Record<Category,string> = {officeLiving:'办公/生活用品',labSupplies:'实验用品',uncategorized:'未分类（历史数据）'};
export const categoryOrder:Category[]=['officeLiving','labSupplies','uncategorized'];
export const money = (cents:number) => new Intl.NumberFormat('zh-CN',{style:'currency',currency:'CNY'}).format(cents/100);
export const bytes = (size:number) => size>=1048576?`${(size/1048576).toFixed(1)} MiB`:`${Math.ceil(size/1024)} KiB`;
export const localToday=()=>{const date=new Date();return `${date.getFullYear()}-${String(date.getMonth()+1).padStart(2,'0')}-${String(date.getDate()).padStart(2,'0')}`;};
export function parseCents(value:string):number|null {
  const text=value.trim().replace(/^[¥￥]/,'').replaceAll(',','');if(!/^\d+(\.\d{0,2})?$/.test(text))return null;
  const [whole,fraction='']=text.split('.');const amount=Number(whole)*100+Number(fraction.padEnd(2,'0'));
  return Number.isSafeInteger(amount)&&amount<=999999999999?amount:null;
}
export function matches(invoice:Invoice,search:string,category:Category|'all',payment:string,from:string,to:string):boolean {
  const needle=search.trim().toLocaleLowerCase();return (category==='all'||invoice.category===category)&&(!payment||invoice.paymentMethod===payment)&&(!from||invoice.purchaseDate>=from)&&(!to||invoice.purchaseDate<=to)&&(!needle||[invoice.fileName,invoice.entryDate,invoice.purchaseDate,invoice.itemName,invoice.paymentMethod,invoice.invoiceNumber,invoice.sellerName,invoice.sellerTaxId,`${invoice.amountCents/100}`,categories[invoice.category],...invoice.sources.flatMap(s=>[s.containerName,s.memberName])].some(value=>value.toLocaleLowerCase().includes(needle)));
}
export interface Group {category:Category;entries:{date:string;purchases:{date:string;payments:{name:string;invoices:Invoice[]}[]}[]}[]}
export function groupInvoices(invoices:Invoice[]):Group[] {
  return categoryOrder.map(category=>{const records=invoices.filter(i=>i.category===category);const entries=[...new Set(records.map(i=>i.entryDate))].sort().reverse().map(date=>{const entry=records.filter(i=>i.entryDate===date);const purchases=[...new Set(entry.map(i=>i.purchaseDate))].sort().reverse().map(date=>{const purchase=entry.filter(i=>i.purchaseDate===date);const payments=[...new Set(purchase.map(i=>i.paymentMethod))].sort().map(name=>({name,invoices:purchase.filter(i=>i.paymentMethod===name)}));return{date,payments};});return{date,purchases};});return{category,entries};}).filter(group=>group.entries.length>0);
}
export function validateInvoice(invoice:Invoice):string[] {
  const errors:string[]=[];if(!invoice.itemName.trim())errors.push('请填写项目名称');if(invoice.amountCents<=0)errors.push('金额必须大于0');
  for(const [label,value] of [['录入日期',invoice.entryDate],['购买日期',invoice.purchaseDate]]){if(!/^\d{4}-\d{2}-\d{2}$/.test(value)||Number.isNaN(Date.parse(value))||new Date(`${value}T12:00:00Z`).toISOString().slice(0,10)!==value)errors.push(`${label}无效`);}
  if(!invoice.paymentMethod.trim())errors.push('请填写支付方式');if(!invoice.reviewed)errors.push('请确认已核对原件');return errors;
}
