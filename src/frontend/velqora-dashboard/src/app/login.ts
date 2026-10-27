import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { Router } from '@angular/router';
import { Auth, message } from './api';
@Component({selector:'app-login',imports:[FormsModule,MatButtonModule],template:`
<div class="login-page"><section class="login-story"><a class="brand"><span class="brand-mark">V</span><span>velqora</span></a>
<div><span class="eyebrow">SECURITY OPERATIONS, CONNECTED.</span><h1>Clarity in <br>every signal.</h1><p>From the first event to the final response. Investigate threats, coordinate your team, and keep the whole picture in view.</p><div class="login-grid"><span>01 / DETECT</span><span>02 / INVESTIGATE</span><span>03 / RESPOND</span></div></div>
<span class="muted">PRISHTINA LAB · LOCAL ENVIRONMENT</span></section>
<section class="login-form"><form (ngSubmit)="login()"><span class="eyebrow">YOUR OPERATIONS CENTER</span><h2>Welcome back</h2><p class="muted">Sign in to your Velqora workspace.</p>
<label>Email address<input name="email" type="email" autocomplete="username" [(ngModel)]="email" required placeholder="analyst@velqora.local"></label>
<label>Password<input name="password" type="password" autocomplete="current-password" [(ngModel)]="password" required maxlength="256"></label>
@if(error()) {<div class="notice error" role="alert">{{error()}}</div>}
<button mat-flat-button class="button primary full" type="submit" [disabled]="busy()">{{busy() ? 'Signing in…' : 'Sign in to workspace →'}}</button>
<p class="login-note">Demo accounts: admin, analyst, or viewer &#64;velqora.local.<br>Use the password generated during local setup.</p></form></section></div>`})
export class LoginComponent {
  private auth=inject(Auth); private router=inject(Router);
  email='analyst@velqora.local'; password=''; busy=signal(false); error=signal('');
  async login() { if(this.busy()) return; this.busy.set(true); this.error.set('');
    try { await this.auth.login(this.email,this.password); this.password=''; await this.router.navigate(['/']); }
    catch(e) {this.error.set(message(e));} finally {this.busy.set(false);}
  }
}
