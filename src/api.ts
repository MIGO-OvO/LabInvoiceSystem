import {invoke,isTauri} from '@tauri-apps/api/core';
import {open,save} from '@tauri-apps/plugin-dialog';
import type {Invoice,Snapshot,Category,Settings,ImportJob,ImportPreviewSource} from './types';
export {isTauri};
export const api={
  snapshot:()=>invoke<Snapshot>('get_snapshot'),
  previewImport:(paths:string[])=>invoke<ImportPreviewSource[]>('preview_import',{paths}),
  startImport:(paths:string[])=>invoke<string>('start_import',{paths}),
  job:(id:string)=>invoke<ImportJob>('get_job',{id}),
  cancel:()=>invoke<void>('cancel_operation'),
  retry:(id:string)=>invoke<string>('retry_import',{id}),
  save:(invoice:Invoice)=>invoke<Invoice>('save_invoice',{invoice}),
  archive:(ids:string[])=>invoke<number>('archive_invoices',{ids}),
  classify:(ids:string[],category:Category)=>invoke<number>('classify_invoices',{ids,category}),
  remove:(ids:string[],restore=false)=>invoke<number>('delete_invoices',{ids,restore}),
  undo:(id:string)=>invoke<number>('undo_import',{id}),
  match:(jobId:string,rowId:string,invoiceId:string)=>invoke<Invoice>('match_excel_row',{jobId,rowId,invoiceId}),
  resolve:(id:string,useCandidate:boolean)=>invoke<Invoice>('resolve_conflict',{id,useCandidate}),
  settings:(settings:Settings)=>invoke<void>('save_settings',{settings}),
  read:(id:string)=>invoke<string>('read_invoice_bytes',{id}),
  open:(id:string)=>invoke<void>('open_original',{id}),
  openFolder:(kind='data')=>invoke<void>('open_data_folder',{kind}),
  saveOriginal:(id:string,path:string)=>invoke<void>('save_original',{id,path}),
  export:(ids:string[],path:string)=>invoke<number>('export_invoices',{ids,path}),
  ocr:(id:string)=>invoke<Invoice>('recognize_invoice',{id}),
  credentials:(apiKey:string,secretKey:string)=>invoke<void>('configure_credentials',{apiKey,secretKey}),
  forgetCredentials:()=>invoke<void>('forget_credentials'),
  testCredentials:()=>invoke<void>('test_credentials'),
  previewLegacy:(path:string)=>invoke<string[]>('preview_legacy',{path}),
  importSettings:(path:string)=>invoke<Settings>('import_legacy_settings',{path}),
  log:()=>invoke<[string,string,string][]>('audit_log'),
  verify:()=>invoke<void>('verify_archive'),
};
export async function chooseInvoices(zipOnly=false):Promise<string[]>{const result=await open({title:zipOnly?'批量接收多个 ZIP':'选择发票或 ZIP',multiple:true,filters:[{name:'发票及报账资料',extensions:zipOnly?['zip']:['pdf','jpg','jpeg','png','zip']}]});return result?(Array.isArray(result)?result:[result]):[];}
export async function chooseFolder():Promise<string|null>{const result=await open({directory:true,multiple:false,title:'选择旧 archive_data 或归档年月文件夹'});return typeof result==='string'?result:null;}
export async function chooseExport(directory:string):Promise<string|null>{return save({title:'导出发票与 Excel / Manifest',defaultPath:`${directory?`${directory}/`:''}LabInvoiceSystem-${Date.now()}.zip`,filters:[{name:'报账资料 ZIP',extensions:['zip']}]});}
