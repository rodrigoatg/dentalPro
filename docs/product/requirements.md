# DentalPro — Requerimientos del Sistema

## 1. Descripción

DentalPro es una aplicación web destinada a digitalizar y centralizar
la gestión de un consultorio odontológico.

El sistema busca reemplazar procesos actualmente realizados mediante
agendas en papel, planillas de cálculo e historias clínicas impresas,
permitiendo gestionar información clínica y administrativa desde una
plataforma centralizada.

La solución se plantea como una aplicación web accesible desde
computadoras, tablets y dispositivos móviles mediante un navegador.

## 2. Objetivo

El objetivo principal es mejorar la eficiencia operativa del consultorio,
reducir errores derivados de procesos manuales y facilitar el acceso
seguro a la información clínica y administrativa.

## 3. Actores

### Recepcionista / Asistente

Responsabilidades:

- Registrar pacientes.
- Consultar y modificar información de pacientes.
- Gestionar turnos.
- Consultar la agenda del consultorio.

### Odontólogo/a

Responsabilidades:

- Consultar historias clínicas.
- Registrar tratamientos.
- Registrar observaciones clínicas.
- Actualizar información clínica.
- Consultar información asociada al paciente.

### Administrativo / Contador

Responsabilidades:

- Consultar información financiera.
- Registrar ingresos y cuentas por cobrar.
- Generar reportes.

### Administrador del sistema

Responsabilidades:

- Gestionar usuarios.
- Gestionar roles.
- Administrar permisos.
- Configurar horarios de atención.

## 4. Requerimientos Funcionales

Los requisitos están marcados como `[MVP]` o `[ROADMAP]`. La presencia de
un requisito en este documento no cambia su clasificación de alcance.

## MVP Scope

### FR-001 [MVP] — Gestión de pacientes

El sistema deberá permitir registrar, modificar, dar de baja y consultar
los datos personales y de contacto de los pacientes.

La búsqueda deberá permitir localizar pacientes mediante nombre, DNI
o número de historia clínica.

### FR-002 [MVP] — Gestión de turnos y agenda

El sistema deberá permitir crear, modificar, reprogramar y cancelar
turnos.

Antes de confirmar un turno, el sistema deberá validar la disponibilidad
horaria del odontólogo y evitar superposiciones.

### FR-003 [MVP] — Historia clínica y ClinicalEntry

El sistema deberá permitir registrar y consultar información clínica
asociada a cada paciente.

La historia clínica deberá contemplar ClinicalEntry para registrar eventos,
observaciones e intervenciones clínicas. Cada ClinicalEntry deberá
versionarse; las versiones anteriores deberán conservarse y permanecer
accesibles. El mecanismo concreto de persistencia se definirá durante el
diseño de implementación.

La historia clínica deberá asociar los tratamientos y permitir actualizar
el odontograma inicial simplificado. El odontograma MVP utilizará dentición
permanente, notación FDI y estados dentales básicos, sin detalle de
superficies. Los cambios históricos deberán ser trazables.

### FR-004 [MVP] — Tratamientos

El sistema deberá permitir registrar tratamientos planificados y realizados
asociados a la historia clínica y a uno o más dientes. Deberá admitir
tratamientos parciales, con distintos estados por diente. Completar un diente
posteriormente no deberá reescribir el estado histórico del tratamiento
original; el nuevo tratamiento deberá registrarse como una operación
posterior cuando corresponda.

### FR-005 [MVP] — Autenticación, autorización y RBAC

El sistema deberá autenticar usuarios con credenciales individuales y
restringir las funcionalidades y datos según permisos configurables por rol.
Un usuario podrá tener varios roles y sus permisos efectivos serán la unión
de los permisos asignados a esos roles. No existirán permisos individuales
por usuario. La API deberá comprobar las asignaciones actuales en el servidor
en cada solicitud; los cambios deberán regir desde la siguiente solicitud,
sin un nuevo inicio de sesión.

### FR-006 [MVP] — Administración de usuarios, roles y horarios

Solamente Administrator podrá crear usuarios, cambiar su estado, asignar o
quitar roles y gestionar permisos de roles. Los roles iniciales serán
Administrator, Dentist y Receptionist. Los permisos administrativos
protegidos no podrán quitarse de Administrator mediante la API. El sistema
deberá impedir que el último Administrator activo pierda ese rol o sea
desactivado. La configuración de horarios de atención se gestionará como
parte de la agenda y fuera del catálogo RBAC hasta que exista el endpoint
correspondiente.

