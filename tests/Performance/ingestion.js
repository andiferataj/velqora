import http from 'k6/http';
import { check } from 'k6';
import exec from 'k6/execution';
export const options={
  scenarios:{ingest:{executor:'constant-arrival-rate',rate:10,timeUnit:'1s',duration:'30s',preAllocatedVUs:5,maxVUs:20}},
  thresholds:{http_req_failed:['rate<0.01'],http_req_duration:['p(95)<1000']}
};
function uuid(){
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g,c=>{const r=Math.floor(Math.random()*16);return(c==='x'?r:(r&3)|8).toString(16);});
}
export default function(){
  const response=http.post((__ENV.VELQORA_API||'http://localhost:8080')+'/api/ingest/events',JSON.stringify(
    Array.from({length:10},(_,i)=>({id:uuid(),schemaVersion:1,occurredAt:new Date().toISOString(),source:'k6',
      type:'load.telemetry',device:'K6-'+exec.vu.idInTest,account:'load-test',ipAddress:'192.0.2.80',
      attributes:{sequence:String(i)},synthetic:true}))
  ),{headers:{'Content-Type':'application/json','X-Ingestion-Key':__ENV.VELQORA_INGESTION_KEY}});
  check(response,{'accepted batch':r=>r.status===200&&r.json().accepted===10});
}
