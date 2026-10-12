import {readdir,readFile,writeFile} from 'node:fs/promises';
import path from 'node:path';
import {createHash} from 'node:crypto';
import assert from 'node:assert/strict';

const [directory,notesFile]=process.argv.slice(2);const {version}=JSON.parse(await readFile('package.json','utf8'));
assert.match(version,/^\d+\.\d+\.\d+(?:-[\w.-]+)?$/);
const tag=`v${version}`;const base=`https://github.com/MIGO-OvO/LabInvoiceSystem/releases/download/${tag}/`;
const inventory=[];const platforms={};
for(const platform of ['windows-x86_64','darwin-x86_64','darwin-aarch64']){
  const record=JSON.parse(await readFile(path.join(directory,`build-${platform}.json`),'utf8'));assert.equal(record.version,version);assert.equal(record.platform,platform);assert.equal(record.updaterSigned,true);
  for(const artifact of record.artifacts){const bytes=await readFile(path.join(directory,artifact.name));assert.equal(bytes.length,artifact.size);assert.equal(createHash('sha256').update(bytes).digest('hex'),artifact.sha256);if(artifact.signature)assert.equal((await readFile(path.join(directory,`${artifact.name}.sig`),'utf8')).trim(),artifact.signature);artifact.url=base+encodeURIComponent(artifact.name);}
  const update=record.artifacts.find(a=>a.signature);assert.ok(update);
  // Do not advertise unsigned/unnotarized macOS builds as production updates.
  if(platform==='windows-x86_64'||record.osSigned&&record.notarized)platforms[platform]={url:update.url,signature:update.signature};
  inventory.push(record);
}
const latest={version,notes:await readFile(notesFile,'utf8'),pub_date:new Date().toISOString(),platforms};
await writeFile(path.join(directory,'latest.json'),JSON.stringify(latest,null,2)+'\n');
await writeFile(path.join(directory,'release-security.json'),JSON.stringify({version,platforms:inventory,macosProductionReady:inventory.filter(r=>r.platform.startsWith('darwin')).every(r=>r.osSigned&&r.notarized),windowsAuthenticodeSigned:false},null,2)+'\n');
const sums=[];for(const name of (await readdir(directory)).sort()){if(name==='SHA256SUMS.txt')continue;const bytes=await readFile(path.join(directory,name));sums.push(`${createHash('sha256').update(bytes).digest('hex')}  ${name}`);}
await writeFile(path.join(directory,'SHA256SUMS.txt'),sums.join('\n')+'\n');
console.log(`v${version}: all three architectures validated; production updater platforms: ${Object.keys(platforms).join(', ')}`);
