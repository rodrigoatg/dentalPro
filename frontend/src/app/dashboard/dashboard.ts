import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PatientsApiService } from '../core/patients/patients-api.service';

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css',
})
export class Dashboard implements OnInit {
  private readonly patientsApi = inject(PatientsApiService);
  protected readonly patientCount = signal<number | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal('');

  ngOnInit(): void {
    this.patientsApi.list().subscribe({
      next: (patients) => {
        this.patientCount.set(patients.length);
        this.loading.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.error.set(
          error.status === 0 ? 'No se pudo conectar con la API.' : 'No se pudo cargar el resumen.',
        );
        this.loading.set(false);
      },
    });
  }
}
