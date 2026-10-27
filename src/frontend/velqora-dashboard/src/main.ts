import { bootstrapApplication } from '@angular/platform-browser';
import { provideHttpClient, withInterceptors, withXsrfConfiguration } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { AppComponent } from './app/app';
import { routes } from './app/routes';
import { sessionInterceptor } from './app/api';
bootstrapApplication(AppComponent, { providers: [
  provideHttpClient(withXsrfConfiguration({ cookieName: 'XSRF-TOKEN', headerName: 'X-XSRF-TOKEN' }), withInterceptors([sessionInterceptor])),
  provideRouter(routes)
]}).catch(error => console.error('Application startup failed', error));
