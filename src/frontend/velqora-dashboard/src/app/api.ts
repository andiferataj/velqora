import { Injectable, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { Router } from '@angular/router';
import { firstValueFrom, catchError, throwError } from 'rxjs';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { User } from './models';

export const sessionInterceptor: HttpInterceptorFn = (request, next) => {
  const router = inject(Router), auth = inject(Auth);
  return next(request).pipe(catchError((error: HttpErrorResponse) => {
    if (error.status === 401 && !request.url.includes('/auth/')) { auth.user.set(null); auth.loaded=false; void router.navigate(['/login']); }
    return throwError(() => error);
  }));
};
export function message(error: unknown): string {
  if (error instanceof HttpErrorResponse) return error.status === 0 ? 'Cannot reach the API. Check that the local services are running.' :
    error.status === 409 ? 'This record changed. Reload it and try again.' :
    error.error?.title || (error.status === 403 ? 'Your account cannot perform this action.' : 'The request failed.');
  return error instanceof Error ? error.message : 'An unexpected error occurred.';
}
@Injectable({providedIn: 'root'})
export class Api {
  private http = inject(HttpClient);
  get<T>(path: string) { return firstValueFrom(this.http.get<T>('/api/' + path)); }
  post<T>(path: string, body: unknown) { return firstValueFrom(this.http.post<T>('/api/' + path, body)); }
  put<T>(path: string, body: unknown) { return firstValueFrom(this.http.put<T>('/api/' + path, body)); }
}
@Injectable({providedIn: 'root'})
export class Auth {
  private api = inject(Api);
  user = signal<User | null>(null);
  loaded = false;
  get canOperate() { return this.user()?.roles.some(r => r === 'Admin' || r === 'Analyst') ?? false; }
  get isAdmin() { return this.user()?.roles.includes('Admin') ?? false; }
  async load() {
    if (this.loaded) return;
    try { this.user.set(await this.api.get<User>('auth/me')); } catch { this.user.set(null); }
    this.loaded = true;
  }
  async login(email: string, password: string) {
    await this.api.get('auth/csrf');
    await this.api.post('auth/login', {email, password});
    this.loaded = false; await this.load(); await this.api.get('auth/csrf');
  }
  async logout() { await this.api.post('auth/logout', {}); this.user.set(null); this.loaded = false; }
}
@Injectable({providedIn: 'root'})
export class Live {
  connected = signal(false);
  revision = signal(0);
  private connection?: HubConnection;
  async start() {
    if (this.connection && this.connection.state !== HubConnectionState.Disconnected) return;
    this.connection = new HubConnectionBuilder().withUrl('/hubs/soc').withAutomaticReconnect().configureLogging(LogLevel.Warning).build();
    this.connection.on('changed', () => this.revision.update(x => x + 1));
    this.connection.onreconnected(() => { this.connected.set(true); this.revision.update(x => x + 1); });
    this.connection.onreconnecting(() => this.connected.set(false));
    this.connection.onclose(() => this.connected.set(false));
    try { await this.connection.start(); this.connected.set(true); } catch { this.connected.set(false); }
  }
  async stop() { await this.connection?.stop(); this.connected.set(false); }
}
