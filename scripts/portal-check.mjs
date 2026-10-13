import {chromium,expect} from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import {createServer} from 'node:http';
import {readFile,mkdir,writeFile} from 'node:fs/promises';
import {fileURLToPath} from 'node:url';
import path from 'node:path';
import assert from 'node:assert/strict';
const root=fileURLToPath(new URL('../',import.meta.url));const directory=path.join(root,'portal');const output=path.join(root,'test-results','portal');await mkdir(output,{recursive:true});
const report={startedAt:new Date().toISOString(),cases:[],consoleErrors:[],networkErrors:[]};
const release=JSON.parse(await readFile(path.join(directory,'release.json'),'utf8'));
// Replay the real 3.0.1 metadata retained in commit cec5519 with Pages' observed 10-minute cache lifetime.
const previousRelease=JSON.parse(await readFile(path.join(root,'scripts','fixtures','portal-release-3.0.1.json'),'utf8'));
let servedRelease=release,releaseRequests=0;
const types={'.html':'text/html; charset=utf-8','.css':'text/css','.js':'text/javascript','.json':'application/json','.png':'image/png','.svg':'image/svg+xml','.woff2':'font/woff2'};
const server=createServer(async(req,res)=>{try{const url=new URL(req.url,'http://localhost');if(url.pathname==='/release.json'){releaseRequests++;res.writeHead(200,{'Content-Type':types['.json'],'Cache-Control':'max-age=600'});res.end(JSON.stringify(servedRelease));return;}if(url.pathname==='/__qa/cache-primer'){res.writeHead(200,{'Content-Type':types['.html'],'Cache-Control':'no-store'});res.end('<!doctype html><title>Priming cache</title><script>fetch("/release.json").then(r=>r.json()).then(data=>{document.title="Cached release "+data.version;});</script>');return;}const file=path.resolve(directory,'.'+decodeURIComponent(url.pathname==='/'?'/index.html':url.pathname));if(!file.startsWith(directory+path.sep)){res.writeHead(403);res.end();return;}const bytes=await readFile(file);res.writeHead(200,{'Content-Type':types[path.extname(file)]??'application/octet-stream'});res.end(bytes);}catch{res.writeHead(404);res.end();}});await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
const browser=await chromium.launch({headless:true});
try{
  for(const [name,width,height,theme]of [['desktop',1440,1000,'light'],['mobile',390,844,'light'],['desktop-dark',1440,1000,'dark'],['mobile-dark',390,844,'dark'],['narrow',320,760,'light'],['tablet',768,1024,'light']]){
    const context=await browser.newContext({viewport:{width,height},colorScheme:theme,reducedMotion:'reduce'});const page=await context.newPage();page.on('pageerror',error=>report.consoleErrors.push(error.message));page.on('response',r=>{if(r.status()>=400)report.networkErrors.push({url:r.url(),status:r.status()});});
    await page.goto(`http://127.0.0.1:${server.address().port}/`);await page.evaluate(()=>document.fonts.ready);await expect(page.locator('#release-status')).not.toHaveAttribute('aria-busy','true');await expect(page.locator('#release-status')).toContainText(`v${release.version}`);
    for(const download of release.downloads)await expect(page.locator(`[data-platform="${download.platform}"]`)).toHaveAttribute('href',download.url);
    assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true,`${name} horizontal overflow`);
    const scan=await new AxeBuilder({page}).withTags(['wcag2a','wcag2aa','wcag21aa']).analyze();assert.deepEqual(scan.violations.map(v=>({id:v.id,impact:v.impact,nodes:v.nodes.map(n=>n.target)})),[],`${name} WCAG AA`);
    await page.screenshot({path:path.join(output,`${name}.png`),fullPage:true});
    if(name==='desktop'){await page.screenshot({path:path.join(output,'desktop-hero.png')});await page.keyboard.press('Tab');await expect(page.locator('.skip-link')).toBeFocused();await page.keyboard.press('Enter');assert.equal(new URL(page.url()).hash,'#main');await page.getByRole('button',{name:'切换为深色主题'}).click();await expect(page.locator('html')).toHaveAttribute('data-theme','dark');await page.reload();await expect(page.locator('html')).toHaveAttribute('data-theme','dark');}
    if(name==='mobile'){await page.screenshot({path:path.join(output,'mobile-hero.png')});const elements=await page.locator('a[href],button,summary').evaluateAll(list=>list.map(el=>{const r=el.getBoundingClientRect();return {tag:el.tagName,text:el.textContent?.trim(),width:r.width,height:r.height};}));assert.ok(elements.filter(e=>e.tag==='BUTTON'||e.tag==='SUMMARY'||/Windows|macOS/.test(e.text)).every(e=>e.height>=44),'Mobile action targets smaller than 44px');}
    report.cases.push({name,width,height,theme,accessibilityViolations:0,horizontalOverflow:false});await context.close();
  }
  const context=await browser.newContext({viewport:{width:1440,height:1000},reducedMotion:'reduce'});const page=await context.newPage();await page.goto(`http://127.0.0.1:${server.address().port}/`);await page.evaluate(()=>document.documentElement.style.fontSize='200%');assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true,'200% text scaling overflow');report.cases.push({name:'200-percent-text',horizontalOverflow:false});await context.close();
  for(const download of release.downloads){const response=await fetch(download.url,{method:'HEAD',signal:AbortSignal.timeout(30000)});assert.ok(response.ok,`Actual download unavailable: ${download.url}`);report.cases.push({name:`download ${download.platform}`,status:response.status,url:download.url});}
  const cacheContext=await browser.newContext();const cachePage=await cacheContext.newPage();servedRelease=previousRelease;
  await cachePage.goto(`http://127.0.0.1:${server.address().port}/__qa/cache-primer`);await expect(cachePage).toHaveTitle(`Cached release ${previousRelease.version}`);
  const requestsAfterPriming=releaseRequests;servedRelease=release;
  await cachePage.goto(`http://127.0.0.1:${server.address().port}/`);await expect(cachePage.locator('#release-status')).not.toHaveAttribute('aria-busy','true');
  report.cacheObservedLinks=await cachePage.locator('[data-platform]').evaluateAll(elements=>elements.map(element=>({platform:element.dataset.platform,url:element.getAttribute('href')})));
  for(const download of release.downloads)await expect(cachePage.locator(`[data-platform="${download.platform}"]`)).toHaveAttribute('href',download.url,{timeout:3000});
  await expect.poll(()=>releaseRequests,{timeout:3000}).toBe(requestsAfterPriming+1);await expect(cachePage.locator('#release-status')).toContainText(`v${release.version}`);
  report.cases.push({name:'cached previous release refreshes to current download URLs',cachedVersion:previousRelease.version,currentVersion:release.version,metadataNetworkRequests:releaseRequests-requestsAfterPriming});await cacheContext.close();
  const noScriptContext=await browser.newContext({javaScriptEnabled:false});const noScriptPage=await noScriptContext.newPage();await noScriptPage.goto(`http://127.0.0.1:${server.address().port}/`);
  for(const download of release.downloads)await expect(noScriptPage.locator(`[data-platform="${download.platform}"]`)).toHaveAttribute('href',download.url,{timeout:3000});
  await expect(noScriptPage.locator('#release-status')).toContainText(`v${release.version}`);report.cases.push({name:'published downloads available without JavaScript',version:release.version});await noScriptContext.close();
  const staleContext=await browser.newContext();const stalePage=await staleContext.newPage();servedRelease=previousRelease;
  await stalePage.goto(`http://127.0.0.1:${server.address().port}/`);await expect(stalePage.locator('#release-status')).not.toHaveAttribute('aria-busy','true');
  for(const download of release.downloads)await expect(stalePage.locator(`[data-platform="${download.platform}"]`)).toHaveAttribute('href',download.url);
  await expect(stalePage.locator('#release-status')).toContainText(`v${release.version}`);report.cases.push({name:'stale network metadata cannot downgrade published download links',receivedVersion:previousRelease.version,publishedVersion:release.version});await staleContext.close();servedRelease=release;
  const unavailableContext=await browser.newContext();const unavailablePage=await unavailableContext.newPage();await unavailablePage.route('**/release.json?*',route=>route.fulfill({status:503,contentType:'application/json',body:'{}'}));
  await unavailablePage.goto(`http://127.0.0.1:${server.address().port}/`);await expect(unavailablePage.locator('#release-status')).not.toHaveAttribute('aria-busy','true');
  for(const download of release.downloads)await expect(unavailablePage.locator(`[data-platform="${download.platform}"]`)).toHaveAttribute('href',download.url);
  await expect(unavailablePage.locator('#release-status')).toContainText(`v${release.version}`);report.cases.push({name:'published downloads survive metadata refresh failure',metadataStatus:503,version:release.version});await unavailableContext.close();
  assert.deepEqual(report.consoleErrors,[]);assert.deepEqual(report.networkErrors,[]);report.result='passed';console.log(`Portal passed ${report.cases.length} viewport, accessibility and link checks.`);
}catch(error){report.result='failed';report.error=error.stack??String(error);console.error(report.error);process.exitCode=1;}
finally{await browser.close();await new Promise(resolve=>server.close(resolve));report.finishedAt=new Date().toISOString();await writeFile(path.join(output,'report.json'),JSON.stringify(report,null,2)+'\n');}
