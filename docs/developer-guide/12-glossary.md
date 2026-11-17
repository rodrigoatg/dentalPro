# 12. Glosario

| Término | Significado en DentalPro |
|---|---|
| Aggregate / agregado | Unidad del dominio que mantiene invariantes relacionadas; Patient crea su MedicalRecord asociado. |
| API | Frontera HTTP/JSON implementada en `DentalPro.Api`. |
| Claim | Dato del principal autenticado; `sub` identifica el usuario JWT. |
| Concurrencia | Operaciones simultáneas que pueden competir por intervalos/filas; se controla con transacciones, locks y constraints. |
| Dependency Injection | Contenedor de ASP.NET que construye servicios registrados. |
| Domain | Conceptos y reglas sin dependencia de tecnología de persistencia. |
| DTO | Objeto de transporte; no necesariamente coincide con entidad Domain o fila EF. |
| Entity | Objeto identificado; la entidad Domain encapsula reglas y una Row representa persistencia. |
| EF Core | ORM que Infrastructure usa para mapear objetos a relacional. |
| Guard | Control de navegación Angular; no autoriza por sí solo una llamada API. |
| Identity | Framework ASP.NET para usuarios, hashes de password, roles y almacenamiento asociado. |
| Interceptor | Código Angular transversal que modifica solicitudes/respuestas; aquí adjunta Bearer y procesa 401/403. |
| IANA time zone | ID de zona horaria usado por agenda/DST; no hay zona predeterminada en DentalPro. |
| Migration | Cambio versionado EF con `Up` y `Down`; se aplica como operación explícita. |
| Minimal API | Declaración de rutas ASP.NET mediante handlers sin controllers tradicionales. |
| Observable | Secuencia RxJS de HttpClient que se consume/suscribe para ejecutar solicitud. |
| Exclusion constraint | Restricción PostgreSQL que prohíbe combinaciones solapadas; usada para reservas. |
| Repository | Abstracción para recuperar/persistir agregados sin exponer detalles DB a Application. |
| RBAC | Autorización por roles y permisos asignados a roles. |
| Snapshot | Versión inmutable que conserva estado completo de ClinicalEntry. |
| Transaction | Unidad commit/rollback común para un grupo de escrituras. |
| Unit of Work | Delimita operación transaccional, guardado y commit/rollback. |
| Value Object | Valor definido por contenido e invariantes, no identidad; `ToothNumber` valida FDI. |
| Audit | Registro append-only de actor, acción y recurso en operaciones relevantes. No es Event Sourcing. |
| Domain Event | Mensaje que representa un hecho del dominio. No se identificó dispatcher/bus de Domain Events; la auditoría usa contratos explícitos. |
