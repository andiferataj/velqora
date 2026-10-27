import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Alert, Asset, Incident, IncidentStatus, Page, Playbook, Severity, Timeline, User } from './models';
import { PageUi } from './page';
@Component({selector:'app-incidents',imports:[CommonModule,FormsModule,RouterLink],template:`
<div class="page-heading"><div><div class="eyebrow">INVESTIGATION & RESPONSE</div><h1>Incidents<span class="heading-dot">.</span></h1><p>A shared workspace for every investigation.</p></div><div class="segmented"><button [class.selected]="!board()" (click)="board.set(false)">List</button><button [class.selected]="board()" (click)="board.set(true)">Board</button></div></div>
@if(error()){<div class="notice error" role="alert">{{error()}}</div>}
<section class="panel"><form class="filters" (ngSubmit)="page=1;load()"><input class="grow" aria-label="Search incidents" name="q" [(ngModel)]="q" placeholder="Search incident title…"><select aria-label="Incident status filter" name="status" [(ngModel)]="status"><option value="">All statuses</option>@for(s of statuses;track s){<option>{{s}}</option>}</select><button class="button">Apply filters</button></form>
@if(!board()){<div class="table-wrap"><table><thead><tr><th>Incident</th><th>Severity</th><th>Status</th><th>Created · UTC</th><th></th></tr></thead><tbody>@for(i of data()?.items??[];track i.id){<tr><td><a class="table-title" [routerLink]="['/incidents',i.id]">{{i.title}}</a><small class="mono">CASE {{i.id.slice(0,8)}}</small></td><td><span [class]="'badge '+i.severity.toLowerCase()">{{i.severity}}</span></td><td>{{i.status}}</td><td class="muted">{{i.createdAt|date:'MMM d, HH:mm':'UTC'}}</td><td><a [routerLink]="['/incidents',i.id]">Open case ↗</a></td></tr>}@empty{<tr><td colspan="5" class="empty-cell">No incidents found. Escalate an alert to open your first case.</td></tr>}</tbody></table></div>}
@else{<div class="kanban">@for(s of statuses;track s){<section class="kanban-column"><h3>{{s}} <span>{{inStatus(s).length}}</span></h3>@for(i of inStatus(s);track i.id){<a class="kanban-card" [routerLink]="['/incidents',i.id]"><span [class]="'badge '+i.severity.toLowerCase()">{{i.severity}}</span><h4>{{i.title}}</h4><small>{{i.createdAt|date:'MMM d':'UTC'}} · {{i.id.slice(0,8)}}</small></a>}@empty{<div class="kanban-empty">No cases</div>}</section>}</div>}
<div class="pagination"><span>{{data()?.total??0}} cases · board shows current page</span><div><button class="button small" [disabled]="page===1" (click)="page=page-1;load()">Previous</button><span>{{page}}</span><button class="button small" [disabled]="page*25 >= (data()?.total??0)" (click)="page=page+1;load()">Next</button></div></div></section>
`})
export class IncidentsComponent extends PageUi {
  data=signal<Page<Incident>|null>(null);board=signal(false);q='';status='';page=1;statuses=['Open','Investigating','Contained','Resolved','Closed'];
  constructor(){super();this.poll(()=>this.load(),5000);}
  inStatus(s:string){return this.data()?.items.filter(x=>x.status===s)??[];}
  load(){return this.read(async()=>{const p=new URLSearchParams({q:this.q,page:String(this.page)});if(this.status)p.set('status',this.status);this.data.set(await this.api.get<Page<Incident>>('incidents?'+p));});}
}
@Component({selector:'app-incident-detail',imports:[CommonModule,FormsModule,RouterLink],template:`
<a class="back-link" routerLink="/incidents">← All incidents</a>
@if(error()){<div class="notice error" role="alert">{{error()}}</div>}@if(success()){<div class="notice success" role="status">{{success()}}</div>}
@if(data();as d){<div class="page-heading"><div><div class="eyebrow">CASE · {{d.incident.id.slice(0,8)}}</div><h1>{{d.incident.title}}</h1><p>Opened {{d.incident.createdAt|date:'medium':'UTC'}} UTC · Version {{d.incident.version}}</p></div><a class="button" [href]="'/api/incidents/'+d.incident.id+'/report'">↓ Export case report</a></div>
<div class="detail-grid"><div><section class="panel"><div class="panel-heading"><h2>Investigation timeline</h2><button class="button small" (click)="load()">↻ Refresh</button></div><div class="event-timeline">@for(t of d.timeline;track t.id){<div class="event-line"><span class="timeline-dot"></span><div><span class="tag">{{t.kind}}</span><span class="timeline-date">{{t.createdAt|date:'MMM d, HH:mm:ss':'UTC'}} UTC</span><p class="timeline-text">{{t.text}}</p><small class="muted">{{actorName(t.actorId)}}</small></div></div>}</div>
@if(auth.canOperate){<form class="comment-form" (ngSubmit)="comment()"><label>Add investigation note<textarea name="comment" [(ngModel)]="commentText" required maxlength="3000" placeholder="Document findings, context, or the next step…"></textarea></label><button class="button primary" [disabled]="busy()||!commentText.trim()">Add note</button></form>}</section>
<section class="panel spaced"><div class="panel-heading"><h2>Linked alerts <span class="count-chip">{{d.alerts.length}}</span></h2></div><div class="detail-body">@for(a of d.alerts;track a.id){<a class="linked-alert" [routerLink]="['/alerts',a.id]"><span [class]="'badge '+a.severity.toLowerCase()">{{a.severity}}</span><span>{{a.title}}<small>{{a.device}} / {{a.account}}</small></span><span>↗</span></a>}
@if(auth.canOperate){<form class="inline-form spaced" (ngSubmit)="linkAlert()"><select aria-label="Additional alert" name="alert" [(ngModel)]="additionalAlert" required><option value="">Link another alert…</option>@for(a of availableAlerts();track a.id){<option [value]="a.id">{{a.title}} · {{a.device}}</option>}</select><button class="button" [disabled]="busy()||!additionalAlert">Link evidence</button></form>}</div></section></div>
<div><section class="panel"><div class="panel-heading"><h2>Case management</h2></div><form class="detail-body" (ngSubmit)="save()"><label>Status<select name="status" [(ngModel)]="status" [disabled]="!auth.canOperate">@for(s of statuses;track s){<option [value]="s">{{s}}</option>}</select></label><label>Severity<select name="severity" [(ngModel)]="severity" [disabled]="!auth.canOperate">@for(s of ['Low','Medium','High','Critical'];track s){<option [value]="s">{{s}}</option>}</select></label><label>Owner<select name="owner" [(ngModel)]="owner" [disabled]="!auth.canOperate"><option value="">Unassigned</option>@for(u of operators();track u.id){<option [value]="u.id">{{u.email}}</option>}</select></label><label>Case summary<textarea name="summary" [(ngModel)]="summary" maxlength="3000" [disabled]="!auth.canOperate"></textarea></label>@if(auth.canOperate){<button class="button primary full" [disabled]="busy()">Save case</button>}</form></section>
@if(auth.canOperate){<section class="panel spaced"><div class="panel-heading"><h2>Coordinate response</h2><span class="tag">SIMULATED</span></div><form class="detail-body" (ngSubmit)="respond()"><p class="muted">A second operator must approve execution. Actions only update synthetic asset state.</p><label>Playbook<select name="playbook" [(ngModel)]="playbookId" required><option value="">Select playbook</option>@for(p of playbooks();track p.id){<option [value]="p.id">{{p.name}}</option>}</select></label><label>Linked asset<select name="asset" [(ngModel)]="assetId" required><option value="">Select asset</option>@for(a of linkedAssets();track a.id){<option [value]="a.id">{{a.name}}</option>}</select></label><button class="button full" [disabled]="busy()||!playbookId||!assetId">Request approval →</button><a class="text-link spaced block" routerLink="/playbooks">View response requests ↗</a></form></section>}</div></div>}
`})
export class IncidentDetailComponent extends PageUi {
  route=inject(ActivatedRoute);data=signal<{incident:Incident;alerts:Alert[];timeline:Timeline[]}|null>(null);
  operators=signal<User[]>([]);allUsers=signal<User[]>([]);availableAlerts=signal<Alert[]>([]);playbooks=signal<Playbook[]>([]);linkedAssets=signal<Asset[]>([]);
  status:IncidentStatus='Open';severity:Severity='High';owner='';summary='';commentText='';additionalAlert='';playbookId='';assetId='';
  statuses=['Open','Investigating','Contained','Resolved','Closed'];
  constructor(){super();this.route.paramMap.pipe(takeUntilDestroyed()).subscribe(()=>void this.load());}
  actorName(id:string){return this.allUsers().find(x=>x.id===id)?.email??id;}
  load(){return this.read(async()=>{const [d,u,a,p,assets]=await Promise.all([
    this.api.get<{incident:Incident;alerts:Alert[];timeline:Timeline[]}>('incidents/'+this.route.snapshot.params['id']),
    this.api.get<User[]>('users'),this.api.get<Page<Alert>>('alerts?size=100'),this.api.get<Playbook[]>('playbooks'),this.api.get<Page<Asset>>('assets?size=100')]);
    this.data.set(d);this.allUsers.set(u);this.operators.set(u.filter(x=>x.roles.some(r=>r==='Admin'||r==='Analyst')));
    this.availableAlerts.set(a.items.filter(x=>!d.alerts.some(y=>y.id===x.id)));this.playbooks.set(p.filter(x=>x.enabled));
    this.linkedAssets.set(assets.items.filter(x=>x.synthetic&&d.alerts.some(y=>y.device===x.name)));
    this.status=d.incident.status;this.severity=d.incident.severity;this.owner=d.incident.ownerId??'';this.summary=d.incident.summary;
  });}
  save(){return this.act(async()=>{const i=this.data()!.incident;await this.api.put('incidents/'+i.id,{version:i.version,status:this.status,severity:this.severity,ownerId:this.owner||null,summary:this.summary});await this.load();this.success.set('Case saved.');});}
  comment(){return this.act(async()=>{await this.api.post('incidents/'+this.data()!.incident.id+'/comments',{text:this.commentText});this.commentText='';await this.load();});}
  linkAlert(){return this.act(async()=>{await this.api.post('incidents/'+this.data()!.incident.id+'/alerts',{alertId:this.additionalAlert});this.additionalAlert='';await this.load();});}
  respond(){return this.act(async()=>{await this.api.post('playbook-runs',{playbookId:this.playbookId,assetId:this.assetId,incidentId:this.data()!.incident.id});await this.load();this.success.set('Response requested. A different operator can approve it on the playbooks page.');});}
}
