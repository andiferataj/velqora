import { Directive, DestroyRef, inject, signal } from '@angular/core';
import { Api, Auth, Live, message } from './api';
@Directive()
export abstract class PageUi {
  api=inject(Api); auth=inject(Auth); live=inject(Live); destroy=inject(DestroyRef);
  loading=signal(false); busy=signal(false); error=signal(''); success=signal('');
  protected async read(action:()=>Promise<void>) {
    this.loading.set(true);
    try {await action(); this.error.set('');} catch(e) {this.error.set(message(e));} finally {this.loading.set(false);}
  }
  async act(action:()=>Promise<void>) {
    if(this.busy()) return; this.busy.set(true); this.error.set(''); this.success.set('');
    try {await action();} catch(e) {this.error.set(message(e));} finally {this.busy.set(false);}
  }
  poll(action:()=>Promise<void>, milliseconds=10000) {
    void action();
    const timer=setInterval(()=> {if(!this.busy() && !this.loading()) void action();},milliseconds);
    this.destroy.onDestroy(()=>clearInterval(timer));
  }
}
