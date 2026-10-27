import { Component, input } from '@angular/core';
@Component({
 selector:'app-icon',
 template:`<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path [attr.d]="paths[name()] || paths['activity']"/></svg>`,
 styles:[`:host{display:inline-flex;width:18px;height:18px;flex-shrink:0}svg{width:100%;height:100%}`]
})
export class IconComponent {
 name=input('activity');
 paths:Record<string,string>={
  overview:'M3 3h7v7H3z M14 3h7v7h-7z M3 14h7v7H3z M14 14h7v7h-7z',
  events:'M4 5h16 M4 12h16 M4 19h16 M8 3v4 M16 10v4 M10 17v4',
  alerts:'M12 3 2 21h20L12 3z M12 9v5 M12 17h.01',
  incidents:'M3 7h7l2-3h9v16H3V7z M7 12h10 M7 16h6',
  assets:'M3 4h18v12H3z M8 21h8 M12 16v5',
  rules:'M12 3 3 7v6c0 5 9 9 9 9s9-4 9-9V7l-9-4z M8 12l3 3 5-6',
  playbooks:'M5 3h14v18H5z M9 8h6 M9 12h6 M9 16h3',
  lab:'M9 3h6 M10 3v6L4 20h16L14 9V3 M7 15h10',
  analytics:'M4 20V10 M10 20V4 M16 20v-8 M22 20H2',
  platform:'M2 12h5l3-8 4 16 3-8h5',
  settings:'M4 6h16 M4 12h16 M4 18h16 M8 3v6 M16 9v6 M10 15v6',
  logout:'M10 4H4v16h6 M14 8l4 4-4 4 M8 12h12',
  menu:'M4 6h16 M4 12h16 M4 18h16',
  activity:'M2 12h5l3-8 4 16 3-8h5'
 };
}
