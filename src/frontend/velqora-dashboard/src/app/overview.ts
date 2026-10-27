import { Component, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { Analytics, Alert, Page } from './models';
import { PageUi } from './page';
@Component({selector:'app-overview',imports:[CommonModule,RouterLink,FormsModule],template:`
<div class="page-heading"><div><div class="eyebrow">WORKSPACE OVERVIEW</div><h1>Security at a glance<span class="heading-dot">.</span></h1><p>Your signals, investigations, and response. One clear picture.</p></div><div class="heading-actions"><select aria-label="Analytics date range" [(ngModel)]="days" (change)="load()"><option [ngValue]="1">Last 24 hours</option><option [ngValue]="7">Last 7 days</option><option [ngValue]="30">Last 30 days</option></select><a class="button primary" routerLink="/lab">◇ Launch simulation</a></div></div>
@if(error()) {<div class="notice error" role="alert">{{error()}}</div>}
@if(data(); as d) {
<div class="metrics">
  <a class="metric" routerLink="/events"><span class="metric-label">Events ingested <span>≋</span></span><strong>{{d.eventCount | number}}</strong><span class="metric-note"><i class="live-dot"></i> Within selected period</span></a>
  <a class="metric" routerLink="/alerts"><span class="metric-label">Open alerts <span class="amber">△</span></span><strong>{{d.openAlerts | number}}<small class="amber">TO REVIEW</small></strong><span class="metric-note">{{d.alertCount}} detections in selected period</span></a>
  <a class="metric" routerLink="/incidents"><span class="metric-label">Active incidents <span>▣</span></span><strong>{{d.activeIncidents | number}}</strong><span class="metric-note">Open, investigating, or contained</span></a>
  <a class="metric" routerLink="/assets"><span class="metric-label">Monitored assets <span>▦</span></span><strong>{{d.assetCount | number}}</strong><span class="metric-note">{{d.enabledRules}} detection rules enabled</span></a>
</div>
<div class="dashboard-grid"><section class="panel activity-panel"><div class="panel-heading"><div><h2>Event activity</h2><p>Ingested telemetry over the last {{d.days}} day(s)</p></div><span class="legend"><i class="live-dot"></i> Security events</span></div>
@if(d.trend.length) {<div class="chart"><div class="chart-y"><span>{{peak() | number}}</span><span>{{peak()/2 | number:'1.0-0'}}</span><span>0</span></div><svg viewBox="0 0 800 190" preserveAspectRatio="none" role="img" aria-label="Daily security event activity"><defs><linearGradient id="area" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stop-color="#80e8b4" stop-opacity=".22"/><stop offset="100%" stop-color="#80e8b4" stop-opacity="0"/></linearGradient></defs><path d="M0 20H800 M0 95H800 M0 170H800" class="chart-grid"/>@if(d.trend.length===1){<rect x="390" width="20" [attr.y]="170-d.trend[0].count/peak()*140" [attr.height]="10+d.trend[0].count/peak()*140" fill="#88f0bd" rx="3"><title>{{d.trend[0].count}} events on {{d.trend[0].day|date:'MMM d':'UTC'}}</title></rect>}@else{<polygon [attr.points]="'0,180 '+points()+' 800,180'" fill="url(#area)"/><polyline [attr.points]="points()" fill="none" stroke="#88f0bd" stroke-width="2.5" vector-effect="non-scaling-stroke"/>}</svg></div><div class="chart-labels" [style.justify-content]="d.trend.length===1 ? 'center' : null">@for(point of d.trend; track point.day){<span>{{point.day | date:'MMM d':'UTC'}}</span>}</div>}
@else {<div class="empty chart-empty"><span>≋</span><h3>Waiting for your first signal</h3><p>Launch a scenario in the Simulation Lab to populate this view.</p><a routerLink="/lab">Open Simulation Lab →</a></div>}
</section><section class="panel"><div class="panel-heading"><div><h2>Alert distribution</h2><p>Detections by severity</p></div><span class="muted">△</span></div><div class="severity-total"><strong>{{d.alertCount}}</strong><span>total detections</span></div>
@for(level of ['Critical','High','Medium','Low']; track level){<div class="severity-line"><span><i class="severity-dot" [class]="level.toLowerCase()"></i>{{level}}</span><strong>{{count(level)}}</strong></div><div class="bar-track"><div [class]="'bar '+level.toLowerCase()" [style.width.%]="d.alertCount ? count(level)/d.alertCount*100 : 0"></div></div>}
</section></div>
<section class="panel"><div class="panel-heading"><div><h2>Latest detections <span class="count-chip">{{d.alertCount}}</span></h2><p>Recent signals that need a closer look</p></div><a routerLink="/alerts" class="text-link">View all alerts ↗</a></div><div class="table-wrap"><table><thead><tr><th>Detection</th><th>Severity</th><th>Asset / identity</th><th>Status</th><th>Detected</th><th></th></tr></thead><tbody>
@for(a of recent(); track a.id){<tr><td><a [routerLink]="['/alerts',a.id]" class="table-title">{{a.title}}</a><small class="mono">{{a.id.slice(0,8)}} · {{a.synthetic ? 'SYNTHETIC' : 'INTEGRATION'}}</small></td><td><span [class]="'badge '+a.severity.toLowerCase()">{{a.severity}}</span></td><td>{{a.device}}<small>{{a.account}}</small></td><td><span class="status"><i></i>{{a.status}}</span></td><td class="muted">{{a.detectedAt | date:'MMM d, HH:mm':'UTC'}}</td><td><a [routerLink]="['/alerts',a.id]" aria-label="Investigate alert">↗</a></td></tr>}
@empty {<tr><td colspan="6" class="empty-cell">No detections yet. Run a simulation to see the event pipeline in action.</td></tr>}
</tbody></table></div></section>
<div class="bottom-grid"><a class="lab-callout" routerLink="/lab"><span class="callout-icon">◇</span><div><span class="eyebrow">THE SIMULATION LAB</span><h3>See the whole response in motion.</h3><p>Generate a scenario. Follow the evidence. Contain the threat.</p></div><span class="callout-arrow">↗</span></a><section class="panel compact-panel"><span class="eyebrow">INVESTIGATION METRICS</span><div class="two-stats"><div><strong>{{d.meanResolutionMinutes === null ? '—' : (d.meanResolutionMinutes | number:'1.0-1')}}</strong><small>Mean resolution · minutes</small></div><div><strong>{{d.syntheticPrecision === null ? '—' : (d.syntheticPrecision | percent:'1.0-0')}}</strong><small>Precision · synthetic labels</small></div></div></section></div>
} @else {<div class="empty">{{loading() ? 'Loading operations overview…' : 'Overview unavailable. Check platform connectivity.'}}</div>}
`})
export class OverviewComponent extends PageUi {
  data=signal<Analytics|null>(null); recent=signal<Alert[]>([]); days=7;
  peak=computed(()=>Math.max(1,...(this.data()?.trend.map(x=>x.count) ?? [])));
  points=computed(()=>{const points=this.data()?.trend ?? []; return points.map((x,i)=>`${points.length===1?400:i/(points.length-1)*800},${170-x.count/this.peak()*140}`).join(' ');});
  constructor(){super();this.poll(()=>this.load(),5000);}
  count(level:string){return this.data()?.severities.find(x=>x.name===level)?.count ?? 0;}
  load(){return this.read(async()=>{const [d,a]=await Promise.all([this.api.get<Analytics>('analytics?days='+this.days),this.api.get<Page<Alert>>('alerts?size=5')]);this.data.set(d);this.recent.set(a.items);});}
}
