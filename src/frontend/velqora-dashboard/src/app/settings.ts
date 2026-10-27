import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Audit, Page, User } from './models';
import { PageUi } from './page';
@Component({selector:'app-settings',imports:[CommonModule,FormsModule],template:`
<div class="page-heading"><div><div class="eyebrow">WORKSPACE ADMINISTRATION</div><h1>People & audit<span class="heading-dot">.</span></h1><p>Clear permissions and a durable history of critical actions.</p></div></div>
@if(error()){<div class="notice error" role="alert">{{error()}}</div>}@if(success()){<div class="notice success">{{success()}}</div>}
@if(!auth.isAdmin){<section class="panel empty"><h2>Administrator access required</h2><p>Workspace administration is available to Admin accounts.</p></section>}
@else{<div class="detail-grid"><section class="panel"><div class="panel-heading"><h2>Workspace accounts</h2></div><div class="table-wrap"><table><thead><tr><th>Email address</th><th>Role</th></tr></thead><tbody>@for(u of users();track u.id){<tr><td>{{u.email}}<small class="mono">{{u.id.slice(0,8)}}</small></td><td>{{u.roles.join(', ')}}</td></tr>}</tbody></table></div></section><section class="panel"><div class="panel-heading"><h2>Create local account</h2></div><form class="detail-body" (ngSubmit)="createUser()"><label>Email<input name="email" type="email" [(ngModel)]="email" required maxlength="256"></label><label>Initial password<input name="password" type="password" autocomplete="new-password" [(ngModel)]="password" minlength="12" maxlength="256" required></label><label>Role<select name="role" [(ngModel)]="role"><option>ReadOnly</option><option>Analyst</option><option>Admin</option></select></label><p class="muted">At least 12 characters with uppercase, lowercase, number, and symbol.</p><button class="button primary full" [disabled]="busy()">Create account</button></form></section></div>
<section class="panel spaced"><div class="panel-heading"><div><h2>Audit trail</h2><p>Append-only application and database protection</p></div><button class="button small" (click)="load()">↻ Refresh</button></div><div class="table-wrap"><table><thead><tr><th>Timestamp · UTC</th><th>Action</th><th>Actor</th><th>Resource</th><th>Details</th></tr></thead><tbody>@for(a of audit()?.items??[];track a.id){<tr><td class="mono">{{a.createdAt|date:'MMM d, HH:mm:ss':'UTC'}}</td><td class="table-title">{{a.action}}</td><td>{{actor(a.actorId)}}</td><td class="mono">{{a.resource.slice(0,16)}}</td><td class="audit-detail" [title]="a.detail">{{a.detail||'—'}}</td></tr>}@empty{<tr><td colspan="5" class="empty-cell">No audit records.</td></tr>}</tbody></table></div><div class="pagination"><span>{{audit()?.total??0}} audit entries</span><div><button class="button small" [disabled]="page===1" (click)="page=page-1;load()">Previous</button><span>{{page}}</span><button class="button small" [disabled]="page*25>=(audit()?.total??0)" (click)="page=page+1;load()">Next</button></div></div></section>}
`})
export class SettingsComponent extends PageUi {
  users=signal<User[]>([]);audit=signal<Page<Audit>|null>(null);email='';password='';role='ReadOnly';page=1;
  constructor(){super();if(this.auth.isAdmin)void this.load();}
  actor(id:string){return this.users().find(x=>x.id===id)?.email??id;}
  load(){return this.read(async()=>{const [u,a]=await Promise.all([this.api.get<User[]>('users'),this.api.get<Page<Audit>>('audit?page='+this.page)]);this.users.set(u);this.audit.set(a);});}
  createUser(){return this.act(async()=>{await this.api.post('users',{email:this.email,password:this.password,role:this.role});this.email='';this.password='';await this.load();this.success.set('Account created.');});}
}
