import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../core/auth/auth.service';
import { AppointmentsApiService } from '../core/appointments/appointments-api.service';
import { Appointment, ClinicScheduleSettings } from '../core/appointments/appointment.models';
import { PatientsApiService } from '../core/patients/patients-api.service';

interface DashboardDay {
  date: string;
  label: string;
  appointments: Appointment[];
}

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css',
})
export class Dashboard implements OnInit {
  private readonly patientsApi = inject(PatientsApiService);
  private readonly appointmentsApi = inject(AppointmentsApiService);
  private readonly auth = inject(AuthService);

  protected readonly isAdministrator = computed(() => this.auth.hasPermission('users.view'));
  protected readonly userFirstName = computed(() => this.auth.user()?.firstName ?? '');
  protected readonly canViewPatients = () => this.auth.hasPermission('patients.view');
  protected readonly canCreatePatients = () => this.auth.hasPermission('patients.create');
  protected readonly canViewAppointments = () => this.auth.hasPermission('appointments.view');
  protected readonly canViewGeneralAgenda = () => this.auth.hasPermission('appointments.view.all');
  protected readonly isDentist = computed(() =>
    this.canViewAppointments() && !this.canViewGeneralAgenda() &&
    this.auth.hasPermission('clinical.records.view'));
  protected readonly isReceptionist = computed(() =>
    this.canViewAppointments() && this.canViewGeneralAgenda() && !this.isAdministrator());
  protected readonly canViewClinicalRecords = () => this.auth.hasPermission('clinical.records.view');
  protected readonly canConfirm = () => this.auth.hasPermission('appointments.confirm');
  protected readonly canComplete = () => this.auth.hasPermission('appointments.complete');

  protected readonly patientsCount = signal<number | null>(null);
  protected readonly dentistsCount = signal<number | null>(null);
  protected readonly appointments = signal<Appointment[]>([]);
  protected readonly timeZone = signal<string | null>(null);
  protected readonly scheduleLoading = signal(false);
  protected readonly appointmentsLoading = signal(false);
  protected readonly patientsLoading = signal(false);
  protected readonly dentistsLoading = signal(false);
  protected readonly scheduleError = signal('');
  protected readonly appointmentsError = signal('');
  protected readonly patientsError = signal('');
  protected readonly dentistsError = signal('');
  protected readonly actionError = signal('');
  protected readonly notice = signal('');
  protected readonly busyAppointmentId = signal<string | null>(null);
  protected readonly clinicToday = signal('');
  protected readonly weekStart = signal('');
  protected readonly selectedDay = signal('');
  private readonly now = signal(Date.now());

  protected readonly periodAppointments = computed(() => {
    const zone = this.timeZone();
    const start = this.isDentist() ? this.selectedDay() : this.weekStart();
    if (!zone || !start) return [];
    const days = this.isDentist() ? 1 : 7;
    const end = this.addDays(start, days);
    return this.appointments()
      .filter((appointment) => {
        const localDate = this.dateInZone(appointment.startDateTime, zone);
        return localDate >= start && localDate < end;
      })
      .sort((left, right) => left.startDateTime.localeCompare(right.startDateTime));
  });

  protected readonly weekDays = computed<DashboardDay[]>(() => {
    const zone = this.timeZone();
    const start = this.weekStart();
    if (!zone || !start) return [];
    return Array.from({ length: 7 }, (_, index) => {
      const date = this.addDays(start, index);
      const dayAppointments = this.periodAppointments().filter(
        (appointment) => this.dateInZone(appointment.startDateTime, zone) === date,
      );
      return { date, label: this.dayLabel(date), appointments: dayAppointments };
    });
  });

  protected readonly pendingAppointments = computed(() =>
    this.periodAppointments().filter((appointment) =>
      appointment.status === 'Scheduled' || appointment.status === 'Confirmed'));
  protected readonly upcomingAppointments = computed(() =>
    this.pendingAppointments()
      .filter((appointment) => Date.parse(appointment.startDateTime) >= this.now())
      .slice(0, 6));
  protected readonly nextAppointment = computed(() => this.upcomingAppointments()[0] ?? null);
  protected readonly scheduledCount = computed(() =>
    this.periodAppointments().filter((appointment) => appointment.status === 'Scheduled').length);
  protected readonly confirmedCount = computed(() =>
    this.periodAppointments().filter((appointment) => appointment.status === 'Confirmed').length);
  protected readonly cancelledCount = computed(() =>
    this.periodAppointments().filter((appointment) => appointment.status === 'Cancelled').length);
  protected readonly completedCount = computed(() =>
    this.periodAppointments().filter((appointment) => appointment.status === 'Completed').length);
  protected readonly activityAppointments = computed(() =>
    [...this.periodAppointments()]
      .sort((left, right) => right.updatedAt.localeCompare(left.updatedAt))
      .slice(0, 5));
  protected readonly periodLabel = computed(() => {
    const start = this.isDentist() ? this.selectedDay() : this.weekStart();
    if (!start) return '';
    return this.isDentist() ? this.dayLabel(start) : this.weekLabel(start);
  });

