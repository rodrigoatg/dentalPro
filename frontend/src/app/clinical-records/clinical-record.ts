import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from '../core/auth/auth.service';
import { ClinicalRecordsApiService } from '../core/clinical-records/clinical-records-api.service';
import { ClinicalRecord, CreateClinicalEntryRequest } from '../core/clinical-records/clinical-record.models';

interface DraftTreatment { type: string; description: string; status: string; priority: string; cost: string; toothNumbers: number[]; completedToothNumbers: number[]; }

@Component({
  selector: 'app-clinical-record',
  imports: [FormsModule, RouterLink],
  templateUrl: './clinical-record.html',
  styleUrl: './clinical-record.css',
})
export class ClinicalRecordPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(ClinicalRecordsApiService);
  protected readonly auth = inject(AuthService);
  protected readonly record = signal<ClinicalRecord | null>(null);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly error = signal('');
  protected readonly message = signal('');
  protected readonly patientId = signal('');
  protected readonly appointmentId = signal<string | null>(null);
  protected readonly formVisible = signal(false);
  protected readonly entryType = signal('Consultation');
  protected readonly occurredAt = signal(new Date().toISOString().slice(0, 16));
  protected readonly diagnosis = signal('');
  protected readonly notes = signal('');
  protected readonly treatmentType = signal('Other');
  protected readonly treatments = signal<DraftTreatment[]>([]);
  protected readonly selectedTeeth = signal<number[]>([]);
  protected readonly completedTeeth = signal<number[]>([]);
  protected readonly treatmentError = signal('');
  protected readonly toothRows = [18,17,16,15,14,13,12,11,21,22,23,24,25,26,27,28,38,37,36,35,34,33,32,31,41,42,43,44,45,46,47,48];
  protected readonly canCreateEntry = () => this.auth.hasPermission('clinical.entries.create');
  protected readonly canCreateTreatment = () => this.auth.hasPermission('clinical.treatments.create');

  ngOnInit(): void {
    this.patientId.set(this.route.snapshot.paramMap.get('patientId') ?? '');
    this.appointmentId.set(this.route.snapshot.queryParamMap.get('appointmentId'));
    this.load();
  }
  protected load(): void {
    this.loading.set(true); this.error.set('');
    this.api.get(this.patientId()).subscribe({
      next: (value) => { this.record.set(value); this.loading.set(false); },
      error: (err: HttpErrorResponse) => { this.error.set(this.errorMessage(err)); this.loading.set(false); },
    });
  }
  protected toggleTooth(tooth: number): void {
    this.selectedTeeth.update(items => items.includes(tooth) ? items.filter(x => x !== tooth) : [...items, tooth]);
    if (!this.selectedTeeth().includes(tooth)) this.completedTeeth.update(items => items.filter(x => x !== tooth));
  }
  protected toggleCompletedTooth(tooth: number): void {
    this.completedTeeth.update(items => items.includes(tooth) ? items.filter(x => x !== tooth) : [...items, tooth]);
  }
  protected setTreatmentStatus(value: string): void { this.treatmentStatus = value; this.completedTeeth.set([]); this.treatmentError.set(''); }
  protected addTreatment(): void {
    if (!this.canCreateTreatment()) return;
    const description = this.treatmentDescription.trim();
    if (!description) return;
    if (this.treatmentStatus === 'PartiallyCompleted' &&
        (this.completedTeeth().length === 0 || this.completedTeeth().length >= this.selectedTeeth().length)) {
      this.treatmentError.set('Para un tratamiento parcial, marcá al menos una pieza realizada y otra pendiente.'); return;
    }
    this.treatments.update(items => [...items, { type: this.treatmentType(), description, status: this.treatmentStatus,
      priority: 'Medium', cost: '', toothNumbers: [...this.selectedTeeth()], completedToothNumbers: [...this.completedTeeth()] }]);
    this.treatmentDescription = ''; this.selectedTeeth.set([]); this.completedTeeth.set([]); this.treatmentError.set('');
  }
  protected removeTreatment(index: number): void { this.treatments.update(items => items.filter((_, i) => i !== index)); }
  protected submit(): void {
    if (!this.canCreateEntry() || this.saving() || !this.notes().trim()) return;
    this.saving.set(true); this.error.set(''); this.message.set('');
    const request: CreateClinicalEntryRequest = {
      type: this.entryType(), occurredAt: new Date(this.occurredAt()).toISOString(), notes: this.notes().trim(),
      diagnosis: this.diagnosis().trim() || null, appointmentId: this.appointmentId(),
      treatments: this.treatments().map(item => ({ ...item, cost: item.cost ? Number(item.cost) : null })),
    };
    this.api.createEntry(this.patientId(), request).subscribe({
      next: () => { this.saving.set(false); this.formVisible.set(false); this.notes.set(''); this.diagnosis.set(''); this.treatments.set([]); this.message.set('Atención clínica registrada.'); this.load(); },
      error: (err: HttpErrorResponse) => { this.saving.set(false); this.error.set(this.errorMessage(err)); },
    });
  }
  protected formatDate(value: string): string { return new Intl.DateTimeFormat('es-AR', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)); }
  protected label(value: string): string { return ({ Consultation: 'Consulta', Treatment: 'Tratamiento', Observation: 'Observación', FollowUp: 'Seguimiento', ExternalRecord: 'Antecedente externo', Other: 'Otro', Planned: 'Planificado', InProgress: 'En curso', PartiallyCompleted: 'Parcialmente realizado', Completed: 'Completado', Cancelled: 'Cancelado' } as Record<string,string>)[value] ?? value; }
  protected toothNumbers(teeth: { toothNumber: number }[]): string { return teeth.map(tooth => tooth.toothNumber).join(', '); }
  protected treatmentDescription = '';
  protected treatmentStatus = 'Planned';
  private errorMessage(error: HttpErrorResponse): string {
    if (error.status === 0) return 'No se pudo conectar con la API.';
    if (error.status === 401) return 'La sesión venció. Iniciá sesión nuevamente.';
    if (error.status === 403) return 'No tenés permiso para acceder a esta historia clínica.';
    if (error.status === 404) return 'No se encontró la historia clínica.';
    if (error.status === 409) return 'El turno asociado no corresponde a este paciente.';
    if (error.status === 400) return 'Revisá los datos de la atención y los tratamientos.';
    return 'Ocurrió un error al comunicarse con DentalPro.';
  }
}
