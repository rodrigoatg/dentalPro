import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';
import { PatientsApiService } from '../core/patients/patients-api.service';
import { CreatedPatient, PatientListItem } from '../core/patients/patient.models';
import { AuthService } from '../core/auth/auth.service';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-patient-list',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './patient-list.html',
  styleUrl: './patient-list.css',
})
export class PatientList implements OnInit {
  private readonly patientsApi = inject(PatientsApiService);
  private readonly auth = inject(AuthService);
  private readonly formBuilder = inject(FormBuilder);
  protected readonly patients = signal<PatientListItem[]>([]);
  protected readonly loading = signal(true);
  protected readonly loadError = signal('');
  protected readonly formVisible = signal(false);
  protected readonly saving = signal(false);
  protected readonly submitError = signal('');
  protected readonly successMessage = signal('');
  protected readonly search = signal('');
  private loadSequence = 0;
  protected readonly canCreatePatients = () => this.auth.hasPermission('patients.create');
  protected readonly canViewClinicalRecords = () => this.auth.hasPermission('clinical.records.view');
  protected readonly documentTypes = [{ value: 'DNI', label: 'DNI' }];
  private readonly currentDate = new Date();
  protected readonly today = `${this.currentDate.getUTCFullYear()}-${String(this.currentDate.getUTCMonth() + 1).padStart(2, '0')}-${String(this.currentDate.getUTCDate()).padStart(2, '0')}`;
  private readonly dateNotInFuture: ValidatorFn = (control): ValidationErrors | null => {
    const value = control.value as string;
    return value && value > this.today ? { futureDate: true } : null;
  };
  protected readonly patientForm = this.formBuilder.nonNullable.group({
    documentType: ['', [Validators.required, Validators.pattern(/\S/)]],
    documentNumber: ['', [Validators.required, Validators.pattern(/\S/)]],
    firstName: ['', [Validators.required, Validators.pattern(/\S/)]],
    lastName: ['', [Validators.required, Validators.pattern(/\S/)]],
    dateOfBirth: ['', this.dateNotInFuture],
    phone: [''],
    email: ['', Validators.email],
    address: [''],
  });

  ngOnInit(): void {
    this.loadPatients();
  }

  protected loadPatients(): void {
    const requestSequence = ++this.loadSequence;
    this.loading.set(true);
    this.loadError.set('');
    this.patientsApi.list(this.search()).subscribe({
      next: (patients) => {
        if (requestSequence !== this.loadSequence) return;
        this.patients.set(patients);
        this.loading.set(false);
      },
      error: (error: HttpErrorResponse) => {
        if (requestSequence !== this.loadSequence) return;
        this.loadError.set(this.apiErrorMessage(error));
        this.loading.set(false);
      },
    });
  }

  protected setSearch(value: string): void {
    this.search.set(value.trim().replace(/\s+/g, ' '));
    this.loadPatients();
  }

  protected openForm(): void {
    if (!this.canCreatePatients()) return;

    this.submitError.set('');
    this.patientForm.reset();
    this.formVisible.set(true);
  }

  protected closeForm(): void {
    if (!this.saving()) this.formVisible.set(false);
  }

  protected submit(): void {
    if (!this.canCreatePatients()) return;

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
    if (error.status === 401) return 'La sesi\u00F3n venci\u00F3. Inici\u00E1 sesi\u00F3n nuevamente.';
    if (error.status === 403) return 'No ten\u00E9s permiso para acceder a los pacientes.';
    if (error.status === 409)
      return 'Ya existe un paciente con ese tipo y n\u00FAmero de documento.';
    if (error.status === 400)
      return 'Revis\u00E1 los datos: tipo y n\u00FAmero de documento, nombre, apellido, fecha de nacimiento y correo.';
    return 'Ocurri\u00F3 un error al comunicarse con DentalPro. Intent\u00E1 nuevamente.';
  }
}
