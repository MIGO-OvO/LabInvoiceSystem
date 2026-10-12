import {chromium,expect} from '@playwright/test';
import {spawn} from 'node:child_process';
import {access,readFile,mkdir,writeFile} from 'node:fs/promises';
import path from 'node:path';
import assert from 'node:assert/strict';

// Installs the real NSIS artifact, opens its embedded production frontend,
// verifies Rust IPC and PDF-independent screens, then uninstalls the QA copy.
assert.equal(process.platform,'win32');
const root=process.cwd(),installer=path.resolve(process.argv[2]??'target/release/bundle/nsis/LabInvoiceSystem_3.0.0_x64-setup.exe');
const directory=path.resolve('.cache/installed-windows-acceptance');
const output=path.resolve('test-results/windows-install');await mkdir(output,{recursive:true});
const run=(exe,args=[])=>new Promise((resolve,reject)=>{const child=spawn(exe,args,{windowsHide:true,stdio:'ignore'});child.on('error',reject);child.on('exit',code=>code===0?resolve():reject(new Error(`Process exit ${code}: ${path.basename(exe)}`)));});
const pause=ms=>new Promise(resolve=>setTimeout(resolve,ms));
const report={startedAt:new Date().toISOString(),installer:path.basename(installer),cases:[],result:'running'};
let browser,app;
try{
  await access(installer);await run(installer,['/S',`/D=${directory}`]);
  const executable=path.join(directory,'lab-invoice-system.exe');await access(executable);report.cases.push('NSIS silent installation into isolated application directory');
  app=spawn(executable,[],{windowsHide:true,env:{...process.env,WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS:'--remote-debugging-port=9224'}});
  app.on('exit',(code,signal)=>{report.exit={code,signal};});app.on('error',error=>{report.launchError=error.message;});
  for(let retry=0;retry<120&&!browser;retry++){browser=await chromium.connectOverCDP('http://127.0.0.1:9224').catch(()=>null);if(!browser)await pause(250);}
  assert.ok(browser,'Installed production WebView did not start');
  let page;for(let retry=0;retry<100&&!page;retry++){page=browser.contexts().flatMap(c=>c.pages()).find(p=>/tauri\.localhost|tauri:\/\//.test(p.url()));if(!page)await pause(100);}
  assert.ok(page,'Embedded production page was not loaded');
  await page.getByRole('heading',{name:'发票录入',exact:true}).waitFor();
  const snapshot=await page.evaluate(()=>window.__TAURI_INTERNALS__.invoke('get_snapshot'));
  assert.equal(snapshot.version,'3.0.0');assert.equal(snapshot.busy,false);
  assert.equal(snapshot.invoices.length,0,'Installed CI smoke test requires an empty generated data directory; never screenshot existing invoices');
  report.cases.push('Installed production executable and embedded React/Rust IPC start successfully');
  await page.getByRole('button',{name:'导出与归档',exact:true}).click();await expect(page.getByRole('heading',{name:'发票导出与归档管理',exact:true})).toBeVisible();
  await page.getByRole('button',{name:'统计仪表盘',exact:true}).click();await expect(page.getByRole('heading',{name:'统计仪表盘',exact:true})).toBeVisible();
  await page.getByRole('button',{name:'设置与辅助操作',exact:true}).click();
  await page.screenshot({path:path.join(output,'installed-production.png')});report.cases.push('Production navigation, statistics and settings operate');
  await browser.close();browser=null;app.kill();app=null;await pause(800);
  await run(path.join(directory,'uninstall.exe'),['/S']);report.cases.push('NSIS uninstall completes');
  for(let retry=0;retry<100;retry++){if(!await access(executable).then(()=>true,()=>false))break;await pause(100);}
  await assert.rejects(access(executable));report.result='passed';
}catch(error){report.result='failed';report.error=error.stack??String(error);console.error(report.error);process.exitCode=1;}
finally{await browser?.close().catch(()=>{});app?.kill();report.finishedAt=new Date().toISOString();await writeFile(path.join(output,'report.json'),JSON.stringify(report,null,2)+'\n');console.log(`Windows install: ${report.result}, ${report.cases.length} checks`);}
