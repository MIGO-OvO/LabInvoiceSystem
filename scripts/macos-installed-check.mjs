import {execFile,spawn} from 'node:child_process';
import {promisify} from 'node:util';
import {access,mkdir,writeFile,readFile} from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import assert from 'node:assert/strict';
const exec=promisify(execFile);assert.equal(process.platform,'darwin');
const dmg=path.resolve(process.argv[2]);const directory=path.resolve('.cache/macos-installed-acceptance');
const output=path.resolve('test-results/macos-install');await mkdir(output,{recursive:true});await mkdir(directory,{recursive:true});
const report={startedAt:new Date().toISOString(),architecture:process.arch,artifact:path.basename(dmg),cases:[],result:'running'};
const pause=ms=>new Promise(resolve=>setTimeout(resolve,ms));let mount,app,exited=false;
try{
  const {stdout}=await exec('hdiutil',['attach','-readonly','-nobrowse','-plist',dmg]);
  const match=stdout.match(/<key>mount-point<\/key>\s*<string>([^<]+)<\/string>/);assert.ok(match,'DMG did not mount');
  mount=match[1].replaceAll('&amp;','&').replaceAll('&lt;','<').replaceAll('&gt;','>');
  const installed=path.join(directory,'LabInvoiceSystem.app');await assert.rejects(access(installed),'QA installation directory must be empty');
  await exec('ditto',[path.join(mount,'LabInvoiceSystem.app'),installed]);report.cases.push('Actual DMG mounts and app copies to isolated installation directory');
  await exec('codesign',['--verify','--deep','--strict',installed]);report.cases.push('Installed application code signature is structurally valid (distribution status recorded separately)');
  const executable=path.join(installed,'Contents/MacOS/lab-invoice-system');await access(executable);
  let diagnostic='';app=spawn(executable,[],{stdio:['ignore','ignore','pipe']});app.stderr.on('data',bytes=>diagnostic+=bytes.toString());app.on('exit',()=>exited=true);app.on('error',()=>exited=true);
  const database=path.join(os.homedir(),'Library/Application Support/dev.migo.labinvoicesystem/invoices.sqlite3');
  for(let retry=0;retry<100;retry++){if(exited)throw new Error(`Installed app exited during launch: ${diagnostic}`);if(await access(database).then(()=>true,()=>false))break;await pause(100);}
  await access(database);await pause(5000);assert.equal(exited,false,`Installed app failed: ${diagnostic}`);
  const header=(await readFile(database)).subarray(0,16).toString();assert.equal(header,'SQLite format 3\0');
  report.cases.push('Installed native application stays running and initializes real SQLite storage');
  report.limitation='Runner smoke test does not verify interactive WKWebView behavior or end-user Gatekeeper acceptance.';report.result='passed';
}catch(error){report.result='failed';report.error=error.stack??String(error);console.error(report.error);process.exitCode=1;}
finally{app?.kill('SIGTERM');if(mount)await exec('hdiutil',['detach',mount]).catch(()=>{});report.finishedAt=new Date().toISOString();await writeFile(path.join(output,`report-${process.arch}.json`),JSON.stringify(report,null,2)+'\n');console.log(`macOS install ${process.arch}: ${report.result}, ${report.cases.length} checks`);}
