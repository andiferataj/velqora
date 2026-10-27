import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ScenarioRun } from './models';
import { PageUi } from './page';
@Component({selector:'app-lab',imports:[CommonModule,RouterLink],template:`
<div class="page-heading"><div><div class="eyebrow">AN INTERACTIVE PROVING GROUND</div><h1>Simulation Lab<span class="heading-dot">.</span></h1><p>Generate the signal. Trace the detection. Practice the response.</p></div><span class="tag lab-tag">◇ ISOLATED SYNTHETIC DATA</span></div>
@if(error()){<div class="notice error" role="alert">{{error()}}</div>}
<div class="lab-banner"><div class="lab-orbit">◇</div><div><span class="eyebrow">YOUR FIRST END-TO-END INVESTIGATION</span><h2>13 events. One story to uncover.</h2><p>Start Login Abuse to generate 12 failed logins followed by a success. Investigate the alerts, open a case, and request simulated containment.</p></div><button class="button primary" [disabled]="busy()||!auth.canOperate" (click)="start('login-abuse')">Run login abuse →</button></div>
<div class="card-grid scenario-grid">@for(s of scenarios;track s.name){<section class="panel scenario-card"><div class="card-top"><span class="scenario-number">{{s.number}}</span><span [class]="'badge '+s.level">{{s.category}}</span></div><h2>{{s.title}}</h2><p>{{s.description}}</p><div class="scenario-meta"><span>{{s.events}} events</span><span>{{s.expectation}}</span></div><button class="button full" [disabled]="busy()||!auth.canOperate" (click)="start(s.name)">▷ Launch scenario</button></section>}</div>
<section class="panel spaced"><div class="panel-heading"><div><h2>Scenario runs</h2><p>Replay creates a new isolated run; previous evidence stays intact.</p></div><a class="text-link" routerLink="/alerts">Investigate detections ↗</a></div><div class="table-wrap"><table><thead><tr><th>Scenario / run</th><th>Asset</th><th>Progress</th><th>Alerts</th><th>Status</th><th>Controls</th></tr></thead><tbody>@for(r of runs();track r.id){<tr><td class="table-title">{{r.scenario}}<small class="mono">{{r.id.slice(0,8)}} · {{r.createdAt|date:'HH:mm:ss':'UTC'}}</small></td><td class="mono">{{r.device}}</td><td><span>{{r.cursor}} / {{r.totalEvents}}</span><div class="bar-track progress"><div class="bar low" [style.width.%]="r.cursor/r.totalEvents*100"></div></div></td><td><span class="count-chip">{{alerts(r.id)}}</span></td><td>{{r.status}}@if(r.error){<small class="error-text">{{r.error}}</small>}</td><td><div class="button-row">@if(r.status==='Running'){<button class="button small" [disabled]="busy()||!auth.canOperate" (click)="control(r,'pause')">Pause</button>}@if(r.status==='Paused'){<button class="button small" [disabled]="busy()||!auth.canOperate" (click)="control(r,'resume')">Resume</button>}@if(r.status==='Paused'||r.status==='Running'){<button class="button small" [disabled]="busy()||!auth.canOperate" (click)="reset(r)">Reset ↻</button><button class="button small danger-button" [disabled]="busy()||!auth.canOperate" (click)="control(r,'cancel')">Cancel</button>}@else{<button class="button small" [disabled]="busy()||!auth.canOperate" (click)="start(r.scenario)">Replay ↻</button>}</div></td></tr>}@empty{<tr><td colspan="6" class="empty-cell">Your lab is ready. Choose a scenario to start.</td></tr>}</tbody></table></div></section>
`})
export class LabComponent extends PageUi {
  runs=signal<ScenarioRun[]>([]);outcomes=signal<{runId:string;alerts:number}[]>([]);
  scenarios=[
    {name:'login-abuse',number:'01',title:'Login abuse',description:'12 failed sign-ins followed by a successful authentication from the same identity.',events:13,expectation:'2 expected detections',category:'AUTHENTICATION',level:'high'},
    {name:'privilege-misuse',number:'02',title:'Privilege misuse',description:'An account signs in and changes privileges. Follow the identity trail.',events:3,expectation:'1 expected detection',category:'PRIVILEGE',level:'critical'},
    {name:'event-spike',number:'03',title:'Network event spike',description:'A sudden burst of network activity crosses the configured volume threshold.',events:120,expectation:'1 expected detection',category:'NETWORK',level:'medium'},
    {name:'telemetry-loss',number:'04',title:'Telemetry loss',description:'A device sends a heartbeat, then goes silent. Detection appears after 30 seconds.',events:1,expectation:'1 delayed detection',category:'AVAILABILITY',level:'medium'},
    {name:'benign-baseline',number:'05',title:'Benign baseline',description:'Normal authentication activity with isolated failures. Test for false positives.',events:30,expectation:'0 expected detections',category:'BASELINE',level:'low'}
  ];
  constructor(){super();this.poll(()=>this.load(),2000);}
  alerts(id:string){return this.outcomes().find(x=>x.runId===id)?.alerts??0;}
  load(){return this.read(async()=>{const d=await this.api.get<{runs:ScenarioRun[];outcomes:{runId:string;alerts:number}[]}>('scenario-runs');this.runs.set(d.runs);this.outcomes.set(d.outcomes);});}
  start(scenario:string){return this.act(async()=>{await this.api.post('scenario-runs',{scenario});await this.load();});}
  reset(r:ScenarioRun){return this.act(async()=>{await this.api.post("scenario-runs/"+r.id+"/reset",{version:r.version,action:"reset"});await this.load();});}
  control(r:ScenarioRun,action:string){return this.act(async()=>{await this.api.put('scenario-runs/'+r.id,{version:r.version,action});await this.load();});}
}
