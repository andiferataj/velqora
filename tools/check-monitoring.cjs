const fs=require('node:fs/promises'),path=require('node:path');
const sleep=ms=>new Promise(r=>setTimeout(r,ms));
async function json(url){const r=await fetch(url,{signal:AbortSignal.timeout(8000)});if(!r.ok)throw Error(url+' returned '+r.status);return r.json();}
(async()=>{
 const prometheus=process.env.VELQORA_PROMETHEUS_URL||'http://localhost:9090';
 const grafana=process.env.VELQORA_GRAFANA_URL||'http://localhost:3000';
 const jaeger=process.env.VELQORA_JAEGER_URL||'http://localhost:16686';
 let result,lastError;
 const deadline=Date.now()+120000;
 while(Date.now()<deadline){
  try{
   const health=await json(grafana+'/api/health');
   if(health.database!=='ok')throw Error('Grafana database is not ready');
   const names=['velqora_events_ingested_total','velqora_alerts_created_total','velqora_outbox_pending','velqora_queue_pending','velqora_queue_dead_letters','velqora_detection_duration_milliseconds_bucket','velqora_event_processing_delay_seconds_bucket'];
   for(const name of names){
    const query=await json(prometheus+'/api/v1/query?query='+encodeURIComponent(name));
    if(query.status!=='success'||!query.data.result.length)throw Error('Missing exported metric '+name);
   }
   const traces=await json(jaeger+'/api/traces?service=velqora-api&limit=100&lookback=1h');
   const linked=traces.data.find(t=>{
    const services=new Set(Object.values(t.processes).map(p=>p.serviceName));
    return services.has('velqora-api')&&services.has('velqora-worker')&&t.spans.some(s=>s.operationName==='outbox.publish')&&t.spans.some(s=>s.operationName==='queue.consume');
   });
   if(!linked)throw Error('No trace joins API ingestion, outbox publishing, and worker consumption yet');
   result={at:new Date().toISOString(),passed:true,metrics:names,traceId:linked.traceID,services:[...new Set(Object.values(linked.processes).map(p=>p.serviceName))]};
   break;
  }catch(e){lastError=e.message;await sleep(5000);}
 }
 if(!result)throw Error('Monitoring acceptance failed: '+lastError);
 await fs.mkdir(path.resolve(__dirname,'../.local/release'),{recursive:true});
 await fs.writeFile(path.resolve(__dirname,'../.local/release/monitoring.json'),JSON.stringify(result,null,2));
 console.log(JSON.stringify(result,null,2));
})().catch(e=>{console.error(e.message);process.exitCode=1;});
