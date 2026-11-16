import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin, of } from 'rxjs';
import { AuthService } from '../core/auth/auth.service';
import { AppointmentsApiService } from '../core/appointments/appointments-api.service';
import {
  Appointment, AppointmentInterval, ClinicDateException, ClinicSchedule, ClinicScheduleSettings,
  ConfigureClinicSchedule, DentistOption, WeeklyOpeningInterval,
} from '../core/appointments/appointment.models';
import { PatientListItem } from '../core/patients/patient.models';
import { PatientsApiService } from '../core/patients/patients-api.service';
import { ActivatedRoute, RouterLink } from '@angular/router';

interface AppointmentDay { date: string; label: string; appointments: Appointment[]; }
interface StartOption { value: string; label: string; }
interface EditableWeeklyInterval { dayOfWeek: string; start: string; end: string; }

@Component({
  selector: 'app-agenda',
  imports: [FormsModule, RouterLink],
  templateUrl: './agenda.html',
  styleUrl: './agenda.css',
})
export class Agenda implements OnInit {
  private readonly api = inject(AppointmentsApiService);
  private readonly patientsApi = inject(PatientsApiService);
  private readonly auth = inject(AuthService);
  protected readonly patientFilterId = inject(ActivatedRoute).snapshot.queryParamMap.get('patientId');
  private readonly viewPermission = 'appointments.view';
  protected readonly appointments = signal<Appointment[]>([]);
  protected readonly patients = signal<PatientListItem[]>([]);
  protected readonly dentists = signal<DentistOption[]>([]);
  protected readonly schedule = signal<ClinicSchedule | null>(null);
  protected readonly settings = signal<ClinicScheduleSettings | null>(null);
  protected readonly loading = signal(true);
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  protected readonly message = signal('');
  protected readonly scheduleError = signal('');
  protected readonly scheduleMessage = signal('');
  protected readonly formVisible = signal(false);
  protected readonly scheduleVisible = signal(false);
  protected readonly editingAppointment = signal<Appointment | null>(null);
  protected readonly weekStart = signal(this.mondayOf(this.todayInZone('UTC')));
  protected readonly focusedDate = signal(this.todayInZone('UTC'));
  protected readonly selectedPatientId = signal('');
  protected readonly selectedDentistId = signal('');
  protected readonly selectedDate = signal('');
  protected readonly selectedStart = signal('');
  protected readonly duration = signal('');
  protected readonly notes = signal('');
  protected readonly availability = signal<AppointmentInterval[]>([]);
  protected readonly defaultDuration = signal(0);
  protected readonly increment = signal(0);
  protected readonly slotOptions = signal<StartOption[]>([]);
  protected readonly selectedExceptionDate = signal('');
  protected readonly exceptionStart = signal('');
  protected readonly exceptionEnd = signal('');
  protected readonly exceptionIntervals = signal<{ start: string; end: string }[]>([]);
  protected readonly timeZoneInput = signal('');
  protected readonly defaultDurationInput = signal('');
  protected readonly incrementInput = signal('');
  protected readonly weeklyIntervals = signal<EditableWeeklyInterval[]>([]);
  protected readonly weekDays = computed<AppointmentDay[]>(() => {
    const zone = this.settings()?.timeZoneId ?? 'UTC';
    return Array.from({ length: 7 }, (_, index) => {
      const date = this.addDays(this.weekStart(), index);
      const dayAppointments = this.appointments()
        .filter((appointment) => !this.patientFilterId || appointment.patientId === this.patientFilterId)
        .filter((appointment) => this.dateInZone(appointment.startDateTime, zone) === date)
        .sort((a, b) => a.startDateTime.localeCompare(b.startDateTime));
      return { date, label: this.dayLabel(date), appointments: dayAppointments };
    });
  });
  protected readonly canScheduleManage = () => this.auth.hasPermission('appointments.schedule.manage');
  protected readonly canCreate = () => this.auth.hasPermission('appointments.create') && this.settings() !== null;
  protected readonly canViewClinicalRecords = () => this.auth.hasPermission('clinical.records.view');
  protected readonly canViewPatients = () => this.auth.hasPermission('patients.view');

  ngOnInit(): void {
    this.loadReferenceData();
  }