  ngOnInit(): void {
    if (this.isAdministrator() && this.canViewPatients()) this.loadPatientCount();
    if (this.isAdministrator() && this.auth.hasPermission('appointments.create')) this.loadDentistCount();
    if (this.canViewAppointments()) this.loadScheduleSettings();
  }

  protected previousWeek(): void {
    if (!this.weekStart()) return;
    this.weekStart.set(this.addDays(this.weekStart(), -7));
    this.loadAppointments();
  }

  protected nextWeek(): void {
    if (!this.weekStart()) return;
    this.weekStart.set(this.addDays(this.weekStart(), 7));
    this.loadAppointments();
  }

  protected previousDay(): void {
    if (!this.selectedDay()) return;
    this.selectedDay.set(this.addDays(this.selectedDay(), -1));
    this.loadAppointments();
  }

  protected nextDay(): void {
    if (!this.selectedDay()) return;
    this.selectedDay.set(this.addDays(this.selectedDay(), 1));
    this.loadAppointments();
  }

  protected goToToday(): void {
    if (this.isDentist()) this.selectedDay.set(this.clinicToday());
    else this.weekStart.set(this.mondayOf(this.clinicToday()));
    this.loadAppointments();
  }

  protected refresh(): void {
    this.now.set(Date.now());
    if (this.canViewAppointments() && this.timeZone()) this.loadAppointments();
    if (this.isAdministrator()) {
      this.loadPatientCount();
      this.loadDentistCount();
    }
  }

  protected timeLabel(value: string): string {
    const zone = this.timeZone();
    if (!zone) return '—';
    return new Intl.DateTimeFormat('es-AR', { timeZone: zone, hour: '2-digit', minute: '2-digit' })
      .format(new Date(value));
  }

  protected dateTimeLabel(value: string): string {
    const zone = this.timeZone();
    if (!zone) return '—';
    return new Intl.DateTimeFormat('es-AR', {
      timeZone: zone, day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit',
    }).format(new Date(value));
  }

  protected statusLabel(status: string): string {
    const labels: Record<string, string> = {
      Scheduled: 'Pendiente', Confirmed: 'Confirmado', Completed: 'Completado',
      Cancelled: 'Cancelado', Rescheduled: 'Reprogramado',
    };
    return labels[status] ?? status;
  }

  protected statusClass(status: string): string {
    return `status-${status.toLocaleLowerCase('en-US')}`;
  }

  protected canConfirmAppointment(appointment: Appointment): boolean {
    return this.canConfirm() && appointment.status === 'Scheduled';
  }

  protected canCompleteAppointment(appointment: Appointment): boolean {
    return this.canComplete() && (appointment.status === 'Scheduled' || appointment.status === 'Confirmed');
  }

  protected confirmAppointment(appointment: Appointment): void {
    if (!this.canConfirmAppointment(appointment) || this.busyAppointmentId()) return;
    this.updateAppointment(appointment, () => this.appointmentsApi.confirm(appointment.appointmentId), 'Turno confirmado.');
  }

  protected completeAppointment(appointment: Appointment): void {
    if (!this.canCompleteAppointment(appointment) || this.busyAppointmentId()) return;
    this.updateAppointment(appointment, () => this.appointmentsApi.complete(appointment.appointmentId), 'Turno completado.');
  }

  private loadPatientCount(): void {
    this.patientsLoading.set(true);
    this.patientsError.set('');
    this.patientsApi.count().subscribe({
      next: (result) => { this.patientsCount.set(result.count); this.patientsLoading.set(false); },
      error: (error: HttpErrorResponse) => {
        this.patientsError.set(this.errorMessage(error)); this.patientsLoading.set(false);
      },
    });
  }

  private loadDentistCount(): void {
    this.dentistsLoading.set(true);
    this.dentistsError.set('');
    this.appointmentsApi.dentists().subscribe({
      next: (dentists) => { this.dentistsCount.set(dentists.length); this.dentistsLoading.set(false); },
      error: (error: HttpErrorResponse) => {
        this.dentistsError.set(this.errorMessage(error)); this.dentistsLoading.set(false);
      },
    });
  }

  private loadScheduleSettings(): void {
    this.scheduleLoading.set(true);
    this.scheduleError.set('');
    this.appointmentsApi.scheduleSettings().subscribe({
      next: (settings) => {
        this.applyScheduleSettings(settings);
        this.scheduleLoading.set(false);
        this.loadAppointments();
      },
      error: (error: HttpErrorResponse) => {
        this.scheduleError.set(error.status === 409
          ? 'La clínica todavía no tiene horarios configurados.'
          : this.errorMessage(error));
        this.scheduleLoading.set(false);
      },
    });
  }

