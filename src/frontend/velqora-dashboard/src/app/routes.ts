import { inject } from '@angular/core';
import { CanActivateFn, Router, Routes } from '@angular/router';
import { Auth } from './api';
const authenticated: CanActivateFn = async () => {
  const auth = inject(Auth), router = inject(Router);
  await auth.load();
  return auth.user() ? true : router.createUrlTree(['/login']);
};
export const routes: Routes = [
  {path: 'login', loadComponent: () => import('./login').then(m => m.LoginComponent)},
  {path: '', canActivate: [authenticated], children: [
    {path: '', pathMatch: 'full', loadComponent: () => import('./overview').then(m => m.OverviewComponent)},
    {path: 'events', loadComponent: () => import('./events').then(m => m.EventsComponent)},
    {path: 'alerts', loadComponent: () => import('./alerts').then(m => m.AlertsComponent)},
    {path: 'alerts/:id', loadComponent: () => import('./alerts').then(m => m.AlertDetailComponent)},
    {path: 'incidents', loadComponent: () => import('./incidents').then(m => m.IncidentsComponent)},
    {path: 'incidents/:id', loadComponent: () => import('./incidents').then(m => m.IncidentDetailComponent)},
    {path: 'assets', loadComponent: () => import('./assets').then(m => m.AssetsComponent)},
    {path: 'rules', loadComponent: () => import('./rules').then(m => m.RulesComponent)},
    {path: 'playbooks', loadComponent: () => import('./playbooks').then(m => m.PlaybooksComponent)},
    {path: 'lab', loadComponent: () => import('./lab').then(m => m.LabComponent)},
    {path: 'analytics', loadComponent: () => import('./overview').then(m => m.OverviewComponent), data: {analytics: true}},
    {path: 'platform', loadComponent: () => import('./platform').then(m => m.PlatformComponent)},
    {path: 'settings', loadComponent: () => import('./settings').then(m => m.SettingsComponent)}
  ]},
  {path: '**', redirectTo: ''}
];
