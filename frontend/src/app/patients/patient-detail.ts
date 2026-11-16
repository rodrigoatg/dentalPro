import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from '../core/auth/auth.service';
import { PatientsApiService } from '../core/patients/patients-api.service';
import { CreatedPatient } from '../core/patients/patient.models';

@Component({
  selector: 'app-patient-detail',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './patient-detail.html',
  styleUrl: './patient-detail.css',
})
export class PatientDetail implements OnInit {
  private readonly api = inject(PatientsApiService);
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder);
  protected readonly patient = signal<CreatedPatient | null>(null);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly editing = signal(false);
  protected readonly error = signal('');
  protected readonly message = signal('');
  protected readonly canEdit = () => this.auth.hasPermission('patients.update');
  protected readonly canViewClinical = () => this.auth.hasPermission('clinical.records.view');
  protected readonly form = this.fb.nonNullable.group({
    documentType: ['', [Validators.required, Validators.pattern(/\S/)]], documentNumber: ['', [Validators.required, Validators.pattern(/\S/)]],
    firstName: ['', [Validators.required, Validators.pattern(/\S/)]],
    lastName: ['', [Validators.required, Validators.pattern(/\S/)]],
    dateOfBirth: [''], phone: [''], email: ['', Validators.email], address: [''],
  });
  private readonly id = this.route.snapshot.paramMap.get('patientId') ?? '';

  ngOnInit(): void { this.load(); }

  protected startEditing(): void {
    if (this.canEdit()) this.editing.set(true);
  }

  protected load(): void {
    this.loading.set(true); this.error.set('');
    this.api.get(this.id).subscribe({
      next: (patient) => { this.patient.set(patient); this.form.patchValue({
        documentType: patient.documentType, documentNumber: patient.documentNumber,
        firstName: patient.firstName, lastName: patient.lastName, dateOfBirth: patient.dateOfBirth ?? '',
        phone: patient.phone ?? '', email: patient.email ?? '', address: patient.address ?? '',
      }); this.loading.set(false); },
      error: (error: HttpErrorResponse) => { this.error.set(this.messageFor(error)); this.loading.set(false); },
    });
  }

  protected save(): void {
    if (!this.canEdit() || !this.patient()) return;
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.saving.set(true); this.error.set(''); this.message.set('');
    const value = this.form.getRawValue();
    this.api.update(this.id, { ...value, dateOfBirth: value.dateOfBirth || null,
      phone: value.phone || null, email: value.email || null, address: value.address || null }).subscribe({
      next: () => { this.saving.set(false); this.editing.set(false); this.message.set('Datos personales actualizados.'); this.load(); },
      error: (error: HttpErrorResponse) => { this.saving.set(false); this.error.set(this.messageFor(error)); },
    });
  }

  private messageFor(error: HttpErrorResponse): string {
    if (error.status === 0) return 'No se pudo conectar con la API.';
    if (error.status === 401) return 'La sesión venció. Iniciá sesión nuevamente.';
    if (error.status === 403) return 'No tenés permiso para realizar esta operación.';
    if (error.status === 404) return 'No se encontró el paciente.';
    if (error.status === 409) return 'Ya existe un paciente con ese tipo y número de documento.';
    if (error.status === 400) return 'Revisá los campos obligatorios, el correo y la fecha de nacimiento.';
    return 'Ocurrió un error al comunicarse con DentalPro.';
  }
}