  private applyScheduleSettings(settings: ClinicScheduleSettings): void {
    this.timeZone.set(settings.timeZoneId);
    const today = this.todayInZone(settings.timeZoneId);
    this.clinicToday.set(today);
    this.selectedDay.set(today);
    this.weekStart.set(this.mondayOf(today));
  }

  private loadAppointments(): void {
    const zone = this.timeZone();
    const start = this.isDentist() ? this.selectedDay() : this.weekStart();
    if (!zone || !start) return;
    const end = this.addDays(start, this.isDentist() ? 1 : 7);
    const from = new Date(Date.parse(`${start}T00:00:00Z`) - 2 * 86_400_000).toISOString();
    const to = new Date(Date.parse(`${end}T00:00:00Z`) + 2 * 86_400_000).toISOString();
    this.appointmentsLoading.set(true);
    this.appointmentsError.set('');
    // No dentistId is sent: the API applies the authenticated user's own or general scope.
    this.appointmentsApi.list(from, to).subscribe({
      next: (items) => { this.appointments.set(items); this.appointmentsLoading.set(false); },
      error: (error: HttpErrorResponse) => {
        this.appointmentsError.set(this.errorMessage(error)); this.appointmentsLoading.set(false);
      },
    });
  }

  private updateAppointment(
    appointment: Appointment,
    action: () => ReturnType<AppointmentsApiService['confirm']>,
    success: string,
  ): void {
    this.busyAppointmentId.set(appointment.appointmentId);
    this.actionError.set('');
    this.notice.set('');
    action().subscribe({
      next: () => {
        this.busyAppointmentId.set(null);
        this.notice.set(success);
        this.now.set(Date.now());
        this.loadAppointments();
      },
      error: (error: HttpErrorResponse) => {
        this.busyAppointmentId.set(null);
        this.actionError.set(this.errorMessage(error));
      },
    });
  }

  private todayInZone(zone: string): string {
    const parts = new Intl.DateTimeFormat('en-CA', {
      timeZone: zone, year: 'numeric', month: '2-digit', day: '2-digit',
    }).formatToParts(new Date());
    return `${this.part(parts, 'year')}-${this.part(parts, 'month')}-${this.part(parts, 'day')}`;
  }

  private dateInZone(value: string, zone: string): string {
    const parts = new Intl.DateTimeFormat('en-CA', {
      timeZone: zone, year: 'numeric', month: '2-digit', day: '2-digit',
    }).formatToParts(new Date(value));
    return `${this.part(parts, 'year')}-${this.part(parts, 'month')}-${this.part(parts, 'day')}`;
  }

  protected dayLabel(date: string): string {
    return new Intl.DateTimeFormat('es-AR', { timeZone: 'UTC', weekday: 'short', day: 'numeric', month: 'short' })
      .format(new Date(`${date}T12:00:00Z`));
  }

  private weekLabel(start: string): string {
    const end = this.addDays(start, 6);
    const first = new Date(`${start}T12:00:00Z`);
    const last = new Date(`${end}T12:00:00Z`);
    const month = new Intl.DateTimeFormat('es-AR', { timeZone: 'UTC', month: 'long', year: 'numeric' });
    const firstMonth = month.format(first);
    return firstMonth === month.format(last)
      ? `${first.getUTCDate()}–${last.getUTCDate()} de ${firstMonth}`
      : `${this.dayLabel(start)} – ${this.dayLabel(end)}`;
  }

  private part(parts: Intl.DateTimeFormatPart[], type: string): string {
    return parts.find((item) => item.type === type)?.value ?? '';
  }

  private mondayOf(date: string): string {
    const value = new Date(`${date}T00:00:00Z`);
    const weekday = (value.getUTCDay() + 6) % 7;
    value.setUTCDate(value.getUTCDate() - weekday);
    return value.toISOString().slice(0, 10);
  }

  private addDays(date: string, days: number): string {
    const value = new Date(`${date}T00:00:00Z`);
    value.setUTCDate(value.getUTCDate() + days);
    return value.toISOString().slice(0, 10);
  }

  private errorMessage(error: HttpErrorResponse): string {
    if (error.status === 0) return 'No se pudo conectar con la API.';
    if (error.status === 401) return 'La sesión venció. Iniciá sesión nuevamente.';
    if (error.status === 403) return 'Tu cuenta no tiene permiso para consultar estos datos.';
    if (error.status === 409) return 'No se pudo cargar la agenda por su configuración actual.';
    return 'No se pudo cargar esta información.';
  }
}
