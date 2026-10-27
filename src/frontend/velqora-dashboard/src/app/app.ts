import { Component, effect, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { Auth, Live, message } from './api';
import { IconComponent } from './icon';
@Component({
  selector: 'app-root', imports: [RouterOutlet, RouterLink, RouterLinkActive, IconComponent],
  template: `
  @if (auth.user()) {
    <div class="shell">
      <aside class="sidebar" [class.open]="menuOpen()">
        <a class="brand" routerLink="/"><span class="brand-mark">V</span><span>velqora<span class="brand-sub">SECURITY OPERATIONS</span></span></a>
        <div class="workspace"><span class="workspace-icon">P</span><div>Prishtina Lab<small>Local workspace</small></div><span class="live-dot"></span></div>
        <p class="nav-label">WORKSPACE</p>
        <nav aria-label="Main navigation">
          @for (item of navigation; track item.path) {
            <a [routerLink]="item.path" [attr.aria-label]="item.label" routerLinkActive="active" [routerLinkActiveOptions]="{exact: item.path === '/'}" (click)="menuOpen.set(false)">
              <app-icon [name]="item.icon" />{{item.label}} @if(item.label === 'Simulation Lab') {<span class="nav-badge" aria-hidden="true">LAB</span>}
            </a>
          }
        </nav>
        <div class="sidebar-bottom"><div class="environment"><span class="live-dot"></span> LOCAL ENVIRONMENT</div><p>Synthetic telemetry. Real workflows.</p>
          <div class="profile"><span class="avatar">{{auth.user()?.email?.slice(0, 1)?.toUpperCase()}}</span><div>{{auth.user()?.email?.split('@')?.[0]}}<small>{{auth.user()?.roles?.join(', ')}}</small></div>
          <button class="icon-button" aria-label="Sign out" (click)="logout()"><app-icon name="logout" /></button></div>
        </div>
      </aside>
      <div class="main-shell">
        <header class="topbar"><button class="icon-button mobile-menu" aria-label="Toggle navigation" (click)="menuOpen.set(!menuOpen())"><app-icon name="menu" /></button><div class="breadcrumb">Workspace <span>/</span> Security operations</div>
          <div class="top-actions"><span class="connection" [class.offline]="!live.connected()"><span class="live-dot"></span>{{live.connected() ? 'Live connection' : 'Live updates offline'}}</span><span class="top-divider"></span><span class="local-tag">LOCAL</span></div>
        </header>
        <main id="main-content"><router-outlet /></main>
        <footer>VELQORA SOC <span>Authorized data · Simulated response actions</span><span>v0.1.0</span></footer>
      </div>
    </div>
  } @else { <router-outlet /> }
  @if(error()) { <div class="toast error" role="alert">{{error()}}<button class="icon-button" aria-label="Dismiss" (click)="error.set('')">×</button></div> }
  `
})
export class AppComponent {
  auth = inject(Auth); live = inject(Live); router = inject(Router); menuOpen = signal(false); error = signal('');
  navigation = [
    {path:'/',label:'Overview',icon:'overview'}, {path:'/events',label:'Event explorer',icon:'events'},
    {path:'/alerts',label:'Alerts',icon:'alerts'}, {path:'/incidents',label:'Incidents',icon:'incidents'},
    {path:'/assets',label:'Asset inventory',icon:'assets'}, {path:'/rules',label:'Detection rules',icon:'rules'},
    {path:'/playbooks',label:'Response playbooks',icon:'playbooks'}, {path:'/lab',label:'Simulation Lab',icon:'lab'},
    {path:'/analytics',label:'Analytics',icon:'analytics'}, {path:'/platform',label:'Platform health',icon:'platform'},
    {path:'/settings',label:'Administration',icon:'settings'}
  ];
  constructor() { effect(() => { if (this.auth.user()) void this.live.start(); else void this.live.stop(); }); }
  async logout() { try { await this.auth.logout(); await this.router.navigate(['/login']); } catch(e) { this.error.set(message(e)); } }
}
