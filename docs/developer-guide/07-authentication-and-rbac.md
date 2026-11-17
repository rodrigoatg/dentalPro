# 7. Autenticación y autorización

## Login y sesión

`POST /api/auth/login` acepta `LoginRequest`. Identity valida credenciales/estado; `JwtTokenGenerator` emite bearer token cuyo `sub` identifica al usuario. La respuesta contiene token, usuario, roles y expiración. Angular después pide `GET /api/auth/me` para obtener permisos efectivos vigentes.

JWT se configura desde sección `Jwt`: issuer, audience, signing key y vida del token. Compose mapea `JWT_SIGNING_KEY` a `Jwt__SigningKey`; issuer/audience tienen defaults en `appsettings.json`. La clave debe representar al menos 32 bytes. No guardes su valor en Git/documentación.

Identity almacena hashes de contraseñas. Validación de sesión comprueba estado y security stamp actuales; los permisos no son autoridad basada solo en claims persistidos en JWT.

## Roles y permisos

Rol agrupa capacidades; Permission code identifica una capacidad concreta. `RolePermission` enlaza ambos. Usuario puede pertenecer a varios roles; los permisos efectivos son su unión. No hay asignaciones individuales de permiso a usuario.

`PermissionCatalog` es el catálogo canónico. `SecurityBootstrapper` crea roles, completa metadatos y asigna permisos. Code es identificador estable; nombre y descripción son metadatos.

## Matriz inicial del bootstrap

Según `SecurityBootstrapper`, `PermissionCatalog` y `SystemRoles`:

| Permisos | Administrator | Receptionist | Dentist |
|---|:---:|:---:|:---:|
| `patients.view`, `patients.create`, `patients.update` | Sí | Sí | Sí |
| `appointments.view`, `appointments.view.all` | Sí | Sí | `appointments.view` |
| Crear, actualizar/reprogramar, cancelar, confirmar, completar appointments | Sí | Sí | confirmar y completar |
| `appointments.schedule.manage` | Sí | No | No |
| `clinical.records.view`, `clinical.records.view.all`, `clinical.entries.create`, `clinical.treatments.create` | No | No | view, entries, treatments |
| Users view/create/status/roles manage | Sí* | No | No |
| Roles view/permissions manage; permissions view | Sí* | No | No |

Los permisos clínicos no están marcados como protegidos ni están en asignaciones iniciales de Administrator. Dentist no recibe `clinical.records.view.all`; Application comprueba su scope propio. Administrator, por tanto, no recibe permisos clínicos por defecto.

## Dónde se autoriza

1. Bearer authentication establece usuario.
2. `RequirePermission(code)` pone requisito en el endpoint.
3. `PermissionAuthorizationHandler` consulta permiso efectivo actual.
4. Application comprueba alcance/propiedad para recursos concretos. Dentist actúa sobre sus propios turnos; general agenda se controla por permisos.
5. Angular puede ocultar rutas/acciones, pero no es frontera de seguridad.

API requiere autenticación por defecto, excepto login explícito. Cambios role-permission afectan decisiones posteriores sin renovar JWT según arquitectura actual.

## Bootstrap

Flag operacional, no ruta web:

```powershell
docker compose run --rm api dotnet run --project src/DentalPro.Api -- --bootstrap-security
```

`Program.cs` crea scope, llama `ISecurityBootstrapper.InitializeAsync()` y termina sin arrancar servidor. El bootstrap usa transacción Serializable. Si no hay Administrator exige username/password/first name/last name; email es opcional. Si ya existe Administrator, no lo vuelve a crear.

## Añadir permiso

1. Define capacidad, usuarios, scope y regla en requisitos/ADR si cambia decisión.
2. Agrega constante y `PermissionDefinition` en `PermissionCatalog`.
3. Define asignación inicial en `SecurityBootstrapper` con mínimo privilegio.
4. Protege ruta con `.RequirePermission(code)`.
5. Implementa scope/ownership en Application si hace falta; no uses rol hardcodeado dentro Domain.
6. Agrega pruebas de catálogo, asignación y API para actor sin permiso.
7. Refleja permiso en UI y matriz. Bootstrap agrega el nuevo code sin migrar esquema.

Permisos protegidos de Administrator no se quitan por operaciones API normales; hay protección para no dejar el sistema sin último administrador activo.