  protected loadReferenceData(): void {
    this.loading.set(true);
    this.error.set('');
    forkJoin({
      appointments: this.api.list(this.queryFrom(), this.queryTo()),
      patients: this.auth.hasPermission('patients.view') ? this.patientsApi.list() : of([]),
      dentists: this.auth.hasPermission('appointments.create') ? this.api.dentists() : of([]),
    })
      .subscribe({
        next: ({ appointments, patients, dentists }) => {
          this.appointments.set(appointments);
          this.patients.set(patients.filter((patient) => patient.isActive));
          this.dentists.set(dentists);
          this.loading.set(false);
        },
        error: (error: HttpErrorResponse) => { this.error.set(this.errorMessage(error)); this.loading.set(false); },
      });
    this.api.scheduleSettings().subscribe({
      next: (settings) => {
        this.settings.set(settings);
        this.focusedDate.set(this.todayInZone(settings.timeZoneId));
        this.weekStart.set(this.mondayOf(this.focusedDate()));
        this.loadAppointments();
      },
      error: (error: HttpErrorResponse) => {
        if (error.status === 409) this.scheduleError.set('La clínica todavía no tiene horarios configurados.');
        else if (error.status !== 0) this.scheduleError.set(this.errorMessage(error));
      },
    });
  }

  protected loadAppointments(): void {
    this.loading.set(true);
    this.api.list(this.queryFrom(), this.queryTo()).subscribe({
      next: (appointments) => { this.appointments.set(appointments); this.loading.set(false); },
      error: (error: HttpErrorResponse) => { this.error.set(this.errorMessage(error)); this.loading.set(false); },
    });
  }

  protected shiftWeek(amount: number): void {
    this.weekStart.set(this.addDays(this.weekStart(), amount * 7));
    this.focusedDate.set(this.weekStart());
    this.loadAppointments();
  }
  protected shiftDay(amount: number): void {
    const date = this.addDays(this.focusedDate(), amount);
    this.focusedDate.set(date);
    if (date < this.weekStart() || date > this.addDays(this.weekStart(), 6)) {
      this.weekStart.set(this.mondayOf(date));
      this.loadAppointments();
    }
  }
  protected goToday(): void {
    this.focusedDate.set(this.todayInZone(this.settings()?.timeZoneId ?? 'UTC'));
    this.weekStart.set(this.mondayOf(this.focusedDate()));
    this.loadAppointments();
  }
  protected weekLabel(): string {
    const start = new Date(`${this.weekStart()}T12:00:00Z`);
    const end = new Date(`${this.addDays(this.weekStart(), 6)}T12:00:00Z`);
    return `${new Intl.DateTimeFormat('es-AR', { month: 'short', timeZone: 'UTC' }).format(start)} – ${new Intl.DateTimeFormat('es-AR', { month: 'short', year: 'numeric', timeZone: 'UTC' }).format(end)}`;
  }
  protected dayLabel(date: string): string {
    return new Intl.DateTimeFormat('es-AR', { weekday: 'short', day: 'numeric', month: 'short', timeZone: 'UTC' }).format(new Date(`${date}T12:00:00Z`));
  }
  protected timeLabel(value: string): string {
    return new Intl.DateTimeFormat('es-AR', { hour: '2-digit', minute: '2-digit', timeZone: this.settings()?.timeZoneId ?? 'UTC' }).format(new Date(value));
  }
  protected statusLabel(status: string): string {
    return ({ Scheduled: 'Programado', Confirmed: 'Confirmado', Completed: 'Completado', Cancelled: 'Cancelado', Rescheduled: 'Reprogramado' } as Record<string, string>)[status] ?? status;
  }
  protected statusClass(status: string): string { return status.toLowerCase(); }

