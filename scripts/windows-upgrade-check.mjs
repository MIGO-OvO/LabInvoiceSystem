import {chromium,expect} from '@playwright/test';
import {PDFDocument,StandardFonts} from 'pdf-lib';
import {spawn,execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {mkdir,copyFile,writeFile,readFile,access,rename,cp} from 'node:fs/promises';
import path from 'node:path';
import assert from 'node:assert/strict';
import {collectWebviewDiagnostics,webviewArguments} from './webview-diagnostics.mjs';

// Uses an actual older application and the REAL current public Release.
// No test update server, fake manifest or alternate signature key.
assert.equal(process.platform,'win32');
const {version}=JSON.parse(await readFile('package.json','utf8'));
const installer=path.resolve(process.argv[2]);const bootstrapArgument=process.argv[3]??'.cache/updater-bootstrap/lab-invoice-system.exe';const bootstrap=bootstrapArgument==='installed'?null:path.resolve(bootstrapArgument);
const bootstrapVersion=process.argv[4]??'3.0.0-qa.0';
const directory=path.resolve('.cache/windows-upgrade-acceptance'),output=path.resolve('test-results/windows-upgrade');
await mkdir(directory,{recursive:true});await mkdir(output,{recursive:true});
const report={startedAt:new Date().toISOString(),bootstrapVersion,targetVersion:version,endpoint:'https://github.com/MIGO-OvO/LabInvoiceSystem/releases/latest/download/latest.json',cases:[],result:'running'};
const pause=ms=>new Promise(resolve=>setTimeout(resolve,ms));
const run=(exe,args=[])=>new Promise((resolve,reject)=>{const p=spawn(exe,args,{stdio:'ignore',windowsHide:true});p.on('error',reject);p.on('exit',code=>code===0?resolve():reject(new Error(`Exit ${code}: ${path.basename(exe)}`)));});
async function until(action,timeout=90000){const end=Date.now()+timeout;let last;while(Date.now()<end){try{const result=await action();if(result)return result;}catch(error){last=error;}await pause(200);}throw last??new Error('Upgrade acceptance timed out');}
const executable=path.join(directory,'lab-invoice-system.exe');let app,browser,page,dataRoot,testId;
const exec=promisify(execFile);const ownedExecutable=executable.replaceAll("'","''");
async function installedProcess(){
  const {stdout}=await exec('powershell.exe',['-NoProfile','-NonInteractive','-Command',`$ownedExecutable='${ownedExecutable}'; $ownedApps=@(Get-CimInstance Win32_Process -Filter "Name = 'lab-invoice-system.exe'" | Where-Object ExecutablePath -eq $ownedExecutable); if($ownedApps.Count -eq 1){[pscustomobject]@{pid=$ownedApps[0].ProcessId;path=$ownedApps[0].ExecutablePath;version=(Get-Item -LiteralPath $ownedExecutable).VersionInfo.ProductVersion}|ConvertTo-Json -Compress}`],{windowsHide:true});
  return stdout.trim()?JSON.parse(stdout):null;
}
const invoke=(command,args={})=>page.evaluate(({command,args})=>window.__TAURI_INTERNALS__.invoke(command,args),{command,args});
async function connect(){browser=await until(()=>chromium.connectOverCDP('http://127.0.0.1:9225').catch(()=>null));page=await until(()=>browser.contexts().flatMap(c=>c.pages()).find(p=>/tauri\.localhost|tauri:\/\//.test(p.url())));await page.getByRole('heading',{name:'发票录入',exact:true}).waitFor();}
try{
  if(bootstrap)await access(bootstrap);await run(installer,['/S',`/D=${directory}`]);await access(executable);await access(path.join(directory,'uninstall.exe'));if(bootstrap)await copyFile(bootstrap,executable);
  app=spawn(executable,[],{windowsHide:true,env:{...process.env,WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS:webviewArguments(9225,output)}});await connect();
  const initial=await invoke('get_snapshot');assert.equal(initial.version,bootstrapVersion);assert.equal(initial.invoices.length,0,'Upgrade QA must never use existing user invoices');dataRoot=initial.dataDirectory;
  const expectedRoot=path.resolve(process.env.APPDATA,'dev.migo.labinvoicesystem');assert.equal(path.resolve(dataRoot),expectedRoot,'Unexpected data location');
  const pdf=await PDFDocument.create();const font=await pdf.embedFont(StandardFonts.Helvetica);pdf.addPage().drawText('SYNTHETIC UPDATE QA - NO REAL INVOICE',{x:40,y:730,font,size:18});const original=path.join(directory,'synthetic-update.pdf');await writeFile(original,await pdf.save());
  const jobId=await invoke('start_import',{paths:[original]});await until(async()=>{const job=await invoke('get_job',{id:jobId});return !['queued','running'].includes(job.state);});
  let invoice=(await invoke('get_snapshot')).invoices[0];Object.assign(invoice,{category:'labSupplies',entryDate:'2026-10-12',purchaseDate:'2026-10-10',amountCents:12345,itemName:'更新验收（合成发票）',paymentMethod:'公务卡',invoiceNumber:'SYNTHETIC-UPDATER-QA-001',reviewed:true});invoice=await invoke('save_invoice',{invoice});testId=invoice.id;await invoke('archive_invoices',{ids:[testId]});
  await invoke('save_settings',{settings:{...initial.settings,theme:'light',monthlyQuota:321,itemCorrections:{'SYNTHETIC-UPDATER-QA':'合成项目修正'}}});await page.reload();await page.getByRole('heading',{name:'发票录入',exact:true}).waitFor();
  const before=await invoke('get_snapshot');assert.equal(before.statistics.total.amountCents,12345);const originalBytes=await invoke('read_invoice_bytes',{id:testId});report.cases.push('Real synthetic invoice archived before upgrade');
  await page.getByRole('button',{name:'设置与辅助操作',exact:true}).click();await page.getByRole('button',{name:'手动检查更新',exact:true}).click();await page.getByRole('button',{name:'立即更新',exact:true}).waitFor({timeout:45000});
  const metadata=await invoke('plugin:updater|check',{timeout:15000});assert.equal(metadata.currentVersion,bootstrapVersion);assert.equal(metadata.version,version);report.cases.push(`Official updater discovers the real public v${version} manifest`);
  await page.getByRole('button',{name:'稍后提醒',exact:true}).click();await until(async()=>(await invoke('get_snapshot')).settings.remindAfter>Date.now()/1000);report.cases.push('Later reminder persists');
  await page.getByRole('button',{name:'手动检查更新',exact:true}).click();await page.getByRole('button',{name:'跳过此版本',exact:true}).click();await until(async()=>(await invoke('get_snapshot')).settings.skippedVersion===version);report.cases.push('Skip version persists and manual check can override it');
  await page.getByRole('button',{name:'手动检查更新',exact:true}).click();await page.getByRole('button',{name:'立即更新',exact:true}).waitFor();const expectedSettings=(await invoke('get_snapshot')).settings;await page.screenshot({path:path.join(output,'before-upgrade.png')});
  await page.getByRole('button',{name:'立即更新',exact:true}).click().catch(()=>{});
  await until(()=>app.exitCode!==null,180000);await browser.close().catch(()=>{});browser=null;
  report.nativeAutoRestart=await until(async()=>{const process=await installedProcess();return process&&[version,`${version}.0`].includes(process.version)?process:null;},30000);
  report.cases.push('Official public update automatically starts the installed target-version native executable');
  browser=await until(()=>chromium.connectOverCDP('http://127.0.0.1:9225').catch(()=>null),10000).catch(()=>null);
  if(!browser){
    // NSIS may launch through Explorer, which does not inherit our CDP environment.
    // Prove that automatic restart occurred first; restart only this owned QA process for inspection.
    report.diagnosticRestart='Automatic native restart verified; Explorer did not inherit WebView2 diagnostic flags. The owned QA executable was restarted to inspect its persisted data.';
    await exec('powershell.exe',['-NoProfile','-NonInteractive','-Command',`$ownedExecutable='${ownedExecutable}'; $ownedPid=${report.nativeAutoRestart.pid}; $ownedApp=Get-CimInstance Win32_Process -Filter "ProcessId = $ownedPid"; if($ownedApp.ExecutablePath -ne $ownedExecutable){throw 'Owned upgrade process changed'}; Stop-Process -Id $ownedPid`],{windowsHide:true});
    await until(()=>installedProcess().then(process=>!process));
    app=spawn(executable,[],{windowsHide:true,env:{...process.env,WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS:webviewArguments(9225,output)}});await connect();
  }
  page=await until(()=>browser.contexts().flatMap(c=>c.pages()).find(p=>/tauri\.localhost|tauri:\/\//.test(p.url())));await page.getByRole('heading',{name:'发票录入',exact:true}).waitFor();
  const after=await invoke('get_snapshot');assert.equal(after.version,version);assert.equal(after.invoices.length,1);assert.deepEqual(after.invoices[0],before.invoices[0],'All archived metadata and provenance must survive');assert.equal(after.statistics.total.amountCents,12345);
  for(const [key,value] of Object.entries(expectedSettings))assert.deepEqual(after.settings[key],value,`Saved setting ${key} must survive`);
  if(!Object.hasOwn(expectedSettings,'automaticOcr')){assert.equal(after.settings.automaticOcr,true);assert.equal(after.settings.automaticOcrConsent,false);report.cases.push('Old settings migrate to automatic OCR enabled without expanding upload consent');}
  assert.equal(await invoke('read_invoice_bytes',{id:testId}),originalBytes);await invoke('verify_archive');
  const installed=await invoke('plugin:updater|check',{timeout:15000});assert.equal(installed,null,'Updated production app should be current');report.cases.push('Official updater downloads, verifies signature, installs and restarts production release');report.cases.push('Archive identity, original bytes, category, amount and settings survive the real upgrade');
  const {readdir}=await import('node:fs/promises');assert.ok((await readdir(path.join(dataRoot,'backups'))).some(name=>name.includes('before-update')));report.cases.push('Update preparation creates an actual database backup');
  await page.getByRole('button',{name:'导出与归档',exact:true}).click();await page.screenshot({path:path.join(output,'after-upgrade.png')});
  await invoke('plugin:window|close',{label:'main'}).catch(()=>{});await browser.close().catch(()=>{});browser=null;await pause(1000);await run(path.join(directory,'uninstall.exe'),['/S']);await until(()=>access(executable).then(()=>false,()=>true));
  // Only the dataset proven empty before this test, populated by this test, is moved.
  if(process.env.GITHUB_ACTIONS==='true'){
    // CI checks out on D:, while its synthetic application data is on C:.
    // Preserve the proven QA-only dataset in the artifact without a cross-volume rename.
    const destination=path.resolve(output,'synthetic-data');assert.ok(destination.startsWith(output+path.sep));
    await cp(dataRoot,destination,{recursive:true,errorOnExist:true,force:false});report.syntheticDataArchive='synthetic-data/';
  }else{
    const archiveRoot=path.resolve('../.labinvoice-backups/20261013-v3');const destination=path.resolve(archiveRoot,`windows-upgrade-generated-data-${Date.now()}`);assert.ok(destination.startsWith(archiveRoot+path.sep));await mkdir(archiveRoot,{recursive:true});await rename(dataRoot,destination);
  }
  report.result='passed';
}catch(error){report.result='failed';report.error=error.stack??String(error);await collectWebviewDiagnostics(output,app);if(page)await page.screenshot({path:path.join(output,'failure.png')}).catch(()=>{});console.error(report.error);process.exitCode=1;}
finally{await browser?.close().catch(()=>{});app?.kill();report.finishedAt=new Date().toISOString();await writeFile(path.join(output,'report.json'),JSON.stringify(report,null,2)+'\n');console.log(`Windows public update: ${report.result}, ${report.cases.length} checks`);}
