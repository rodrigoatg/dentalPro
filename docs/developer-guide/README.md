# Guía de desarrollo de DentalPro

Esta guía enseña a leer, ejecutar y modificar el DentalPro que existe hoy en el repositorio. Está dirigida a alguien que ya programa y quiere entender las decisiones y el recorrido real de los datos.

## Ruta de aprendizaje

1. [Visión del proyecto](01-project-overview.md): propósito, tecnologías y arquitectura ejecutable.
2. [Preparar el entorno](02-environment-setup.md): Docker Compose y ejecución local en Windows.
3. [Arquitectura](03-architecture.md): responsabilidades y dirección de dependencias.
4. [Backend](04-backend.md): Domain, Application, Infrastructure y API con ejemplos.
5. [Frontend](05-frontend.md): rutas, componentes, servicios, sesión y permisos.
6. [Base de datos](06-database.md): esquema, relaciones, migraciones y mantenimiento seguro.
7. [Autenticación y RBAC](07-authentication-and-rbac.md): JWT, permisos efectivos y matriz inicial.
8. [Flujos de negocio](08-business-flows.md): Pacientes, Agenda y registro clínico de extremo a extremo.
9. [Pruebas y diagnóstico](09-testing-and-debugging.md): suites y problemas típicos.
10. [Rutinas de desarrollo](10-development-workflows.md): cómo extender el sistema con cambios pequeños.
11. [Ejemplos comentados](11-code-examples.md): fragmentos del repositorio explicados.
12. [Glosario](12-glossary.md): vocabulario usado en la solución.

## Cómo usar esta guía

Leé primero los capítulos 1–3 para construir un mapa mental. Después seguí un flujo completo del capítulo 8 y abrí los archivos citados en el IDE. Para modificar una parte, consultá el capítulo correspondiente y la documentación de dominio/ADR antes de tocar código.

Las rutas son relativas a la raíz. Los comandos se basan en `docker-compose.yml`, proyectos `.csproj`, `package.json`, scripts y pruebas actuales. No ejecutes `database update`, bootstrap ni comandos que alteren datos sin revisar antes el destino. `.env` contiene secretos locales.

## Fuente de verdad y límites

El código determina qué rutas y comportamientos están expuestos. `docs/domain/`, `docs/architecture/decisions/` y `docs/product/` describen intención. Si difieren del código, esta guía señala la discrepancia. Fue preparada por inspección del repositorio, no ejecutando la aplicación o conectándose a PostgreSQL.

Estado observado: pacientes, agenda, configuración de horarios y consulta/registro clínico están conectados en backend y frontend. El odontograma se crea vacío con el paciente, pero actualmente no hay API para editar piezas ni persistencia de `OdontogramTooth`/`ToothStateHistory`. Tampoco se expone edición de entradas/versiones clínicas.

Hay documentación general anterior que quedó atrasada: `docs/architecture/architecture.md` §17 todavía dice que los endpoints de agenda y pantallas Angular no están implementados, aunque ahora existen `AppointmentEndpoints`, `ClinicScheduleEndpoints` y las páginas Angular. `docs/domain/domain-model.md` describe conceptos de odontograma que aún no aparecen como filas/endpoints en el modelo EF/API. Usá código + esta guía para el estado ejecutable y propone actualizar esos documentos fuente cuando cambie el alcance.
