import {chromium,expect} from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import {createServer} from 'node:http';
import {readFile,mkdir,writeFile} from 'node:fs/promises';
import {fileURLToPath} from 'node:url';
import path from 'node:path';
import assert from 'node:assert/strict';
const root=fileURLToPath(new URL('../',import.meta.url));const directory=path.join(root,'portal');const output=path.join(root,'test-results','portal');await mkdir(output,{recursive:true});
const report={startedAt:new Date().toISOString(),cases:[],consoleErrors:[],networkErrors:[]};
const types={'.html':'text/html; charset=utf-8','.css':'text/css','.js':'text/javascript','.json':'application/json','.png':'image/png','.svg':'image/svg+xml','.woff2':'font/woff2'};
const server=createServer(async(req,res)=>{try{const url=new URL(req.url,'http://localhost');const file=path.resolve(directory,'.'+decodeURIComponent(url.pathname==='/'?'/index.html':url.pathname));if(!file.startsWith(directory+path.sep)){res.writeHead(403);res.end();return;}const bytes=await readFile(file);res.writeHead(200,{'Content-Type':types[path.extname(file)]??'application/octet-stream'});res.end(bytes);}catch{res.writeHead(404);res.end();}});await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
const browser=await chromium.launch({headless:true});
try{
  for(const [name,width,height,theme]of [['desktop',1440,1000,'light'],['mobile',390,844,'light'],['desktop-dark',1440,1000,'dark'],['mobile-dark',390,844,'dark'],['narrow',320,760,'light'],['tablet',768,1024,'light']]){
    const context=await browser.newContext({viewport:{width,height},colorScheme:theme,reducedMotion:'reduce'});const page=await context.newPage();page.on('pageerror',error=>report.consoleErrors.push(error.message));page.on('response',r=>{if(r.status()>=400)report.networkErrors.push({url:r.url(),status:r.status()});});
    await page.goto(`http://127.0.0.1:${server.address().port}/`);await page.evaluate(()=>document.fonts.ready);await expect(page.locator('#release-status')).not.toHaveText('正在读取已发布的安装包…');
    assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true,`${name} horizontal overflow`);
    const scan=await new AxeBuilder({page}).withTags(['wcag2a','wcag2aa','wcag21aa']).analyze();assert.deepEqual(scan.violations.map(v=>({id:v.id,impact:v.impact,nodes:v.nodes.map(n=>n.target)})),[],`${name} WCAG AA`);
    await page.screenshot({path:path.join(output,`${name}.png`),fullPage:true});
    if(name==='desktop'){await page.screenshot({path:path.join(output,'desktop-hero.png')});await page.keyboard.press('Tab');await expect(page.locator('.skip-link')).toBeFocused();await page.keyboard.press('Enter');assert.equal(new URL(page.url()).hash,'#main');await page.getByRole('button',{name:'切换为深色主题'}).click();await expect(page.locator('html')).toHaveAttribute('data-theme','dark');await page.reload();await expect(page.locator('html')).toHaveAttribute('data-theme','dark');}
    if(name==='mobile'){await page.screenshot({path:path.join(output,'mobile-hero.png')});const elements=await page.locator('a[href],button,summary').evaluateAll(list=>list.map(el=>{const r=el.getBoundingClientRect();return {tag:el.tagName,text:el.textContent?.trim(),width:r.width,height:r.height};}));assert.ok(elements.filter(e=>e.tag==='BUTTON'||e.tag==='SUMMARY'||/Windows|macOS/.test(e.text)).every(e=>e.height>=44),'Mobile action targets smaller than 44px');}
    report.cases.push({name,width,height,theme,accessibilityViolations:0,horizontalOverflow:false});await context.close();
  }
  const context=await browser.newContext({viewport:{width:1440,height:1000},reducedMotion:'reduce'});const page=await context.newPage();await page.goto(`http://127.0.0.1:${server.address().port}/`);await page.evaluate(()=>document.documentElement.style.fontSize='200%');assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true,'200% text scaling overflow');report.cases.push({name:'200-percent-text',horizontalOverflow:false});await context.close();
  const release=JSON.parse(await readFile(path.join(directory,'release.json'),'utf8'));for(const download of release.downloads){const response=await fetch(download.url,{method:'HEAD',signal:AbortSignal.timeout(30000)});assert.ok(response.ok,`Actual download unavailable: ${download.url}`);report.cases.push({name:`download ${download.platform}`,status:response.status,url:download.url});}
  assert.deepEqual(report.consoleErrors,[]);assert.deepEqual(report.networkErrors,[]);report.result='passed';console.log(`Portal passed ${report.cases.length} viewport, accessibility and link checks.`);
}catch(error){report.result='failed';report.error=error.stack??String(error);console.error(report.error);process.exitCode=1;}
finally{await browser.close();await new Promise(resolve=>server.close(resolve));report.finishedAt=new Date().toISOString();await writeFile(path.join(output,'report.json'),JSON.stringify(report,null,2)+'\n');}
