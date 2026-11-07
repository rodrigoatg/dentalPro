import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PatientsApiService } from '../core/patients/patients-api.service';
import { AuthService } from '../core/auth/auth.service';

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css',
})
export class Dashboard implements OnInit {
  private readonly patientsApi = inject(PatientsApiService);
  private readonly auth = inject(AuthService);
  protected readonly canViewPatients = () => this.auth.hasPermission('patients.view');
  protected readonly canCreatePatients = () => this.auth.hasPermission('patients.create');
  protected readonly patientCount = signal<number | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal('');

  ngOnInit(): void {
    if (!this.canViewPatients()) {
      this.loading.set(false);
      return;
    }
    this.patientsApi.list().subscribe({
      next: (patients) => {
        this.patientCount.set(patients.length);
        this.loading.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.error.set(
          error.status === 0
            ? 'No se pudo conectar con la API.'
            : error.status === 401
              ? 'La sesi\u00F3n venci\u00F3. Inici\u00E1 sesi\u00F3n nuevamente.'
              : error.status === 403
                ? 'No ten\u00E9s permiso para consultar pacientes.'
                : 'No se pudo cargar el resumen.',
        );
        this.loading.set(false);
      },
    });
  }
}
