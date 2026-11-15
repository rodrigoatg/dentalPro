export interface Appointment {
  appointmentId: string;
  patientId: string;
  patientFirstName: string;
  patientLastName: string;
  dentistId: string;
  dentistFirstName: string;
  dentistLastName: string;
  startDateTime: string;
  endDateTime: string;
  durationMinutes: number;
  status: 'Scheduled' | 'Confirmed' | 'Completed' | 'Cancelled' | 'Rescheduled' | string;
  notes: string | null;
  rescheduledFromAppointmentId: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface DentistOption { id: string; firstName: string; lastName: string; }
export interface AppointmentInterval { start: string; end: string; }
export interface Availability {
  availableIntervals: AppointmentInterval[];
  defaultAppointmentDurationMinutes: number;
  startIncrementMinutes: number;
}
export interface AppointmentRequest {
  patientId: string;
  dentistId: string;
  startDateTime: string;
  durationMinutes?: number | null;
  notes?: string | null;
}
export interface RescheduleRequest {
  dentistId: string;
  startDateTime: string;
  durationMinutes?: number | null;
  notes?: string | null;
}
export interface WeeklyOpeningInterval {
  dayOfWeek: string;
  start: string;
  end: string;
}
export interface ClinicOpeningInterval { start: string; end: string; }
export interface ClinicDateException { date: string; openingIntervals: ClinicOpeningInterval[]; }
export interface ClinicSchedule {
  id: string;
  timeZoneId: string;
  defaultAppointmentDurationMinutes: number;
  startIncrementMinutes: number;
  weeklyIntervals: WeeklyOpeningInterval[];
  dateExceptions: ClinicDateException[];
}
export interface ClinicScheduleSettings {
  timeZoneId: string;
  defaultAppointmentDurationMinutes: number;
  startIncrementMinutes: number;
}
export interface ConfigureClinicSchedule {
  timeZoneId: string;
  defaultAppointmentDurationMinutes: number;
  startIncrementMinutes: number;
  weeklyIntervals: WeeklyOpeningInterval[];
}

