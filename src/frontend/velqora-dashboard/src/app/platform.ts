import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Platform } from './models';
import { PageUi } from './page';
@Component({selector:'app-platform',imports:[CommonModule],template:`
<div class="page-heading"><div><div class="eyebrow">PLATFORM OPERATIONS</div><h1>Platform health<span class="heading-dot">.</span></h1><p>Observe the pipeline behind every investigation.</p></div><button class="button" (click)="load()">↻ Refresh health</button></div>
@if(error()){<div class="notice error" role="alert">{{error()}}</div>}@if(success()){<div class="notice success">{{success()}}</div>}
@if(data();as d){<div class="metrics">
<div class="metric"><span class="metric-label">PostgreSQL <span>▤</span></span><strong class="health-word" [class.mint]="d.postgres">{{d.postgres?'Healthy':'Unavailable'}}</strong><span class="metric-note">Durable events, cases, and audit</span></div>
<div class="metric"><span class="metric-label">RabbitMQ <span>⌁</span></span><strong class="health-word" [class.mint]="d.broker">{{d.broker?'Connected':'Unavailable'}}</strong><span class="metric-note">{{d.broker?.consumers??0}} detection consumers</span></div>
<div class="metric"><span class="metric-label">Redis <span>◈</span></span><strong class="health-word" [class.mint]="d.redis">{{d.redis?'Healthy':'Unavailable'}}</strong><span class="metric-note">Rebuildable operational cache</span></div>
<div class="metric"><span class="metric-label">Detection worker <span>⌘</span></span><strong class="health-word" [class.mint]="d.worker.ready">{{d.worker.ready?'Healthy':'Degraded'}}</strong><span class="metric-note">{{d.worker.heartbeat?.lastSeenAt|date:'HH:mm:ss':'UTC'}} UTC heartbeat</span></div>
</div><div class="dashboard-grid"><section class="panel"><div class="panel-heading"><h2>Message delivery</h2></div><div class="detail-body"><dl><dt>Unpublished outbox messages</dt><dd class="mono">{{d.outboxPending|number}}</dd><dt>Messages waiting for retry</dt><dd class="mono">{{d.outboxRetries|number}}</dd><dt>Detection queue backlog</dt><dd class="mono">{{d.broker?.pending??'—'}}</dd><dt>Dead-letter messages</dt><dd class="mono">{{d.broker?.deadLetters??'—'}}</dd><dt>Durably processed events</dt><dd class="mono">{{d.processedEvents|number}}</dd><dt>Oldest unpublished event</dt><dd>{{d.oldestPending?(d.oldestPending|date:'medium':'UTC'):'None'}}</dd></dl>@if(auth.isAdmin){<button class="button" [disabled]="busy()||!d.broker?.deadLetters" (click)="replay()">Replay up to 100 dead letters</button>}</div></section>
<section class="panel"><div class="panel-heading"><h2>Observability tools</h2></div><div class="detail-body"><a class="tool-link" href="http://localhost:3000" target="_blank" rel="noopener"><span>▥</span><div>Grafana<small>Throughput, detection latency, errors</small></div><span>↗</span></a><a class="tool-link" href="http://localhost:16686" target="_blank" rel="noopener"><span>⌁</span><div>Jaeger<small>Trace an event across the pipeline</small></div><span>↗</span></a><a class="tool-link" href="http://localhost:15672" target="_blank" rel="noopener"><span>≋</span><div>RabbitMQ management<small>Queues, deliveries, and dead letters</small></div><span>↗</span></a><p class="muted spaced">Observability tools run with the monitoring Compose profile. Management ports bind to localhost.</p></div></section></div>}
`})
export class PlatformComponent extends PageUi {
  data=signal<Platform|null>(null);constructor(){super();this.poll(()=>this.load(),5000);}
  load(){return this.read(async()=>this.data.set(await this.api.get<Platform>('platform')));}
  replay(){return this.act(async()=>{const r=await this.api.post<{replayed:number}>('platform/replay-dead-letters',{});await this.load();this.success.set(r.replayed+' message(s) replayed.');});}
}
