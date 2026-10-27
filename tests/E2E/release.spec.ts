import { test, expect, APIRequestContext } from '@playwright/test';
async function signIn(request:APIRequestContext) {
 const password=process.env['VELQORA_DEMO_PASSWORD'];
 if(!password)throw new Error('VELQORA_DEMO_PASSWORD is required.');
 const response=await request.get('/api/auth/csrf');
 const token=(await response.json()).token;
 expect((await request.post('/api/auth/login',{headers:{'X-XSRF-TOKEN':token},data:{email:'admin@velqora.local',password}})).status()).toBe(200);
 return (await (await request.get('/api/auth/csrf')).json()).token as string;
}
test('analytics, every workspace page, report format, and mobile navigation',async({page})=>{
 await signIn(page.request);
 const errors:string[]=[];
 page.on('pageerror',e=>errors.push(e.message));
 page.on('response',r=>{if(r.url().includes('/api/')&&r.status()>=500)errors.push(r.status()+' '+r.url());});
 const analytics=await page.request.get('/api/analytics?days=7');
 expect(analytics.status()).toBe(200);
 const data=await analytics.json();
 expect(data.trend.length).toBeGreaterThan(0);
 expect(data.trend.every((p:{day:string;count:number})=>!isNaN(Date.parse(p.day))&&p.count>0)).toBe(true);
 for(const route of ['/','/events','/alerts','/incidents','/assets','/rules','/playbooks','/lab','/analytics','/platform','/settings']){
  await page.goto(route);
  await expect(page.locator('main h1')).toBeVisible();
  await expect(page.locator('main .notice.error')).toHaveCount(0);
 }
 const cases=await (await page.request.get('/api/incidents?size=1')).json();
 expect(cases.items.length).toBeGreaterThan(0);
 const report=await page.request.get('/api/incidents/'+cases.items[0].id+'/report');
 expect(report.status()).toBe(200);
 const body=await report.json();
 expect(body.incident.id).toBe(cases.items[0].id);
 expect(body.timeline[0]).toHaveProperty('kind');
 expect(body.timeline[0]).toHaveProperty('createdAt');
 await page.setViewportSize({width:390,height:844});
 await page.goto('/');
 await page.getByRole('button',{name:'Toggle navigation'}).click();
 await page.getByRole('navigation').getByRole('link',{name:'Alerts',exact:true}).click();
 await expect(page.locator('main h1')).toContainText('Alert');
 expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
 expect(errors).toEqual([]);
});
test('all scenario outcomes, pause/resume/reset isolation, and approval-gated automatic proposals',async({request})=>{
 test.setTimeout(180000);
 const token=await signIn(request),headers={'X-XSRF-TOKEN':token};
 const created:Record<string,{id:string;device:string;version:number}>={};
 for(const scenario of ['login-abuse','privilege-misuse','event-spike','telemetry-loss','benign-baseline']){
  const r=await request.post('/api/scenario-runs',{headers,data:{scenario}});
  expect(r.status()).toBe(200);created[scenario]=await r.json();
 }
 for(const [scenario,expected] of Object.entries({'login-abuse':2,'privilege-misuse':1,'event-spike':1,'telemetry-loss':1,'benign-baseline':0})){
  await expect.poll(async()=>{
   const d=await (await request.get('/api/scenario-runs')).json();
   const run=d.runs.find((r:{id:string})=>r.id===created[scenario].id);
   const count=d.outcomes.find((o:{runId:string})=>o.runId===run.id)?.alerts??0;
   return run.status==='Completed'&&count===expected;
  },{timeout:110000,intervals:[1000]}).toBe(true);
 }
 const start=await request.post('/api/scenario-runs',{headers,data:{scenario:'event-spike'}});
 const first=await start.json();
 const paused=await request.put('/api/scenario-runs/'+first.id,{headers,data:{version:first.version,action:'pause'}});
 expect(paused.status()).toBe(200);const p=await paused.json();
 const state=await (await request.get('/api/scenario-runs')).json();
 expect(state.runs.find((r:{id:string})=>r.id===p.id).cursor).toBe(p.cursor);
 const resumed=await request.put('/api/scenario-runs/'+p.id,{headers,data:{version:p.version,action:'resume'}});
 expect(resumed.status()).toBe(200);
 const reset=await request.post('/api/scenario-runs/'+p.id+'/reset',{headers,data:{version:(await resumed.json()).version,action:'reset'}});
 expect(reset.status()).toBe(200);const replacement=await reset.json();
 expect(replacement.device).not.toBe(first.device);
 const states=await (await request.get('/api/scenario-runs')).json();
 expect(states.runs.find((r:{id:string})=>r.id===first.id).status).toBe('Cancelled');
 expect((await request.put('/api/scenario-runs/'+replacement.id,{headers,data:{version:replacement.version,action:'cancel'}})).status()).toBe(200);
 const draft={name:'Release proposal '+Date.now(),description:'Release acceptance automatic proposal',action:'notify',enabled:true,trigger:'IncidentCreated',minimumSeverity:'High'};
 const book=await (await request.post('/api/playbooks',{headers,data:draft})).json();
 try{
  const alerts=await (await request.get('/api/alerts?q='+created['login-abuse'].device)).json();
  const critical=alerts.items.find((a:{severity:string})=>a.severity==='Critical');
  expect(critical).toBeTruthy();
  const result=await request.post('/api/incidents',{headers,data:{title:'Release automatic proposal',summary:'Approval boundary verification',severity:'Critical',alertIds:[critical.id]}});
  expect(result.status()).toBe(201);const incident=await result.json();
  const runs=await (await request.get('/api/playbook-runs?size=100')).json();
  const proposal=runs.items.find((r:{incidentId:string;playbookId:string})=>r.incidentId===incident.id&&r.playbookId===book.id);
  expect(proposal.status).toBe('Pending');
  expect(proposal.actionSnapshot).toBe('notify');
  const detail=await (await request.get('/api/incidents/'+incident.id)).json();
  expect(detail.timeline.some((t:{kind:string})=>t.kind==='response')).toBe(false);
  const decision=await request.post('/api/playbook-runs/'+proposal.id+'/decision',{headers,data:{version:proposal.version,approve:false}});
  expect(decision.status()).toBe(200);
 }finally{
  const current=(await (await request.get('/api/playbooks')).json()).find((b:{id:string})=>b.id===book.id);
  expect((await request.put('/api/playbooks/'+book.id,{headers,data:{...draft,enabled:false,version:current.version}})).status()).toBe(200);
 }
});
