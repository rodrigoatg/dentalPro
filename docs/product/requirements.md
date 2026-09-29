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

### RF-01 — Gestión de pacientes

El sistema deberá permitir registrar, modificar, dar de baja y consultar
los datos personales y de contacto de los pacientes.

La búsqueda deberá permitir localizar pacientes mediante nombre, DNI
o número de historia clínica.

### RF-02 — Gestión de turnos

El sistema deberá permitir crear, modificar, reprogramar y cancelar
turnos.

Antes de confirmar un turno, el sistema deberá validar la disponibilidad
horaria del odontólogo y evitar superposiciones.

### RF-03 — Historia clínica digital

El sistema deberá permitir registrar y consultar información clínica
asociada a cada paciente.

La historia clínica deberá contemplar tratamientos realizados,
observaciones clínicas y actualización del odontograma.

### RF-04 — Carga de bioimágenes

El sistema deberá permitir cargar, asociar y visualizar radiografías,
fotografías, tomografías y otros documentos clínicos vinculados con
la historia clínica del paciente.

### RF-05 — Exportación de historia clínica

El sistema deberá permitir generar una versión exportable de la historia
clínica de un paciente para interconsultas o derivaciones.

### RF-06 — Gestión financiera

El sistema deberá permitir registrar ingresos y cuentas por cobrar,
así como consultar y filtrar movimientos financieros por período y
tipo de operación.

### RF-07 — Generación de reportes

El sistema deberá permitir generar y exportar reportes financieros
consolidados a partir de los movimientos registrados.

### RF-08 — Autenticación y control de acceso

El sistema deberá autenticar a los usuarios y restringir las
funcionalidades disponibles de acuerdo con el rol asignado.

El control de acceso deberá implementar un modelo basado en roles (RBAC).

### RF-09 — Notificación de turnos

El sistema deberá permitir notificar al paciente ante la confirmación,
modificación o cancelación de un turno.

### RF-10 — Auditoría de acciones

El sistema deberá registrar las acciones relevantes realizadas por
los usuarios, incluyendo quién realizó la acción y cuándo fue realizada.

### RF-11 — Administración de usuarios y horarios

El sistema deberá permitir gestionar las cuentas de usuario, sus roles
y permisos, además de configurar los horarios de atención disponibles
del consultorio.

## 5. Requerimientos No Funcionales

### RNF-01 — Seguridad

El sistema deberá utilizar autenticación mediante credenciales
individuales, control de acceso basado en roles y mecanismos de
protección para información sensible.

Se deberá contemplar autenticación de segundo factor para dispositivos
no reconocidos.

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

Un odontólogo no podrá tener dos turnos superpuestos en un mismo
período horario.

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

### Funcionalidades principales

- Gestión de pacientes.
- Gestión de turnos.
- Historia clínica digital.
- Registro de tratamientos.
- Registro de observaciones clínicas.
- Odontograma.
- Autenticación y autorización.
- Administración de usuarios y roles.

### Funcionalidades previstas para evolución

- Bioimágenes.
- Gestión financiera.
- Reportes.
- Exportación de historias clínicas.
- Notificaciones automáticas.
- Integraciones con servicios externos.

## 8. Restricciones

El sistema será inicialmente una aplicación web.

Las funcionalidades orientadas a pacientes externos al consultorio,
las integraciones con sistemas externos de salud y las herramientas
avanzadas de análisis financiero no forman parte del alcance inicial.