### FR-007 [MVP] — Auditoría de operaciones relevantes

El sistema deberá registrar quién realizó y cuándo se realizaron las
operaciones relevantes, incluyendo creación, actualización o desactivación
de pacientes; creación, actualización y cancelación de turnos; cambios en
historias clínicas; creación y actualización de ClinicalEntry y tratamientos;
cambios en el odontograma; y eventos de autenticación. El MVP no requiere
registrar cada operación de lectura.

## Future Roadmap

### FR-020 [ROADMAP] — Carga de bioimágenes

El sistema deberá permitir cargar, asociar y visualizar radiografías,
fotografías, tomografías y otros documentos clínicos vinculados con
la historia clínica del paciente.

### FR-021 [ROADMAP] — Exportación de historia clínica

El sistema deberá permitir generar una versión exportable de la historia
clínica de un paciente para interconsultas o derivaciones.

### FR-022 [ROADMAP] — Gestión financiera

El sistema deberá permitir registrar ingresos y cuentas por cobrar,
así como consultar y filtrar movimientos financieros por período y
tipo de operación.

### FR-023 [ROADMAP] — Reportes financieros avanzados

El sistema deberá permitir generar y exportar reportes financieros
consolidados a partir de los movimientos registrados.

### FR-024 [ROADMAP] — Notificación de turnos

El sistema deberá permitir notificar al paciente ante la confirmación,
modificación o cancelación de un turno.

### FR-025 [ROADMAP] — Consentimiento informado

La gestión de consentimientos asociados a tratamientos o procedimientos
clínicos queda fuera del MVP.

### FR-026 [ROADMAP] — Integraciones externas

Las integraciones con sistemas externos de salud, FHIR y SNOMED CT quedan
fuera del MVP.

### FR-027 [ROADMAP] — Portal de pacientes

El acceso directo de pacientes a sus datos clínicos queda fuera del MVP.

## 5. Requerimientos No Funcionales

### RNF-01 — Seguridad

El sistema deberá utilizar autenticación mediante credenciales
individuales, almacenar contraseñas mediante hashing, aplicar autorización
en backend y control de acceso basado en roles, utilizar HTTPS y expirar
las sesiones. Estas medidas forman parte del MVP.

La autenticación de segundo factor (2FA), incluso para dispositivos no
reconocidos, pertenece al Future Roadmap y no es requisito del MVP.

Los tokens previamente emitidos deberán dejar de ser válidos cuando se
desactive una cuenta o cambien sus roles. La API deberá fallar de forma
cerrada si no puede verificar el estado actual del usuario. Los permisos
efectivos no se tomarán de claims del token y se consultarán desde las
asignaciones actuales de roles y permisos.

### RNF-02 — Disponibilidad

El sistema deberá alcanzar una disponibilidad mínima del 99,5 %
durante el horario laboral.

### RNF-03 — Rendimiento

Las operaciones estándar de consulta, alta y modificación deberán
responder en un tiempo máximo de 2 segundos bajo condiciones normales.

### RNF-04 — Escalabilidad

La arquitectura deberá permitir incorporar nuevos módulos y
funcionalidades sin requerir un rediseño estructural del sistema.

### RNF-05 — Usabilidad

La interfaz deberá ser intuitiva y permitir que usuarios sin
conocimientos técnicos avanzados puedan utilizar las funcionalidades
principales.

### RNF-06 — Compatibilidad

La aplicación deberá funcionar correctamente en versiones actuales
de Chrome, Firefox, Edge y Safari.

### RNF-07 — Portabilidad

La aplicación deberá poder utilizarse desde computadoras,
tablets y dispositivos móviles mediante un navegador web.

### RNF-08 — Respaldo

La información deberá contar con mecanismos de backup automático
para permitir su recuperación ante fallas.

### RNF-09 — Cumplimiento normativo

El tratamiento de datos personales y de salud deberá contemplar
los requisitos establecidos por la Ley 25.326 de Protección de
Datos Personales.

## 6. Reglas de negocio

### RN-01 — Unicidad del paciente

No deberá existir más de un paciente registrado con el mismo DNI.

### RN-02 — Disponibilidad de turnos

No podrá haber turnos superpuestos para un mismo odontólogo ni para un
mismo paciente. Los intervalos son semiabiertos `[inicio, fin)`, por lo que
dos turnos consecutivos pueden compartir exactamente el límite entre fin e
inicio.