  protected openCreate(): void {
    if (!this.canCreate()) return;
    this.editingAppointment.set(null);
    this.selectedPatientId.set('');
    this.selectedDentistId.set('');
    this.selectedDate.set(this.todayInZone(this.settings()?.timeZoneId ?? 'UTC'));
    this.selectedStart.set('');
    this.duration.set('');
    this.notes.set('');
    this.formVisible.set(true);
    this.refreshAvailability();
  }
  protected openReschedule(appointment: Appointment): void {
    if (!this.auth.hasPermission('appointments.update')) return;
    this.editingAppointment.set(appointment);
    this.selectedPatientId.set(appointment.patientId);
    this.selectedDentistId.set(appointment.dentistId);
    this.selectedDate.set(this.dateInZone(appointment.startDateTime, this.settings()?.timeZoneId ?? 'UTC'));
    this.selectedStart.set('');
    this.duration.set(String(appointment.durationMinutes));
    this.notes.set(appointment.notes ?? '');
    this.formVisible.set(true);
    this.refreshAvailability();
  }
  protected closeForm(): void { if (!this.busy()) this.formVisible.set(false); }
  protected refreshAvailability(): void {
    const dentistId = this.selectedDentistId();
    this.availability.set([]);
    this.slotOptions.set([]);
    if (!dentistId || !this.formVisible()) return;
    const date = this.selectedDate();
    if (!date) return;
    const request = this.api.availability(
      new Date(Date.parse(`${date}T00:00:00Z`) - 86_400_000).toISOString(),
      new Date(Date.parse(`${date}T00:00:00Z`) + 2 * 86_400_000).toISOString(),
      dentistId, this.selectedPatientId() || undefined);
    request.subscribe({
      next: (availability) => {
        this.availability.set(availability.availableIntervals);
        this.defaultDuration.set(availability.defaultAppointmentDurationMinutes);
        this.increment.set(availability.startIncrementMinutes);
        this.updateSlots();
      },
      error: (error: HttpErrorResponse) => this.error.set(this.errorMessage(error)),
    });
  }
  protected updateSlots(): void {
    const duration = Number(this.duration()) || this.defaultDuration();
    const step = this.increment();
    if (duration <= 0 || step <= 0) { this.slotOptions.set([]); return; }
    const options: StartOption[] = [];
    for (const interval of this.availability()) {
      const intervalStart = new Date(interval.start).getTime();
      const intervalEnd = new Date(interval.end).getTime();
      for (let start = intervalStart; start + duration * 60_000 <= intervalEnd; start += step * 60_000) {
        const instant = new Date(start);
        if (this.dateInZone(instant.toISOString(), this.settings()?.timeZoneId ?? 'UTC') !== this.selectedDate()) continue;
        if (this.isAmbiguousWallTime(instant, this.settings()?.timeZoneId ?? 'UTC')) continue;
        options.push({ value: instant.toISOString(), label: `${this.dayLabel(this.dateInZone(instant.toISOString(), this.settings()?.timeZoneId ?? 'UTC'))} ${this.timeLabel(instant.toISOString())}` });
      }
    }
    this.slotOptions.set(options);
    if (!options.some((option) => option.value === this.selectedStart())) this.selectedStart.set('');
  }
  protected saveAppointment(): void {
    const dentistId = this.selectedDentistId();
    const start = this.slotOptions().find((slot) => slot.value === this.selectedStart());
    if (!dentistId || !start || this.busy()) return;
    const selectedDuration = this.duration().trim() ? Number(this.duration()) : null;
    if (selectedDuration !== null && (!Number.isInteger(selectedDuration) || selectedDuration <= 0)) {
      this.error.set('La duración debe ser un número entero positivo de minutos.'); return;
    }
    const dateTimeOffset = this.localDateTimeOffset(start.value, this.settings()?.timeZoneId ?? 'UTC');
    this.busy.set(true); this.error.set('');
    const editing = this.editingAppointment();
    const call = editing
      ? this.api.reschedule(editing.appointmentId, { dentistId, startDateTime: dateTimeOffset, durationMinutes: selectedDuration, notes: this.notes() || null })
      : this.api.create({ patientId: this.selectedPatientId(), dentistId, startDateTime: dateTimeOffset, durationMinutes: selectedDuration, notes: this.notes() || null });
    call.subscribe({
      next: () => { this.busy.set(false); this.formVisible.set(false); this.message.set(editing ? 'Turno reprogramado.' : 'Turno creado.'); this.loadAppointments(); },
      error: (error: HttpErrorResponse) => { this.busy.set(false); this.error.set(this.errorMessage(error)); },
    });
  }

  protected cancel(appointment: Appointment): void {
    if (window.confirm(`¿Cancelar el turno de ${appointment.patientFirstName} ${appointment.patientLastName}?`)) this.mutate(appointment, 'cancel');
  }
  protected confirm(appointment: Appointment): void { this.mutate(appointment, 'confirm'); }
  protected complete(appointment: Appointment): void { this.mutate(appointment, 'complete'); }
  protected canReschedule(appointment: Appointment): boolean {
    return this.auth.hasPermission('appointments.update') && ['Scheduled', 'Confirmed'].includes(appointment.status);
  }
  protected canCancel(appointment: Appointment): boolean {
    return this.auth.hasPermission('appointments.cancel') && ['Scheduled', 'Confirmed'].includes(appointment.status);
  }
  protected canConfirm(appointment: Appointment): boolean {
    return this.auth.hasPermission('appointments.confirm') && appointment.status === 'Scheduled';
  }
  protected canComplete(appointment: Appointment): boolean {
    return this.auth.hasPermission('appointments.complete') && ['Scheduled', 'Confirmed'].includes(appointment.status);
  }
  private mutate(appointment: Appointment, operation: 'cancel' | 'confirm' | 'complete'): void {
    if (this.busy()) return;
    this.busy.set(true); this.error.set('');
    const request = operation === 'cancel' ? this.api.cancel(appointment.appointmentId)
      : operation === 'confirm' ? this.api.confirm(appointment.appointmentId)
      : this.api.complete(appointment.appointmentId);
    request.subscribe({
      next: () => { this.busy.set(false); this.message.set('El estado del turno se actualizó.'); this.loadAppointments(); },
      error: (error: HttpErrorResponse) => { this.busy.set(false); this.error.set(this.errorMessage(error)); },
    });
  }

