export type Category = 'officeLiving' | 'labSupplies' | 'uncategorized';
export interface Source {batchId:string;containerName:string;containerHash:string;memberName:string;memberIndex?:number|null;metadataKind:string}
export interface Invoice {
  id:string;batchId:string;fileName:string;blobHash:string;extension:string;fileSize:number;category:Category;entryDate:string;purchaseDate:string;amountCents:number;itemName:string;paymentMethod:string;invoiceNumber:string;sellerName:string;sellerTaxId:string;state:'draft'|'archived'|'deleted';sources:Source[];warnings:string[];ocrItemName:string;duplicateOf:string|null;previousState:string|null;reviewed:boolean;duplicateReason:string;
}
export interface Issue {container:string;member:string;message:string}
export interface SpreadsheetRow {id:string;container:string;containerHash:string;rowNumber:number;entryDate:string;purchaseDate:string;amountCents:number;itemName:string;paymentMethod:string;invoiceNumber:string;sellerName:string;sellerTaxId:string;category:Category;mappedFile:string}
export interface ImportJob {id:string;state:string;totalContainers:number;processedContainers:number;totalFiles:number;processedFiles:number;bytesRead:number;totalBytes:number;success:number;duplicates:number;failed:number;issues:Issue[];unassignedRows:SpreadsheetRow[];invoiceIds:string[]}
export interface Settings {theme:'light'|'dark'|'system';exportDirectory:string;cloudOcrConsent:boolean;monthlyUsage:number;monthlyQuota:number;usageMonth:string;skippedVersion:string;remindAfter:number;itemCorrections:Record<string,string>}
export interface Totals {count:number;amountCents:number}
export interface Statistics {total:Totals;last30Cents:number;categories:Record<Category,Totals>;daily:Record<string,number>;payments:Record<string,Totals>}
export interface Snapshot {invoices:Invoice[];jobs:ImportJob[];settings:Settings;statistics:Statistics;dataDirectory:string;version:string;credentialsConfigured:boolean;busy:boolean}
export interface ExportProgress {completed:number;total:number;bytesWritten:number;totalBytes:number}
export type Page = 'import'|'export'|'statistics'|'settings';
