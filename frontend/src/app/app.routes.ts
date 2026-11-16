import { Routes } from '@angular/router';
import { authenticatedGuard, anonymousOnlyGuard, permissionGuard } from './core/auth/auth.guards';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
  {
    path: 'login',
    canActivate: [anonymousOnlyGuard],
    loadComponent: () => import('./auth/login').then((module) => module.Login),
  },
  {
    path: 'forbidden',
    canActivate: [authenticatedGuard],
    loadComponent: () => import('./core/auth/forbidden').then((module) => module.Forbidden),
  },
  {
    path: 'dashboard',
    canActivate: [authenticatedGuard],
    loadComponent: () => import('./dashboard/dashboard').then((module) => module.Dashboard),
  },
  {
    path: 'patients/:patientId',
    canActivate: [permissionGuard],
    data: { permission: 'patients.view' },
    loadComponent: () => import('./patients/patient-detail').then((module) => module.PatientDetail),
  },
  {
    path: 'patients/:patientId/medical-record',
    canActivate: [permissionGuard],
    data: { permission: 'clinical.records.view' },
    loadComponent: () => import('./clinical-records/clinical-record').then((module) => module.ClinicalRecordPage),
  },
  {
    path: 'patients',
    canActivate: [permissionGuard],
    data: { permission: 'patients.view' },
    loadComponent: () => import('./patients/patient-list').then((module) => module.PatientList),
  },
  {
    path: 'appointments',
    canActivate: [permissionGuard],
    data: { permission: 'appointments.view' },
    loadComponent: () => import('./appointments/agenda').then((module) => module.Agenda),
  },
  {
    path: 'admin',
    canActivate: [permissionGuard],
    data: { permissionsAny: ['users.view', 'roles.view', 'permissions.view'] },
    loadComponent: () => import('./admin/admin-page').then((module) => module.AdminPage),
  },
  { path: '**', redirectTo: 'dashboard' },
];
