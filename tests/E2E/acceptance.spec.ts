import { test, expect, APIRequestContext, Browser } from '@playwright/test';
const password=process.env['VELQORA_DEMO_PASSWORD'];
test.beforeAll(()=>{if(!password)throw new Error('Set VELQORA_DEMO_PASSWORD to the generated DEMO_PASSWORD before running acceptance tests.');});
async function login(page: import('@playwright/test').Page,email:string){
  await page.goto('/login');
  await page.getByLabel('Email address').fill(email);
  await page.getByLabel('Password',{exact:true}).fill(password!);
  await page.getByRole('button',{name:'Sign in to workspace'}).click();
  await expect(page.getByRole('heading',{name:'Security at a glance.'})).toBeVisible();
}
async function csrf(request:APIRequestContext){return (await (await request.get('/api/auth/csrf')).json()).token as string;}
test('central workflow: simulation → detection → case → independent approval → containment',async({page,browser})=>{
  await login(page,'analyst@velqora.local');
  await page.getByRole('link',{name:'Simulation Lab',exact:false}).first().click();
  await page.getByRole('button',{name:'Run login abuse'}).click();
  const row=page.locator('tbody tr').first();
  await expect(row).toContainText('Completed');
  await expect(row.locator('.count-chip')).toHaveText('2');
  const device=await row.locator('td').nth(1).innerText();
  await page.getByRole('link',{name:'Alerts',exact:true}).click();
  await page.getByRole('textbox',{name:'Search alerts'}).fill(device);
  await page.getByRole('button',{name:'Apply filters'}).click();
  const detectionRow=page.locator('tbody tr').filter({hasText:device}).filter({has:page.getByRole('link',{name:'Success after login abuse',exact:true})});
  await expect(detectionRow).toHaveCount(1);
  await detectionRow.getByRole('link',{name:'Success after login abuse',exact:true}).click();
  await expect(page.getByText('auth.failure',{exact:true})).toHaveCount(12);
  await expect(page.getByText('auth.success',{exact:true})).toHaveCount(1);
  await page.getByLabel('Incident title').fill('Acceptance / '+device);
  await page.getByRole('button',{name:'Create incident'}).click();
  await expect(page.getByRole('heading',{name:'Acceptance / '+device})).toBeVisible();
  await page.getByLabel('Add investigation note').fill('Verified 12 failures followed by success. Requesting simulated containment.');
  await page.getByRole('button',{name:'Add note',exact:true}).click();
  await expect(page.getByText('Verified 12 failures followed by success. Requesting simulated containment.',{exact:true})).toBeVisible();
  await page.getByRole('combobox',{name:'Status',exact:true}).selectOption('Investigating');
  await page.getByRole('button',{name:'Save case',exact:true}).click();
  await expect(page.getByText('Case saved.',{exact:true})).toBeVisible();
  await page.getByRole('combobox',{name:'Playbook',exact:true}).selectOption({label:'Contain workstation'});
  await page.getByRole('combobox',{name:'Linked asset',exact:true}).selectOption({label:device});
  await page.getByRole('button',{name:'Request approval'}).click();
  await expect(page.getByText('Response requested.',{exact:false})).toBeVisible();
  const incidentId=page.url().split('/').pop()!;
  const token=await csrf(page.request);
  const runs=(await (await page.request.get('/api/playbook-runs?size=100')).json()).items;
  const run=runs.find((r:{incidentId:string})=>r.incidentId===incidentId);
  expect(run.status).toBe('Pending');
  const self=await page.request.post('/api/playbook-runs/'+run.id+'/decision',{headers:{'X-XSRF-TOKEN':token},data:{version:run.version,approve:true}});
  expect(self.status()).toBe(403);
  const adminContext=await browser.newContext({baseURL:process.env['VELQORA_BASE_URL']||'http://localhost:4200'});
  const adminPage=await adminContext.newPage();
  try{
    await login(adminPage,'admin@velqora.local');
    const adminToken=await csrf(adminContext.request);
    const decision=await adminContext.request.post('/api/playbook-runs/'+run.id+'/decision',{headers:{'X-XSRF-TOKEN':adminToken},data:{version:run.version,approve:true}});
    expect(decision.ok()).toBeTruthy();
    await expect.poll(async()=>{
      const response=await adminContext.request.get('/api/playbook-runs?size=100');
      const data=await response.json();return data.items.find((x:{id:string})=>x.id===run.id)?.status;
    }).toBe('Executed');
    const asset=await (await adminContext.request.get('/api/assets/'+run.assetId)).json();
    expect(asset.asset.isolated).toBe(true);
    const report=await adminContext.request.get('/api/incidents/'+incidentId+'/report');
    expect(report.ok()).toBeTruthy();
    const body=await report.json();
    expect(body.evidence).toHaveLength(13);
    expect(body.timeline.some((x:{kind:string})=>x.kind==='response')).toBeTruthy();
  }finally{await adminContext.close();}
});
test('read-only authorization and CSRF are enforced by the API',async({page})=>{
  await login(page,'viewer@velqora.local');
  const token=await csrf(page.request);
  const response=await page.request.post('/api/scenario-runs',{headers:{'X-XSRF-TOKEN':token},data:{scenario:'login-abuse'}});
  expect(response.status()).toBe(403);
  const noCsrf=await page.request.post('/api/auth/logout',{data:{}});
  expect(noCsrf.status()).toBe(400);
  const users=await page.request.post('/api/users',{headers:{'X-XSRF-TOKEN':token},data:{email:'blocked@local.test',password:'Abc123!abc123!',role:'Admin'}});
  expect(users.status()).toBe(403);
});
test('ingestion idempotency persists exactly 1000 distinct events',async({request,page})=>{
  const key=process.env['VELQORA_INGESTION_KEY'];if(!key)throw new Error('Set VELQORA_INGESTION_KEY.');
  await login(page,'admin@velqora.local');
  const source='acceptance-'+Date.now(),time=new Date().toISOString();
  const data=Array.from({length:1000},(_,i)=>({
    id:crypto.randomUUID(),schemaVersion:1,occurredAt:time,source,type:'test.telemetry',
    device:'ACCEPTANCE-BULK',account:'load-test',ipAddress:'192.0.2.55',synthetic:true,attributes:{index:String(i)}
  }));
  const options={headers:{'X-Ingestion-Key':key},data};
  let response=await request.post('/api/ingest/events',options);
  expect(response.ok()).toBeTruthy();expect((await response.json()).accepted).toBe(1000);
  response=await request.post('/api/ingest/events',options);
  expect(response.ok()).toBeTruthy();expect((await response.json()).duplicates).toBe(1000);
  const stored=await (await page.request.get('/api/events?q='+source)).json();
  expect(stored.total).toBe(1000);
  const conflict=await request.post('/api/ingest/events',{headers:{'X-Ingestion-Key':key},data:[{...data[0],account:'conflict'}]});
  expect(conflict.status()).toBe(409);
});
test('incident version conflicts preserve the first edit',async({page})=>{
  await login(page,'analyst@velqora.local');
  const token=await csrf(page.request);
  const alerts=await (await page.request.get('/api/alerts?size=1')).json();
  expect(alerts.items.length).toBeGreaterThan(0);
  const created=await page.request.post('/api/incidents',{headers:{'X-XSRF-TOKEN':token},data:{title:'Concurrency acceptance',summary:'Initial',severity:'High',alertIds:[alerts.items[0].id]}});
  expect(created.ok()).toBeTruthy();const incident=await created.json();
  const body={version:incident.version,status:'Investigating',severity:'High',ownerId:incident.ownerId,summary:'First edit'};
  expect((await page.request.put('/api/incidents/'+incident.id,{headers:{'X-XSRF-TOKEN':token},data:body})).ok()).toBeTruthy();
  expect((await page.request.put('/api/incidents/'+incident.id,{headers:{'X-XSRF-TOKEN':token},data:{...body,summary:'Stale edit'}})).status()).toBe(409);
  const current=await (await page.request.get('/api/incidents/'+incident.id)).json();
  expect(current.incident.summary).toBe('First edit');
});
