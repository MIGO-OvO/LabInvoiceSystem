import {chromium,expect} from '@playwright/test';
import {spawn,execFile} from 'node:child_process';
import {access,readFile,mkdir,writeFile} from 'node:fs/promises';
import path from 'node:path';
import assert from 'node:assert/strict';
import {webviewArguments,collectWebviewDiagnostics} from './webview-diagnostics.mjs';

// Installs the real NSIS artifact, opens its embedded production frontend,
// verifies Rust IPC and PDF-independent screens, then uninstalls the QA copy.
assert.equal(process.platform,'win32');
const root=process.cwd();
const version=JSON.parse(await readFile(path.join(root,'package.json'),'utf8')).version;
const installer=path.resolve(process.argv[2]??`target/release/bundle/nsis/LabInvoiceSystem_${version}_x64-setup.exe`);
const directory=path.resolve('.cache/installed-windows-acceptance');
const output=path.resolve('test-results/windows-install');await mkdir(output,{recursive:true});
const run=(exe,args=[],timeoutMs=120000)=>new Promise((resolve,reject)=>{const child=spawn(exe,args,{windowsHide:true,stdio:'ignore'});const timeout=setTimeout(()=>{child.kill();reject(new Error(`Process did not finish without user input: ${path.basename(exe)} ${args.join(' ')}`));},timeoutMs);child.on('error',error=>{clearTimeout(timeout);reject(error);});child.on('exit',code=>{clearTimeout(timeout);code===0?resolve():reject(new Error(`Process exit ${code}: ${path.basename(exe)}`));});});
const pause=ms=>new Promise(resolve=>setTimeout(resolve,ms));
const report={startedAt:new Date().toISOString(),installer:path.basename(installer),cases:[],result:'running'};
let browser,app;
try{
  await access(installer);await run(installer,['/S',`/D=${directory}`]);
  const executable=path.join(directory,'lab-invoice-system.exe');await access(executable);report.cases.push('NSIS silent installation into isolated application directory');
  app=spawn(executable,[],{windowsHide:true,env:{...process.env,WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS:webviewArguments(9224,output)}});
  app.on('exit',(code,signal)=>{report.exit={code,signal};});app.on('error',error=>{report.launchError=error.message;});
  for(let retry=0;retry<120&&!browser;retry++){browser=await chromium.connectOverCDP('http://127.0.0.1:9224').catch(()=>null);if(!browser)await pause(250);}
  assert.ok(browser,'Installed production WebView did not start');
  let page;for(let retry=0;retry<100&&!page;retry++){page=browser.contexts().flatMap(c=>c.pages()).find(p=>/tauri\.localhost|tauri:\/\//.test(p.url()));if(!page)await pause(100);}
  assert.ok(page,'Embedded production page was not loaded');
  await page.getByRole('heading',{name:'发票录入',exact:true}).waitFor();
  const snapshot=await page.evaluate(()=>window.__TAURI_INTERNALS__.invoke('get_snapshot'));
  assert.equal(snapshot.version,version);assert.equal(snapshot.busy,false);
  assert.equal(snapshot.invoices.length,0,'Installed CI smoke test requires an empty generated data directory; never screenshot existing invoices');
  report.cases.push('Installed production executable and embedded React/Rust IPC start successfully');
  await page.getByRole('button',{name:'导出与归档',exact:true}).click();await expect(page.getByRole('heading',{name:'发票导出与归档管理',exact:true})).toBeVisible();
  await page.getByRole('button',{name:'统计仪表盘',exact:true}).click();await expect(page.getByRole('heading',{name:'统计仪表盘',exact:true})).toBeVisible();
  await page.getByRole('button',{name:'设置与辅助操作',exact:true}).click();
  await page.screenshot({path:path.join(output,'installed-production.png')});report.cases.push('Production navigation, statistics and settings operate');
  await browser.close();browser=null;app.kill();app=null;await pause(800);
  // The official updater uses these exact unattended NSIS arguments. An initial
  // silent install has no saved language, so a language selector would block here.
  await run(installer,['/P','/UPDATE','/R','/ARGS'],60000);
  const ownedPath=executable.replaceAll("'","''");
  await new Promise((resolve,reject)=>execFile('powershell.exe',['-NoProfile','-NonInteractive','-Command',`$ErrorActionPreference = 'Stop'; $testExecutable = '${ownedPath}'; $deadline = [DateTime]::UtcNow.AddSeconds(15); do { $testApps = @(Get-CimInstance Win32_Process -Filter "Name = 'lab-invoice-system.exe'" | Where-Object ExecutablePath -eq $testExecutable); if ($testApps.Count -gt 0) { break }; Start-Sleep -Milliseconds 100 } while ([DateTime]::UtcNow -lt $deadline); if ($testApps.Count -ne 1) { throw 'Passive update did not restart exactly one owned application' }; foreach ($testApp in $testApps) { Stop-Process -Id $testApp.ProcessId }`],{windowsHide:true},error=>error?reject(error):resolve()));
  report.cases.push('Official passive update arguments finish without a language prompt and automatically restart the installed application');await pause(800);
  await run(path.join(directory,'uninstall.exe'),['/S']);report.cases.push('NSIS uninstall completes');
  for(let retry=0;retry<100;retry++){if(!await access(executable).then(()=>true,()=>false))break;await pause(100);}
  await assert.rejects(access(executable));report.result='passed';
}catch(error){report.result='failed';report.error=error.stack??String(error);await collectWebviewDiagnostics(output,app);console.error(report.error);process.exitCode=1;}
finally{await browser?.close().catch(()=>{});app?.kill();report.finishedAt=new Date().toISOString();await writeFile(path.join(output,'report.json'),JSON.stringify(report,null,2)+'\n');console.log(`Windows install: ${report.result}, ${report.cases.length} checks`);}