  protected openScheduleEditor(): void {
    if (!this.canScheduleManage()) return;
    this.scheduleVisible.set(true); this.scheduleError.set('');
    this.api.schedule().subscribe({
      next: (schedule) => this.loadScheduleForm(schedule),
      error: (error: HttpErrorResponse) => {
        if (error.status === 404) this.loadScheduleForm(null);
        else this.scheduleError.set(this.errorMessage(error));
      },
    });
  }
  protected addWeeklyInterval(): void { this.weeklyIntervals.update((items) => [...items, { dayOfWeek: '', start: '', end: '' }]); }
  protected removeWeeklyInterval(index: number): void { this.weeklyIntervals.update((items) => items.filter((_, i) => i !== index)); }
  protected updateWeeklyInterval(index: number, field: 'dayOfWeek' | 'start' | 'end', value: string): void {
    this.weeklyIntervals.update((items) => items.map((item, i) => i === index ? { ...item, [field]: value } : item));
  }
  protected removeExceptionInterval(index: number): void {
    this.exceptionIntervals.update((items) => items.filter((_, i) => i !== index));
  }
  protected exceptionLabel(exception: ClinicDateException): string {
    return exception.openingIntervals.length
      ? exception.openingIntervals.map((item) => `${item.start}–${item.end}`).join(', ')
      : 'Cerrado';
  }
  protected addExceptionInterval(): void {
    if (!this.exceptionStart() || !this.exceptionEnd()) return;
    this.exceptionIntervals.update((items) => [...items, { start: this.exceptionStart(), end: this.exceptionEnd() }]);
    this.exceptionStart.set(''); this.exceptionEnd.set('');
  }
  protected saveSchedule(): void {
    if (!this.canScheduleManage() || this.busy()) return;
    const request: ConfigureClinicSchedule = {
      timeZoneId: this.timeZoneInput().trim(),
      defaultAppointmentDurationMinutes: Number(this.defaultDurationInput()),
      startIncrementMinutes: Number(this.incrementInput()),
      weeklyIntervals: this.weeklyIntervals().map((interval) => ({ ...interval })),
    };
    if (!request.timeZoneId || !Number.isInteger(request.defaultAppointmentDurationMinutes) || request.defaultAppointmentDurationMinutes <= 0 || !Number.isInteger(request.startIncrementMinutes) || request.startIncrementMinutes <= 0 || request.weeklyIntervals.some((item) => !item.dayOfWeek || !item.start || !item.end)) {
      this.scheduleError.set('Completá una zona horaria IANA, duración, incremento y franjas válidas.'); return;
    }
    this.busy.set(true); this.scheduleError.set('');
    const save = this.schedule() ? this.api.updateSchedule(request) : this.api.createSchedule(request);
    save.subscribe({ next: (schedule) => { this.busy.set(false); this.loadScheduleForm(schedule); this.scheduleMessage.set('Configuración guardada.'); this.settings.set({ timeZoneId: schedule.timeZoneId, defaultAppointmentDurationMinutes: schedule.defaultAppointmentDurationMinutes, startIncrementMinutes: schedule.startIncrementMinutes }); this.loadAppointments(); }, error: (error: HttpErrorResponse) => { this.busy.set(false); this.scheduleError.set(this.errorMessage(error)); } });
  }
  protected saveException(): void {
    if (!this.selectedExceptionDate() || this.busy()) return;
    this.busy.set(true); this.scheduleError.set('');
    const exception: ClinicDateException = { date: this.selectedExceptionDate(), openingIntervals: this.exceptionIntervals() };
    this.api.setException(exception).subscribe({ next: (schedule) => { this.busy.set(false); this.loadScheduleForm(schedule); this.scheduleMessage.set('Excepción guardada.'); this.loadAppointments(); }, error: (error: HttpErrorResponse) => { this.busy.set(false); this.scheduleError.set(this.errorMessage(error)); } });
  }
  protected removeException(date: string): void {
    this.api.removeException(date).subscribe({ next: (schedule) => { this.loadScheduleForm(schedule); this.scheduleMessage.set('Excepción eliminada.'); this.loadAppointments(); }, error: (error: HttpErrorResponse) => this.scheduleError.set(this.errorMessage(error)) });
  }
  protected selectException(exception: ClinicDateException): void {
    this.selectedExceptionDate.set(exception.date);
    this.exceptionIntervals.set(exception.openingIntervals.map((item) => ({ ...item })));
  }
  protected closeScheduleEditor(): void { this.scheduleVisible.set(false); }
  private loadScheduleForm(schedule: ClinicSchedule | null): void {
    this.schedule.set(schedule);
    this.timeZoneInput.set(schedule?.timeZoneId ?? '');
    this.defaultDurationInput.set(schedule ? String(schedule.defaultAppointmentDurationMinutes) : '');
    this.incrementInput.set(schedule ? String(schedule.startIncrementMinutes) : '');
    this.weeklyIntervals.set(schedule?.weeklyIntervals.map((item) => ({ ...item })) ?? []);
    this.selectedExceptionDate.set(''); this.exceptionIntervals.set([]);
  }

