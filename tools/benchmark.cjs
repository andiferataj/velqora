const {performance}=require('node:perf_hooks'),crypto=require('node:crypto'),fs=require('node:fs/promises'),path=require('node:path'),os=require('node:os');
const {request}=require('playwright');
(async()=>{
 const origin=process.env.VELQORA_BASE_URL||'http://localhost:4200';
 const key=process.env.VELQORA_INGESTION_KEY,password=process.env.VELQORA_DEMO_PASSWORD;
 if(!key||!password)throw Error('Load generated credentials into VELQORA_INGESTION_KEY and VELQORA_DEMO_PASSWORD.');
 const client=await request.newContext({baseURL:origin});
 try{
  const token=(await (await client.get('/api/auth/csrf')).json()).token;
  const signed=await client.post('/api/auth/login',{headers:{'X-XSRF-TOKEN':token},data:{email:'admin@velqora.local',password}});
  if(!signed.ok())throw Error('Benchmark sign-in failed: '+signed.status());
  const initial=await (await client.get('/api/platform')).json();
  if(initial.outboxPending||initial.broker?.pending)throw Error('Wait for the existing pipeline backlog to drain before measuring.');
  const source='release-load-'+Date.now(),latencies=[],failures=[];
  const start=performance.now(),jobs=[];
  for(let batch=0;batch<300;batch++){
   const delay=start+batch*100-performance.now();if(delay>0)await new Promise(r=>setTimeout(r,delay));
   jobs.push((async()=>{
    const events=Array.from({length:10},(_,i)=>({id:crypto.randomUUID(),schemaVersion:1,occurredAt:new Date().toISOString(),source,type:'load.telemetry',device:'RELEASE-LOAD',account:'load-test',ipAddress:'192.0.2.80',attributes:{sequence:String(batch*10+i)},synthetic:true}));
    const at=performance.now();
    const r=await client.post('/api/ingest/events',{headers:{'X-Ingestion-Key':key},data:events});
    latencies.push(performance.now()-at);
    if(!r.ok()||(await r.json()).accepted!==10)failures.push({batch,status:r.status()});
   })());
  }
  await Promise.all(jobs);
  const elapsed=performance.now()-start;
  let health,drained=false;
  const drainStart=performance.now();
  while(performance.now()-drainStart<180000){
   health=await (await client.get('/api/platform')).json();
   if(health.outboxPending===0&&health.broker?.pending===0&&health.processedEvents>=initial.processedEvents+3000){drained=true;break;}
   await new Promise(r=>setTimeout(r,2000));
  }
  const stored=await (await client.get('/api/events?q='+source)).json();
  latencies.sort((a,b)=>a-b);
  const percentile=q=>Math.round(latencies[Math.min(latencies.length-1,Math.ceil(q*latencies.length)-1)]*100)/100;
  const report={at:new Date().toISOString(),origin,source,workload:{batches:300,eventsPerBatch:10,scheduledEventsPerSecond:100,durationSeconds:30},actual:{acceptedEvents:3000-failures.length*10,storedEvents:stored.total,requestMilliseconds:{p50:percentile(.5),p95:percentile(.95),max:latencies.at(-1)},ingestionSeconds:elapsed/1000,drainSeconds:(performance.now()-drainStart)/1000,drained,failures},host:{platform:os.platform(),cpuCount:os.cpus().length,memoryGiB:Math.round(os.totalmem()/1073741824)},passed:failures.length===0&&percentile(.95)<1000&&stored.total===3000&&drained};
  await fs.mkdir(path.resolve(__dirname,'../.local/release'),{recursive:true});
  await fs.writeFile(path.resolve(__dirname,'../.local/release/performance.json'),JSON.stringify(report,null,2));
  console.log(JSON.stringify(report,null,2));if(!report.passed)process.exitCode=1;
 }finally{await client.dispose();}
})().catch(e=>{console.error(e.message);process.exitCode=1;});
