# DentalPro - Contexto del Proyecto

## 1. Descripción

DentalPro es una aplicación web para la gestión de un
consultorio odontológico.

Su objetivo es digitalizar y centralizar los procesos
clínicos y administrativos que actualmente se realizan
mediante agendas en papel, documentos físicos y planillas
de cálculo.

El sistema permitirá gestionar pacientes, turnos e
información clínica desde una interfaz web.

---

## 2. Objetivo

Centralizar la información del consultorio y reducir
errores y tiempos asociados a procesos manuales.

---

## 3. Usuarios

### Recepcionista

Responsabilidades:

- Registrar pacientes.
- Consultar pacientes.
- Crear turnos.
- Modificar turnos.
- Cancelar turnos.
- Consultar agenda.

### Odontólogo

Responsabilidades:

- Consultar pacientes.
- Consultar historias clínicas.
- Registrar tratamientos.
- Registrar observaciones clínicas.
- Actualizar información clínica.

### Administrativo

Responsabilidades:

- Gestionar información financiera.
- Consultar movimientos.
- Generar reportes.

### Administrador del sistema

Responsabilidades:

- Gestionar usuarios.
- Gestionar roles.
- Configurar horarios.

---

# 4. MVP

El MVP inicial estará compuesto por:

1. Autenticación.
2. Gestión de usuarios y roles.
3. Gestión de pacientes.
4. Gestión de turnos.
5. Agenda.
6. Historia clínica básica.
7. Registro de tratamientos.
8. Registro de observaciones.

---

# 5. Funcionalidades futuras

Las siguientes funcionalidades forman parte del
producto completo pero no serán prioritarias para
la primera implementación:

- Bioimágenes.
- Gestión financiera.
- Reportes.
- Exportación de historias clínicas.
- Notificaciones externas.
- Integraciones con servicios externos.

---

# 6. Seguridad

El sistema deberá contemplar:

- Autenticación.
- Autorización basada en roles.
- Contraseñas almacenadas de forma segura.
- HTTPS.
- Auditoría de acciones relevantes.
- Expiración de sesión.

Las funcionalidades avanzadas de 2FA podrán incorporarse
posteriormente.

---

# 7. Arquitectura

El sistema utilizará una arquitectura cliente-servidor.

Frontend:

Aplicación web.

Backend:

API REST.

Persistencia:

Base de datos relacional.

---

# 8. Principios de desarrollo

- Mantener separación de responsabilidades.
- Evitar lógica de negocio en el frontend.
- Validar información en frontend y backend.
- Utilizar DTOs para comunicación entre capas.
- Mantener APIs REST.
- Escribir código mantenible y documentado.
- Priorizar simplicidad.
- Evitar dependencias innecesarias.
- Implementar tests para la lógica crítica.

---

# 9. Alcance

El objetivo inicial es obtener un MVP funcional,
probable de desarrollar y probar por un equipo
de dos personas durante la cursada.

No se deben implementar funcionalidades fuera
del MVP sin autorización explícita.

---

# 10. Regla fundamental

Antes de implementar una funcionalidad importante:

1. Analizar el requerimiento.
2. Revisar la arquitectura existente.
3. Proponer una estrategia.
4. Esperar aprobación.
5. Implementar.
6. Ejecutar pruebas.
7. Informar resultados.