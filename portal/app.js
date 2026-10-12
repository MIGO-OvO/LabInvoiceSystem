const themeButton=document.getElementById('theme-toggle');
function describeTheme(){themeButton.setAttribute('aria-label',document.documentElement.dataset.theme==='dark'?'切换为浅色主题':'切换为深色主题');}
describeTheme();themeButton.addEventListener('click',()=>{document.documentElement.dataset.theme=document.documentElement.dataset.theme==='dark'?'light':'dark';try{localStorage.setItem('labinvoice-portal-theme',document.documentElement.dataset.theme);}catch{}describeTheme();});
const status=document.getElementById('release-status');
async function release(){
  try{const response=await fetch('release.json',{signal:AbortSignal.timeout(8000)});if(!response.ok)throw new Error('release metadata unavailable');const data=await response.json();
    if(!data.version||!Array.isArray(data.downloads)||!data.downloads.length){status.textContent='安装包尚未发布。可在 GitHub 查看发布进度。';return;}
    let count=0;for(const element of document.querySelectorAll('[data-platform]')){const artifact=data.downloads.find(a=>a.platform===element.dataset.platform);if(!artifact)continue;const url=new URL(artifact.url);if(url.hostname!=='github.com'||!url.pathname.startsWith('/MIGO-OvO/LabInvoiceSystem/releases/download/'))continue;element.href=url.href;element.removeAttribute('aria-disabled');element.title=artifact.notarized||element.dataset.platform==='windows-x86_64'?`下载 ${artifact.name}`:`下载未公证预览构建：${artifact.name}`;if(element.dataset.platform.startsWith('darwin')&&!artifact.notarized)element.querySelector('small').textContent+=(` · 未公证预览`);count++;}
    status.textContent=count?`v${data.version} · 已发布 ${count} 个架构安装包`:'当前没有可用的安装包，请查看 GitHub Releases。';
    const releaseURL=new URL(data.releaseUrl);if(releaseURL.hostname==='github.com'&&releaseURL.pathname.startsWith('/MIGO-OvO/LabInvoiceSystem/releases/tag/'))document.getElementById('release-link').href=releaseURL.href;
    if(data.downloads.some(a=>a.platform.startsWith('darwin'))){const note=data.macosProductionReady?'macOS 发行签名与公证已通过。各平台的实际状态见 Release 校验文件。':'macOS 当前下载为未公证预览构建，正式分发与在线更新等待 Apple 发行凭据。';document.getElementById('macos-availability').textContent=note;document.getElementById('changelog-macos').textContent=note;}
  }catch{status.textContent='暂时无法读取下载信息。请通过 GitHub Releases 获取安装包。';}
}
void release();
function openLinkedDetails(){const id=location.hash.slice(1);const element=document.getElementById(id);if(element instanceof HTMLDetailsElement){element.open=true;element.querySelector('summary')?.focus({preventScroll:true});element.scrollIntoView({block:'start'});}}
window.addEventListener('hashchange',openLinkedDetails);openLinkedDetails();
window.addEventListener('load',()=>requestAnimationFrame(openLinkedDetails),{once:true});