  private queryFrom(): string { return new Date(Date.parse(`${this.weekStart()}T00:00:00Z`) - 2 * 86_400_000).toISOString(); }
  private queryTo(): string { return new Date(Date.parse(`${this.addDays(this.weekStart(), 7)}T00:00:00Z`) + 2 * 86_400_000).toISOString(); }
  private todayInZone(zone: string): string {
    const parts = new Intl.DateTimeFormat('en-CA', { timeZone: zone, year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date());
    return `${this.part(parts, 'year')}-${this.part(parts, 'month')}-${this.part(parts, 'day')}`;
  }
  private dateInZone(value: string, zone: string): string {
    const parts = new Intl.DateTimeFormat('en-CA', { timeZone: zone, year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date(value));
    return `${this.part(parts, 'year')}-${this.part(parts, 'month')}-${this.part(parts, 'day')}`;
  }
  private part(parts: Intl.DateTimeFormatPart[], type: string): string { return parts.find((item) => item.type === type)?.value ?? ''; }
  private mondayOf(date: string): string {
    const value = new Date(`${date}T00:00:00Z`);
    const weekday = (value.getUTCDay() + 6) % 7;
    value.setUTCDate(value.getUTCDate() - weekday);
    return value.toISOString().slice(0, 10);
  }
  private addDays(date: string, days: number): string {
    const value = new Date(`${date}T00:00:00Z`); value.setUTCDate(value.getUTCDate() + days);
    return value.toISOString().slice(0, 10);
  }
  private localDateTimeOffset(value: string, zone: string): string {
    const date = new Date(value);
    const parts = new Intl.DateTimeFormat('en-CA', { timeZone: zone, year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit', hourCycle: 'h23' }).formatToParts(date);
    const local = `${this.part(parts, 'year')}-${this.part(parts, 'month')}-${this.part(parts, 'day')}T${this.part(parts, 'hour')}:${this.part(parts, 'minute')}:${this.part(parts, 'second')}`;
    const localAsUtc = Date.UTC(Number(local.slice(0, 4)), Number(local.slice(5, 7)) - 1, Number(local.slice(8, 10)), Number(local.slice(11, 13)), Number(local.slice(14, 16)), Number(local.slice(17, 19)));
    const offsetMinutes = Math.round((localAsUtc - date.getTime()) / 60_000);
    const sign = offsetMinutes >= 0 ? '+' : '-'; const absolute = Math.abs(offsetMinutes);
    return `${local}${sign}${String(Math.floor(absolute / 60)).padStart(2, '0')}:${String(absolute % 60).padStart(2, '0')}`;
  }
  private isAmbiguousWallTime(instant: Date, zone: string): boolean {
    const target = this.localDateTimeOffset(instant.toISOString(), zone).slice(0, 19);
    for (let minutes = -180; minutes <= 180; minutes += 15) {
      if (minutes !== 0 && this.localDateTimeOffset(new Date(instant.getTime() + minutes * 60_000).toISOString(), zone).slice(0, 19) === target) return true;
    }
    return false;
  }
  private errorMessage(error: HttpErrorResponse): string {
    if (error.status === 0) return 'No se pudo conectar con la API.';
    if (error.status === 401) return 'La sesión venció. Iniciá sesión nuevamente.';
    if (error.status === 403) return 'No tenés permiso para realizar esta acción.';
    if (error.status === 404) return 'No se encontró el recurso solicitado.';
    if (error.status === 409) return 'El horario ya no está disponible o existe un conflicto de estado. Actualizá la agenda.';
    if (error.status === 400) return 'Revisá los datos y el horario seleccionado.';
    return 'Ocurrió un error al comunicarse con DentalPro.';
  }
}
