const http=require('node:http'),fs=require('node:fs/promises'),path=require('node:path'),assert=require('node:assert/strict');
const {chromium}=require('playwright');
const build=path.resolve(__dirname,'../.local/dashboard-build/browser');
const mime={'.html':'text/html','.js':'text/javascript','.css':'text/css','.svg':'image/svg+xml','.json':'application/json'};
(async()=>{
 const nginx=await fs.readFile(path.resolve(__dirname,'../src/frontend/velqora-dashboard/nginx.conf'),'utf8');
 const policy=nginx.match(/add_header Content-Security-Policy "([^"]+)"/)?.[1];
 assert.ok(policy,'Nginx Content Security Policy is missing');
 const server=http.createServer(async(req,res)=>{
  res.setHeader('Content-Security-Policy',policy);
  try{
   const pathname=decodeURIComponent(new URL(req.url,'http://localhost').pathname);
   if(pathname.startsWith('/api/')||pathname.startsWith('/hubs/')){res.writeHead(404);res.end('No API is hosted by this static UI check.');return;}
   let file=path.resolve(build,'.'+pathname);
   if(!file.startsWith(build+path.sep)&&file!==build){res.writeHead(403);res.end();return;}
   if(pathname==='/'||!path.extname(pathname))file=path.join(build,'index.html');
   const data=await fs.readFile(file);res.setHeader('Content-Type',mime[path.extname(file)]||'application/octet-stream');res.end(data);
  }catch{res.writeHead(404);res.end();}
 });
 await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
 let browser;
 try{
  browser=await chromium.launch({headless:true});
  const page=await browser.newPage({viewport:{width:1440,height:1000}});
  const errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.addInitScript(()=>{window.__cspViolations=[];document.addEventListener('securitypolicyviolation',e=>window.__cspViolations.push(e.violatedDirective));});
  await page.goto('http://127.0.0.1:'+server.address().port+'/login');
  await page.getByRole('heading',{name:'Welcome back'}).waitFor();
  const layout=await page.evaluate(()=>{
   const story=document.querySelector('.login-story'),form=document.querySelector('.login-form');
   return {display:getComputedStyle(document.querySelector('.login-page')).display,
    story:story.getBoundingClientRect().toJSON(),form:form.getBoundingClientRect().toJSON(),
    printOnly:[...document.querySelectorAll('link[rel="stylesheet"]')].some(x=>x.media==='print'),
    violations:window.__cspViolations};
  });
  assert.equal(layout.display,'grid','Login stylesheet did not apply under the deployed CSP');
  assert.equal(layout.printOnly,false,'Application stylesheet is still restricted to print media');
  assert.ok(layout.form.x>=layout.story.right-1,'Desktop login panels should be side by side');
  assert.deepEqual(layout.violations,[],'Content Security Policy violations');
  await fs.mkdir(path.resolve(__dirname,'../.local/visual-qa'),{recursive:true});
  await page.screenshot({path:path.resolve(__dirname,'../.local/visual-qa/login-desktop.png'),fullPage:true});
  await page.getByLabel('Email address').fill('qa.invalid@velqora.local');
  await page.getByLabel('Password',{exact:true}).fill('TestingOnly!123');
  await page.getByRole('button',{name:'Sign in to workspace'}).click();
  await page.getByRole('alert').waitFor();
  assert.equal(errors.length,0,'Runtime page errors: '+errors.join('; '));
  await page.setViewportSize({width:390,height:844});
  await page.getByRole('heading',{name:'Welcome back'}).waitFor();
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth),true,'Mobile horizontal overflow');
  const mobile=await page.evaluate(()=>({story:document.querySelector('.login-story').getBoundingClientRect().toJSON(),form:document.querySelector('.login-form').getBoundingClientRect().toJSON()}));
  assert.ok(mobile.form.y>=mobile.story.bottom-1,'Mobile login panels should stack vertically');
  await page.screenshot({path:path.resolve(__dirname,'../.local/visual-qa/login-mobile.png'),fullPage:true});
  console.log('PASS: deployed CSP allows stylesheet loading; desktop panels align, mobile panels stack without overflow; form/error feedback works; no CSP or page runtime errors.');
  console.log('This static UI check does not verify authentication or backend workflows.');
 }finally{if(browser)await browser.close();await new Promise(resolve=>server.close(resolve));}
})().catch(e=>{console.error(e);process.exitCode=1;});
