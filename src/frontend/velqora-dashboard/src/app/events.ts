import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { A11yModule } from '@angular/cdk/a11y';
import { Page, SecurityEvent } from './models';
import { PageUi } from './page';
@Component({selector:'app-events',imports:[CommonModule,FormsModule,A11yModule],template:`
<div class="page-heading"><div><div class="eyebrow">TELEMETRY</div><h1>Event explorer<span class="heading-dot">.</span></h1><p>Search the signals. Find the story behind the event.</p></div><a class="button" [href]="'/api/events/export?'+query()">↓ Export CSV</a></div>
@if(error()){<div class="notice error" role="alert">{{error()}}</div>}
<section class="panel"><form class="filters" (ngSubmit)="page=1;load()"><label class="search-field"><span>⌕</span><input aria-label="Search events" name="q" [(ngModel)]="q" placeholder="Search device, identity, or source…"></label><select aria-label="Event type" name="type" [(ngModel)]="type"><option value="">All event types</option>@for(t of types;track t){<option [value]="t">{{t}}</option>}</select><label class="inline-label">From<input aria-label="From date" name="from" type="date" [(ngModel)]="from"></label><label class="inline-label">To<input aria-label="To date" name="to" type="date" [(ngModel)]="to"></label><button class="button" type="submit">Apply filters</button></form>
<div class="table-wrap"><table><thead><tr><th>Occurred · UTC</th><th>Event type</th><th>Device</th><th>Identity</th><th>Source</th><th>Data</th></tr></thead><tbody>
@for(e of data()?.items ?? [];track e.id){<tr><td class="mono">{{e.occurredAt | date:'MMM d, HH:mm:ss':'UTC'}}</td><td><button class="link-button" (click)="selected.set(e)">{{e.type}}</button></td><td>{{e.device}}</td><td>{{e.account || '—'}}</td><td class="muted">{{e.source}}</td><td><span class="tag">{{e.synthetic?'SYNTHETIC':'INTEGRATION'}}</span></td></tr>}
@empty{<tr><td colspan="6" class="empty-cell">{{loading()?'Loading events…':'No events match these filters.'}}</td></tr>}
</tbody></table></div><div class="pagination"><span>{{data()?.total ?? 0 | number}} events</span><div><button class="button small" [disabled]="page===1" (click)="page=page-1;load()">← Previous</button><span>Page {{page}}</span><button class="button small" [disabled]="page*25 >= (data()?.total ?? 0)" (click)="page=page+1;load()">Next →</button></div></div></section>
@if(selected();as e){<div class="modal-backdrop" (click)="selected.set(null)"><section class="modal" role="dialog" aria-modal="true" aria-labelledby="event-title" cdkTrapFocus [cdkTrapFocusAutoCapture]="true" (click)="$event.stopPropagation()" (keydown.escape)="selected.set(null)"><div class="panel-heading"><h2 id="event-title">Event details</h2><button class="icon-button" aria-label="Close" (click)="selected.set(null)">×</button></div><div class="detail-body"><span class="tag">SCHEMA v1 · {{e.synthetic?'SYNTHETIC':'INTEGRATION'}}</span><h3>{{e.type}}</h3><dl><dt>Event ID</dt><dd class="mono">{{e.id}}</dd><dt>Occurred</dt><dd>{{e.occurredAt | date:'medium':'UTC'}} UTC</dd><dt>Device</dt><dd>{{e.device}}</dd><dt>Account</dt><dd>{{e.account}}</dd><dt>IP address</dt><dd class="mono">{{e.ipAddress}}</dd><dt>Source</dt><dd>{{e.source}}</dd></dl><h4>Normalized attributes</h4><pre>{{attributes(e) | json}}</pre></div></section></div>}
`})
export class EventsComponent extends PageUi {
  data=signal<Page<SecurityEvent>|null>(null); selected=signal<SecurityEvent|null>(null); page=1;q='';type='';from='';to='';
  types=['auth.failure','auth.success','privilege.change','network.connection','heartbeat'];
  constructor(){super();this.poll(()=>this.load(),5000);}
  query(){const p=new URLSearchParams({q:this.q,type:this.type,page:String(this.page),size:'25'});if(this.from)p.set('from',this.from+'T00:00:00Z');if(this.to)p.set('to',this.to+'T23:59:59.999Z');return p.toString();}
  load(){return this.read(async()=>this.data.set(await this.api.get<Page<SecurityEvent>>('events?'+this.query())));}
  attributes(e:SecurityEvent){try{return JSON.parse(e.attributesJson);}catch{return {};}}
}
