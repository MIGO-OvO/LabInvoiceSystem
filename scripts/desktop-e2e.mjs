import { chromium, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { PDFDocument, StandardFonts, rgb } from 'pdf-lib';
import { mkdir, writeFile, readFile } from 'node:fs/promises';
import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import assert from 'node:assert/strict';

// This drives the actual Tauri WebView and Rust commands. No injected backend.
const root = fileURLToPath(new URL('../', import.meta.url));
const qa = path.join(root, '.cache', `desktop-qa-${Date.now()}`);
const output = path.join(root, 'test-results', 'desktop');
const screenshotDir = path.join(root, 'portal', 'assets');
const executable = path.join(root, 'target', 'debug', process.platform === 'win32' ? 'lab-invoice-system.exe' : 'lab-invoice-system');
await Promise.all([mkdir(qa, {recursive:true}), mkdir(output, {recursive:true}), mkdir(screenshotDir, {recursive:true})]);
const report = {startedAt:new Date().toISOString(),platform:process.platform,cases:[],accessibility:[],consoleErrors:[],screenshots:[]};
let app, vite, browser, page;
const pause = ms => new Promise(resolve => setTimeout(resolve, ms));
async function until(action, timeout = 30000) {
  const deadline = Date.now() + timeout;
  let last;
  while (Date.now() < deadline) {try {const result = await action(); if (result) return result;} catch (error) {last = error;} await pause(100);}
  throw last ?? new Error('Timed out waiting for the desktop service');
}
async function stopApp() {await browser?.close().catch(()=>{});browser=null; if(app){app.kill();await pause(700);app=null;}}
async function startApp(directory) {
  await stopApp();
  app = spawn(executable, [], {cwd:root, windowsHide:true, stdio:['ignore','pipe','pipe'], env:{...process.env, LABINVOICE_DATA_DIR:directory, WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS:'--remote-debugging-port=9223'}});
  app.on('error', error => {report.launchError=error.message;});
  app.stdout.on('data',bytes=>{report.launchOutput=(report.launchOutput??'')+bytes.toString();});
  app.stderr.on('data',bytes=>{report.launchOutput=(report.launchOutput??'')+bytes.toString();});
  app.on('exit',(code,signal)=>{report.lastExit={code,signal};});
  browser = await until(async()=>{try{return await chromium.connectOverCDP('http://127.0.0.1:9223');}catch(error){report.cdpConnectionError=error.message;return null;}},45000);
  page = await until(()=>browser.contexts().flatMap(c=>c.pages()).find(p=>p.url().includes(':1420')));
  page.on('pageerror',error=>report.consoleErrors.push(error.message));
  await page.waitForFunction(()=>!!window.__TAURI_INTERNALS__?.invoke);
  await page.getByRole('heading',{name:'发票录入',exact:true}).waitFor();
}
const invoke = (command,args={}) => page.evaluate(({command,args})=>window.__TAURI_INTERNALS__.invoke(command,args),{command,args});
const snapshot = () => invoke('get_snapshot');
async function completedImport(paths) {
  const id = await invoke('start_import',{paths});
  const job = await until(async()=>{const job=await invoke('get_job',{id});return ['queued','running'].includes(job.state)?null:job;});
  await until(async()=>!(await snapshot()).busy);
  return job;
}
async function step(name, action) {await action();report.cases.push({name,result:'passed'});console.log(`PASS ${name}`);}
async function screenshot(name) {
  await page.screenshot({path:path.join(screenshotDir,`desktop-${name}.png`)});
  report.screenshots.push(`portal/assets/desktop-${name}.png`);
}
async function accessibility(name) {
  const scan = await new AxeBuilder({page}).withTags(['wcag2a','wcag2aa','wcag21aa']).analyze();
  report.accessibility.push({name,violations:scan.violations.map(v=>({id:v.id,impact:v.impact,description:v.description,nodes:v.nodes.map(n=>({target:n.target,summary:n.failureSummary}))}))});
}

try {
  // A managed Vite process is needed only by the actual debug binary.
  const reachable = await fetch('http://127.0.0.1:1420').then(r=>r.ok).catch(()=>false);
  if(!reachable){vite=spawn(process.execPath,[path.join(root,'node_modules/vite/bin/vite.js'),'--host','127.0.0.1','--port','1420','--strictPort'],{cwd:root,windowsHide:true,stdio:'ignore'});await until(async()=>fetch('http://127.0.0.1:1420').then(r=>r.ok).catch(()=>false));}
  const inputs=path.join(qa,'synthetic-invoices');await mkdir(inputs);
  const names=['实验试剂（合成验收）','移液枪头（合成验收）','滤膜（合成验收）','试管（合成验收）','打印纸（合成验收）','收纳盒（合成验收）'];
  const files=[];
  for(let i=0;i<names.length;i++) {
    const pdf=await PDFDocument.create();const font=await pdf.embedFont(StandardFonts.Helvetica);const sheet=pdf.addPage([595,842]);
    sheet.drawText('SYNTHETIC QA INVOICE',{x:48,y:770,size:22,font,color:rgb(.16,.19,.27)});
    sheet.drawText('No real invoice or personal data',{x:48,y:736,size:13,font});
    sheet.drawText(`QA-${String(i+1).padStart(4,'0')} | CNY ${(100+i*23.5).toFixed(2)}`,{x:48,y:692,size:16,font});
    sheet.drawText('For acceptance testing and product screenshots only.',{x:48,y:660,size:12,font});
    if(i===0){const second=pdf.addPage([595,842]);second.drawText('SYNTHETIC QA - PAGE 2',{x:48,y:770,size:18,font});}
    const file=path.join(inputs,`QA-${i+1}.pdf`);await writeFile(file,await pdf.save());files.push(file);
    await writeFile(file.replace(/\.pdf$/,'.json'),JSON.stringify({EntryDate:'2026-10-12',InvoiceDate:`2026-10-${String(5+i).padStart(2,'0')}`,Amount:100+i*23.5,ItemName:names[i],PaymentMethod:i%2?'微信':'公务卡',InvoiceNumber:`QA-${String(i+1).padStart(4,'0')}`,SellerName:'合成验收销售方',SellerTaxId:'QA-NO-REAL-TAX-ID'}));
  }
  const personal=path.join(qa,'personal');await startApp(personal);
  await step('legacy JSON import retains dates and historical category',async()=>{
    const job=await completedImport(files);assert.equal(job.success,6);const data=await snapshot();assert.equal(data.invoices.length,6);assert.ok(data.invoices.every(i=>i.category==='uncategorized'&&i.entryDate==='2026-10-12'&&!i.reviewed));
  });
  await step('cloud OCR refuses upload before consent',async()=>{
    const id=(await snapshot()).invoices[0].id;const result=await invoke('recognize_invoice',{id}).then(()=>null,String);assert.match(result,/明确同意/);assert.equal((await snapshot()).settings.monthlyUsage,0);
  });
  await step('local PDF preview, pagination and zoom use real original',async()=>{
    const item=page.getByRole('button').filter({hasText:names[0]}).first();await item.click();
    await page.getByLabel('PDF 原件第1页').waitFor();await page.getByLabel('PDF 下一页').click();await page.getByLabel('PDF 原件第2页').waitFor();await page.getByLabel('PDF 上一页').click();await page.getByLabel('放大原件').click();await expect(page.locator('output')).toHaveText('120%');await page.getByLabel('适应窗口').click();
  });
  await step('manual field editing, explicit review, validation and archive',async()=>{
    await page.getByLabel('业务分类').selectOption('labSupplies');
    await page.getByLabel('金额（元）',{exact:true}).fill('101.25');
    await page.getByLabel('我已核对原件、日期、金额、支付方式与分类').check();
    await page.getByRole('button',{name:'归档这张',exact:true}).click();
    await until(async()=>{const invoice=(await snapshot()).invoices.find(i=>i.invoiceNumber==='QA-0001');return invoice.state==='archived'&&invoice.amountCents===10125;});
  });
  await step('batch category, manual review and transaction archive',async()=>{
    const data=await snapshot();const lab=data.invoices.filter(i=>i.state==='draft'&&['QA-0002','QA-0003','QA-0004'].includes(i.invoiceNumber));const office=data.invoices.filter(i=>i.state==='draft'&&!lab.some(l=>l.id===i.id));
    await invoke('classify_invoices',{ids:lab.map(i=>i.id),category:'labSupplies'});await invoke('classify_invoices',{ids:office.map(i=>i.id),category:'officeLiving'});
    await page.getByRole('button',{name:'批量核对确认',exact:true}).click();await page.getByRole('button',{name:'确认所选已核对',exact:true}).click();
    await page.getByRole('button',{name:'归档所选',exact:true}).click();await until(async()=>(await snapshot()).statistics.total.count===6);
  });
  await step('search, archive editing, category totals and themes',async()=>{
    await page.getByRole('button',{name:'导出与归档',exact:true}).click();await page.getByLabel('搜索归档发票').fill('QA-0001');await expect(page.locator('tbody tr')).toHaveCount(1);
    await page.getByLabel(`预览并编辑 ${names[0]}`).click();await page.getByLabel('项目名称',{exact:true}).fill('实验试剂（合成验收，已编辑）');await page.getByRole('button',{name:'保存修改',exact:true}).click();
    await until(async()=>(await snapshot()).invoices.find(i=>i.invoiceNumber==='QA-0001').itemName.includes('已编辑'));
    await page.getByLabel('关闭归档编辑').click();await page.getByLabel('搜索归档发票').fill('');await accessibility('archive-dark');await screenshot('archive');
    await page.getByRole('button',{name:'统计仪表盘',exact:true}).click();await expect(page.getByRole('heading',{name:'统计仪表盘',exact:true})).toBeVisible();
    const statistics=(await snapshot()).statistics;assert.equal(statistics.categories.labSupplies.count,4);assert.equal(statistics.categories.officeLiving.count,2);assert.equal(statistics.total.amountCents,95250+125);await screenshot('dashboard');await accessibility('dashboard-dark');
    await page.getByLabel('切换明暗主题').click();await expect(page.locator('html')).toHaveAttribute('data-theme','light');await expect(page.getByLabel('切换明暗主题')).toBeEnabled();await pause(200);await accessibility('dashboard-light');await page.getByLabel('切换明暗主题').click();
  });
  await step('soft delete and UI restore preserve original and totals',async()=>{
    await page.getByRole('button',{name:'导出与归档',exact:true}).click();await page.getByRole('button',{name:'移至已删除',exact:true}).click();await page.getByRole('button',{name:'移至已删除',exact:true}).last().click();
    await until(async()=>(await snapshot()).statistics.total.count===0);await page.getByRole('button',{name:'已删除',exact:true}).click();await page.getByRole('button',{name:'恢复所选',exact:true}).click();await until(async()=>(await snapshot()).statistics.total.count===6);await page.getByRole('button',{name:'返回归档',exact:true}).click();
  });
  const data=await snapshot();const all=data.invoices.filter(i=>i.state==='archived');const labZip=path.join(qa,'submission-lab.zip');const officeZip=path.join(qa,'submission-office.zip');
  await step('real ZIP + XLSX + versioned Manifest export',async()=>{await invoke('export_invoices',{ids:all.filter(i=>i.category==='labSupplies').map(i=>i.id),path:labZip});await invoke('export_invoices',{ids:all.filter(i=>i.category==='officeLiving').map(i=>i.id),path:officeZip});assert.ok((await readFile(labZip)).byteLength>10000);});
  await step('updater drains operations, backs up, rejects edits, then unlocks',async()=>{
    const backup=await invoke('prepare_for_update');assert.ok((await readFile(backup)).byteLength>0);const result=await invoke('save_invoice',{invoice:all[0]}).then(()=>null,String);assert.match(result,/更新/);await invoke('cancel_update_preparation');await invoke('save_invoice',{invoice:all[0]});await invoke('verify_archive');
  });
  await step('restart retains local archives and settings',async()=>{await startApp(personal);assert.equal((await snapshot()).statistics.total.count,6);});
  const receiver=path.join(qa,'receiver');await startApp(receiver);const corrupt=path.join(qa,'corrupt.zip');await writeFile(corrupt,'synthetic invalid zip');
  await step('multiple submission ZIPs: exact roundtrip, independent corrupt ZIP failure',async()=>{
    const job=await completedImport([labZip,officeZip,corrupt]);assert.equal(job.success,6);assert.equal(job.failed,1);const received=(await snapshot()).invoices;
    for(const original of all){const invoice=received.find(i=>i.blobHash===original.blobHash);for(const field of ['fileName','entryDate','purchaseDate','amountCents','category','itemName','paymentMethod','invoiceNumber','sellerName','sellerTaxId'])assert.equal(invoice[field],original[field],field);assert.ok(invoice.sources.length>original.sources.length);assert.equal(await invoke('read_invoice_bytes',{id:invoice.id}),await readFile(files[Number(original.invoiceNumber.slice(-4))-1]).then(b=>b.toString('base64')));}
    await screenshot('import');await accessibility('import-dark');
  });
  await step('cross-batch duplicates, retry and persistent provenance',async()=>{const job=await completedImport([labZip,officeZip]);assert.equal(job.success,0);assert.equal(job.duplicates,6);await invoke('verify_archive');await startApp(receiver);assert.equal((await snapshot()).invoices.length,6);});
  await step('update preparation cancels an active import and preserves committed data',async()=>{
    const before=(await snapshot()).invoices.map(i=>i.blobHash).sort();
    const id=await invoke('start_import',{paths:Array(128).fill(labZip)});
    const active=await invoke('get_job',{id});assert.ok(['queued','running'].includes(active.state),'The import must actually be active before update preparation');
    const backup=await invoke('prepare_for_update');assert.ok((await readFile(backup)).byteLength>0);
    const finished=await invoke('get_job',{id});assert.equal(finished.state,'cancelled');
    await invoke('cancel_update_preparation');await invoke('verify_archive');
    assert.deepEqual((await snapshot()).invoices.map(i=>i.blobHash).sort(),before);
    const retry=await completedImport([labZip]);assert.equal(retry.duplicates,4);
  });
  const significant=report.accessibility.flatMap(a=>a.violations.filter(v=>['critical','serious'].includes(v.impact)));
  assert.deepEqual(significant,[],'Desktop WCAG AA violations');
  assert.deepEqual(report.consoleErrors,[],'Uncaught desktop JavaScript errors');report.result='passed';
}catch(error){report.result='failed';report.error=error.stack??String(error);if(page)await page.screenshot({path:path.join(output,'failure.png')}).catch(()=>{});console.error(report.error);process.exitCode=1;}
finally {await stopApp();vite?.kill();report.finishedAt=new Date().toISOString();await writeFile(path.join(output,'report.json'),JSON.stringify(report,null,2));console.log(`Report: ${path.join(output,'report.json')}`);}