Los estados `Scheduled`, `Confirmed` y `Completed` bloquean el intervalo.
`Cancelled` y `Rescheduled` se conservan como historial, pero no bloquean
disponibilidad. La disponibilidad se comprueba al consultar y se vuelve a
validar en la transacción de creación o reprogramación; la persistencia debe
proteger la regla también ante solicitudes concurrentes.

### Decisiones aprobadas de la agenda (Etapa 0)

- Administrator y Receptionist pueden consultar y administrar la agenda
  general. Dentist solo consulta su propia agenda y puede confirmar y marcar
  como atendidos sus propios turnos. Dentist no crea, reprograma ni cancela
  turnos en la primera versión.
- Se reutilizan los estados `Scheduled`, `Confirmed`, `Completed`,
  `Cancelled` y `Rescheduled`. Las transiciones permitidas son:
  `Scheduled` -> `Confirmed`, `Completed`, `Cancelled` o `Rescheduled`; y
  `Confirmed` -> `Completed`, `Cancelled` o `Rescheduled`. Los otros estados
  son terminales.
- Reprogramar conserva el turno previo como `Rescheduled` y crea un nuevo
  turno que apunta al inmediatamente anterior. La cadena se reconstruye
  siguiendo esas referencias. La actualización del anterior, la creación del
  nuevo y sus eventos de auditoría son atómicos.
- La agenda se valida contra una configuración de horarios de clínica:
  franjas semanales, duración predeterminada y excepciones por fecha para
  horarios especiales, feriados y cierres. No hay horarios individuales por
  odontólogo en el alcance inicial.
- La configuración requiere un identificador de zona horaria IANA. No se
  fija una zona en los requisitos del producto; debe configurarse antes de
  habilitar la agenda. Cambiar horarios no altera turnos existentes; los
  turnos afectados se revisan y resuelven explícitamente.
- Para los permisos y la matriz inicial por rol, ver
  `docs/domain/domain-model.md` y
  `docs/architecture/decisions/ADR-004-appointment-authorization-scope.md`.
  Las asignaciones de agenda para
  Administrator serán explícitas en el catálogo/bootstrap; no se derivan del
  nombre de rol ni alteran la política general de permisos.

### Cuestiones operativas previas a la implementación

- La clínica debe proporcionar su identificador de zona horaria IANA y el
  valor inicial de duración predeterminada. La agenda no se habilitará con
  valores implícitos del servidor o del navegador.
- Debe definirse el incremento de inicio de los turnos disponibles. La
  duración predeterminada fija la longitud del turno, pero por sí sola no
  determina cada hora de inicio ofrecida.
- Debe definirse si se permiten altas o reprogramaciones en el pasado y qué
  hacer con turnos futuros cuando se desactiva un paciente o se deshabilita un
  odontólogo o su rol Dentist.
- La UI necesita búsquedas mínimas de pacientes activos y odontólogos
  activos. El listado actual de pacientes no busca ni pagina, y el endpoint
  administrativo de usuarios requiere `users.view`; no debe suponerse que
  Receptionist tendrá ese permiso.
- Deben definirse el alcance de lectura de auditoría y su período de
  retención conforme a la política operativa y regulatoria aplicable.
- La implementación debe acordar la respuesta HTTP de conflictos de
  disponibilidad y comprobar en PostgreSQL 17 la extensión necesaria para
  las restricciones de exclusión.

### RN-03 — Historia clínica

Cada paciente deberá disponer de una historia clínica asociada.

### RN-04 — Control de acceso

Un usuario solamente podrá acceder a las funcionalidades y datos
correspondientes a los permisos de su rol.

### RN-05 — Auditoría

Las operaciones relevantes sobre información sensible deberán
poder ser trazadas hasta el usuario que las realizó.

### RN-06 — Integridad clínica

Los tratamientos y observaciones deberán quedar asociados al
paciente y a su historia clínica correspondiente.

## 7. Alcance funcional

### MVP Scope

El MVP comprende FR-001 a FR-007, sujetos a los requisitos no funcionales
y reglas de negocio aplicables.

### Future Roadmap

El roadmap comprende FR-020 a FR-027. La inclusión de una capacidad en los
requisitos no implica que forme parte de la primera implementación.

## 8. Restricciones

El sistema será inicialmente una aplicación web.

Las funcionalidades orientadas a pacientes externos al consultorio, las
integraciones con sistemas externos de salud, las imágenes clínicas, la
gestión financiera y los reportes financieros avanzados no forman parte del
MVP.
