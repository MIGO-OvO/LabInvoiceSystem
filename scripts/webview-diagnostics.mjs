import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {readFile,writeFile} from 'node:fs/promises';
import path from 'node:path';

// Failure evidence from the real native host; never inspects invoice contents.
export function webviewArguments(port,output){
  return `--remote-debugging-port=${port} --enable-logging --log-file="${path.join(output,'webview2.log')}"`;
}
export async function collectWebviewDiagnostics(output,app){
  const report={hostPid:app?.pid,hostExitCode:app?.exitCode};
  try{
    const {stdout}=await promisify(execFile)('powershell.exe',['-NoProfile','-Command',"Get-CimInstance Win32_Process -Filter \"Name = 'lab-invoice-system.exe' OR Name = 'msedgewebview2.exe'\" | Select-Object Name,ProcessId,ParentProcessId,SessionId,CommandLine | ConvertTo-Json -Depth 3"],{windowsHide:true,timeout:10000});
    report.processes=stdout.trim()?JSON.parse(stdout):[];
  }catch(error){report.processError=error.message;}
  report.browserLog=await readFile(path.join(output,'webview2.log'),'utf8').catch(()=>null);
  await writeFile(path.join(output,'native-diagnostics.json'),JSON.stringify(report,null,2)+'\n');
}
