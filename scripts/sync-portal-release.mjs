import {writeFile} from 'node:fs/promises';
const headers={'Accept':'application/vnd.github+json','User-Agent':'LabInvoiceSystem-portal-build'};
if(process.env.GH_TOKEN)headers.Authorization=`Bearer ${process.env.GH_TOKEN}`;
const response=await fetch('https://api.github.com/repos/MIGO-OvO/LabInvoiceSystem/releases/latest',{headers});
if(response.status===404){console.log('No published release; portal keeps its explicit unavailable state.');process.exit(0);}
if(!response.ok)throw new Error(`Release API returned ${response.status}`);
const release=await response.json();const securityAsset=release.assets.find(a=>a.name==='release-security.json');if(!securityAsset)throw new Error('Published release has no verified security inventory.');
const securityResponse=await fetch(securityAsset.browser_download_url);if(!securityResponse.ok)throw new Error('Cannot read release security inventory');const security=await securityResponse.json();
const downloads=[];for(const platform of security.platforms){const expected=platform.artifacts.find(a=>a.kind==='installer');const actual=release.assets.find(a=>a.name===expected?.name);if(!actual||actual.size!==expected.size)throw new Error(`Installer is missing or has wrong size: ${platform.platform}`);downloads.push({platform:platform.platform,name:actual.name,url:actual.browser_download_url,size:actual.size,sha256:expected.sha256,notarized:platform.notarized,osSigned:platform.osSigned});}
await writeFile('portal/release.json',JSON.stringify({version:security.version,releaseUrl:release.html_url,publishedAt:release.published_at,macosProductionReady:security.macosProductionReady,downloads},null,2)+'\n');
console.log(`Portal resolves ${downloads.length} existing Release installer assets.`);
