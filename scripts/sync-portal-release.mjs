import {readFile,writeFile} from 'node:fs/promises';
import {createHash} from 'node:crypto';
const headers={'Accept':'application/vnd.github+json','User-Agent':'LabInvoiceSystem-portal-build'};
if(process.env.GH_TOKEN)headers.Authorization=`Bearer ${process.env.GH_TOKEN}`;
const response=await fetch('https://api.github.com/repos/MIGO-OvO/LabInvoiceSystem/releases/latest',{headers});
if(response.status===404){console.log('No published release; portal keeps its explicit unavailable state.');process.exit(0);}
if(!response.ok)throw new Error(`Release API returned ${response.status}`);
const release=await response.json();const securityAsset=release.assets.find(a=>a.name==='release-security.json');if(!securityAsset)throw new Error('Published release has no verified security inventory.');
const securityResponse=await fetch(securityAsset.browser_download_url);if(!securityResponse.ok)throw new Error('Cannot read release security inventory');const security=await securityResponse.json();
if(!/^\d+\.\d+\.\d+$/.test(security.version)||release.tag_name!==`v${security.version}`)throw new Error('Release tag and security inventory versions do not match');
const downloads=[];for(const platform of security.platforms){const expected=platform.artifacts.find(a=>a.kind==='installer');const actual=release.assets.find(a=>a.name===expected?.name);if(!actual||actual.size!==expected.size)throw new Error(`Installer is missing or has wrong size: ${platform.platform}`);downloads.push({platform:platform.platform,name:actual.name,url:actual.browser_download_url,size:actual.size,sha256:expected.sha256,notarized:platform.notarized,osSigned:platform.osSigned});}
const attribute=value=>String(value).replace(/&/g,'&amp;').replace(/"/g,'&quot;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
const scriptHash=createHash('sha256').update(await readFile('portal/app.js')).digest('hex').slice(0,12);
let index=await readFile('portal/index.html','utf8'),linkedInstallers=0;
index=index.replace(/<html\b([^>]*)>/,(_,attributes)=>`<html${attributes.replace(/\s+data-release-version="[^"]*"/g,'')} data-release-version="${security.version}">`);
index=index.replace(/<script src="app\.js(?:\?[^"]*)?" defer><\/script>/,`<script src="app.js?v=${scriptHash}" defer></script>`);
index=index.replace(/<a\b([^>]*\bdata-platform="([^"]+)"[^>]*)>/g,(_,attributes,platform)=>{const download=downloads.find(item=>item.platform===platform);if(!download)throw new Error(`No published installer for portal platform: ${platform}`);linkedInstallers++;const title=download.notarized||platform==='windows-x86_64'?`下载 ${download.name}`:`下载未公证预览构建：${download.name}`;return `<a${attributes.replace(/\s+(?:href|title|aria-disabled)="[^"]*"/g,'')} href="${attribute(download.url)}" title="${attribute(title)}">`;});
if(linkedInstallers!==downloads.length)throw new Error('Portal download buttons do not match published installers');
index=index.replace(/(<span id="release-status" role="status">)[\s\S]*?(<\/span>)/,(_,start,end)=>`${start}v${security.version} · 已发布 ${downloads.length} 个架构安装包${end}`);
index=index.replace(/<a id="release-link" href="[^"]*">/,`<a id="release-link" href="${attribute(release.html_url)}">`);
await writeFile('portal/release.json',JSON.stringify({version:security.version,releaseUrl:release.html_url,publishedAt:release.published_at,macosProductionReady:security.macosProductionReady,downloads},null,2)+'\n');
await writeFile('portal/index.html',index);
console.log(`Portal resolves ${downloads.length} existing Release installer assets.`);
