import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { PatientsApiService } from '../core/patients/patients-api.service';
import { CreatedPatient, PatientListItem } from '../core/patients/patient.models';

@Component({
  selector: 'app-patient-list',
  imports: [ReactiveFormsModule],
  templateUrl: './patient-list.html',
  styleUrl: './patient-list.css',
})
export class PatientList implements OnInit {
  private readonly patientsApi = inject(PatientsApiService);
  private readonly formBuilder = inject(FormBuilder);
  protected readonly patients = signal<PatientListItem[]>([]);
  protected readonly loading = signal(true);
  protected readonly loadError = signal('');
  protected readonly formVisible = signal(false);
  protected readonly saving = signal(false);
  protected readonly submitError = signal('');
  protected readonly successMessage = signal('');
  private readonly currentDate = new Date();
  protected readonly today = `${this.currentDate.getFullYear()}-${String(this.currentDate.getMonth() + 1).padStart(2, '0')}-${String(this.currentDate.getDate()).padStart(2, '0')}`;
  protected readonly patientForm = this.formBuilder.nonNullable.group({
    documentType: ['', Validators.required],
    documentNumber: ['', Validators.required],
    firstName: ['', [Validators.required, Validators.pattern(/\S/)]],
    lastName: ['', [Validators.required, Validators.pattern(/\S/)]],
    dateOfBirth: [''],
    phone: [''],
    email: [''],
    address: [''],
  });

  ngOnInit(): void {
    this.loadPatients();
  }

  protected loadPatients(): void {
    this.loading.set(true);
    this.loadError.set('');
    this.patientsApi.list().subscribe({
      next: (patients) => {
        this.patients.set(patients);
        this.loading.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.loadError.set(this.apiErrorMessage(error));
        this.loading.set(false);
      },
    });
  }

  protected openForm(): void {
    this.submitError.set('');
    this.patientForm.reset();
    this.formVisible.set(true);
  }

  protected closeForm(): void {
    if (!this.saving()) this.formVisible.set(false);
  }

  protected submit(): void {
    if (this.patientForm.invalid) {
      this.patientForm.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    this.submitError.set('');
    this.successMessage.set('');
    const value = this.patientForm.getRawValue();
    this.patientsApi
      .create({
        documentType: value.documentType,
        documentNumber: value.documentNumber,
        firstName: value.firstName,
        lastName: value.lastName,
        dateOfBirth: value.dateOfBirth || null,
        phone: value.phone || null,
        email: value.email || null,
        address: value.address || null,
      })
      .subscribe({
        next: (created) => this.onCreated(created),
        error: (error: HttpErrorResponse) => {
          this.submitError.set(this.apiErrorMessage(error));
          this.saving.set(false);
        },
      });
  }

  protected medicalRecordLabel(number: number): string {
    return `HC-${String(number).padStart(6, '0')}`;
  }

  protected formatDate(value: string | null): string {
    if (!value) return '\u2014';
    return new Intl.DateTimeFormat('es-AR', { dateStyle: 'medium', timeZone: 'UTC' }).format(
      new Date(`${value}T00:00:00Z`),
    );
  }

  protected initials(patient: PatientListItem): string {
    return `${patient.firstName.charAt(0)}${patient.lastName.charAt(0)}`.toLocaleUpperCase('es-AR');
  }

  private onCreated(patient: CreatedPatient): void {
    this.successMessage.set(
      `Paciente registrado. Historia cl\u00EDnica ${this.medicalRecordLabel(patient.medicalRecordNumber)}.`,
    );
    this.formVisible.set(false);
    this.saving.set(false);
    this.loadPatients();
  }

  private apiErrorMessage(error: HttpErrorResponse): string {
    if (error.status === 0)
      return 'No se pudo conectar con la API. Verific\u00E1 que el backend est\u00E1 iniciado.';
    if (error.status === 409)
      return 'Ya existe un paciente con ese tipo y n\u00FAmero de documento.';
    if (error.status === 400)
      return 'Revis\u00E1 los datos ingresados. El nombre y el apellido son obligatorios y la fecha no puede ser futura.';
    return 'Ocurri\u00F3 un error al comunicarse con DentalPro. Intent\u00E1 nuevamente.';
  }
}
