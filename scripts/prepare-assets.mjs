import {readdir,readFile,mkdir,copyFile,writeFile} from 'node:fs/promises';
import path from 'node:path';
import {createHash} from 'node:crypto';
import assert from 'node:assert/strict';

const [platform,target,bundleDirectory,output]=process.argv.slice(2);
assert.ok(['windows-x86_64','darwin-x86_64','darwin-aarch64'].includes(platform));
const {version}=JSON.parse(await readFile('package.json','utf8'));
async function files(directory){const result=[];for(const entry of await readdir(directory,{withFileTypes:true})){const file=path.join(directory,entry.name);if(entry.isDirectory())result.push(...await files(file));else result.push(file);}return result;}
const bundled=await files(bundleDirectory);await mkdir(output,{recursive:true});
const suffix=platform==='windows-x86_64'?'windows_x64':platform==='darwin-x86_64'?'macos_x64':'macos_arm64';
const inventory={version,platform,target,updaterSigned:true,osSigned:platform.startsWith('darwin')&&process.env.APPLE_DISTRIBUTION_VERIFIED==='true',notarized:platform.startsWith('darwin')&&process.env.APPLE_DISTRIBUTION_VERIFIED==='true',artifacts:[]};
for(const kind of platform.startsWith('windows')?['installer']:['installer','updater']){
  const extension=platform.startsWith('windows')?'-setup.exe':kind==='installer'?'.dmg':'.app.tar.gz';
  const candidates=bundled.filter(file=>platform.startsWith('windows')?/[/\\]nsis[/\\].*\.exe$/.test(file):kind==='installer'?file.endsWith('.dmg'):file.endsWith('.app.tar.gz'));
  assert.equal(candidates.length,1,`Expected one ${platform} ${kind}, found ${candidates.length}`);
  const name=`LabInvoiceSystem_${version}_${suffix}${extension}`;await copyFile(candidates[0],path.join(output,name));
  const bytes=await readFile(candidates[0]);const record={kind,name,size:bytes.length,sha256:createHash('sha256').update(bytes).digest('hex')};
  if(kind==='updater'||platform.startsWith('windows')){const signature=await readFile(`${candidates[0]}.sig`,'utf8');assert.ok(signature.trim().length>100,'Missing updater signature');await writeFile(path.join(output,`${name}.sig`),signature.trim()+'\n');record.signature=signature.trim();}
  inventory.artifacts.push(record);
}
await writeFile(path.join(output,`build-${platform}.json`),JSON.stringify(inventory,null,2)+'\n');
console.log(`${platform}: prepared ${inventory.artifacts.length} real bundle artifacts; notarized=${inventory.notarized}`);
