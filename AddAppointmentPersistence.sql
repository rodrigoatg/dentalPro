START TRANSACTION;
CREATE TABLE "Appointments" (
    "Id" uuid NOT NULL,
    "PatientId" uuid NOT NULL,
    "DentistId" uuid NOT NULL,
    "StartDateTime" timestamptz NOT NULL,
    "EndDateTime" timestamptz NOT NULL,
    "Status" text NOT NULL,
    "Notes" text,
    "RescheduledFromAppointmentId" uuid,
    "CreatedAt" timestamptz NOT NULL,
    "UpdatedAt" timestamptz NOT NULL,
    CONSTRAINT "PK_Appointments" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_Appointments_EndAfterStart" CHECK ("StartDateTime" < "EndDateTime"),
    CONSTRAINT "CK_Appointments_RescheduledFrom_Valid" CHECK ("RescheduledFromAppointmentId" IS NULL OR ("RescheduledFromAppointmentId" <> '00000000-0000-0000-0000-000000000000' AND "RescheduledFromAppointmentId" <> "Id")),
    CONSTRAINT "CK_Appointments_Status" CHECK ("Status" IN ('Scheduled', 'Confirmed', 'Completed', 'Cancelled', 'Rescheduled')),
    CONSTRAINT "FK_Appointments_Appointments_RescheduledFromAppointmentId" FOREIGN KEY ("RescheduledFromAppointmentId") REFERENCES "Appointments" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Appointments_AspNetUsers_DentistId" FOREIGN KEY ("DentistId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Appointments_Patients_PatientId" FOREIGN KEY ("PatientId") REFERENCES "Patients" ("Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_Appointments_DentistId_StartDateTime" ON "Appointments" ("DentistId", "StartDateTime");

CREATE INDEX "IX_Appointments_PatientId_StartDateTime" ON "Appointments" ("PatientId", "StartDateTime");

CREATE UNIQUE INDEX "UX_Appointments_RescheduledFromAppointmentId" ON "Appointments" ("RescheduledFromAppointmentId") WHERE "RescheduledFromAppointmentId" IS NOT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261111005924_AddAppointmentPersistence', '10.0.12');

COMMIT;

