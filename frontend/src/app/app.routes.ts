import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
  {
    path: 'dashboard',
    loadComponent: () => import('./dashboard/dashboard').then((module) => module.Dashboard),
  },
  {
    path: 'patients',
    loadComponent: () => import('./patients/patient-list').then((module) => module.PatientList),
  },
  { path: '**', redirectTo: 'dashboard' },
];
