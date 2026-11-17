# DentalPro

Aplicaci&oacute;n de gesti&oacute;n odontol&oacute;gica. El MVP local incluye dashboard y alta/listado de pacientes.

## Desarrollo con Docker Compose

Requisitos: Docker Desktop con Docker Compose y un archivo `.env` local. Preparalo
una vez desde el ejemplo:

```powershell
Copy-Item .env.example .env
```

Edit&aacute; `.env` antes de iniciar:

- `POSTGRES_PASSWORD` debe coincidir con la contrase&ntilde;a ya configurada en el
  volumen existente de PostgreSQL. Cambiarla en `.env` no modifica una base ya
  inicializada.
- `JWT_SIGNING_KEY` debe ser una clave Base64 que represente al menos 32 bytes.
  Pod&eacute;s generarla en PowerShell con
  `[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))`.

- Como el GetBytes puede dar error usar
`$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()`
`$bytes = New-Object byte[] 32`
`$rng.GetBytes($bytes)`
`$jwtKey = [Convert]::ToBase64String($bytes)`
`$rng.Dispose()`
- Las variables `SECURITY_BOOTSTRAP_ADMIN_*` se completan solo para el comando
  expl&iacute;cito de bootstrap.

No agregues `.env` a Git. Desde la ra&iacute;z del repositorio, levant&aacute; los tres
servicios:

```powershell
docker compose up --build
```

Direcciones locales: Angular `http://localhost:4200`, API
`http://localhost:5010` y PostgreSQL `localhost:5432`. Angular usa su proxy
Docker para reenviar `/api` a `api:8080`. El proxy local
`frontend/proxy.conf.json` sigue apuntando a `localhost:5010`.

Los fuentes se montan para hot reload. `node_modules`, paquetes NuGet y las
carpetas `bin`/`obj` viven en vol&uacute;menes Docker separados para no mezclar
dependencias o binarios de Windows con Linux.

### Operaciones expl&iacute;citas de persistencia y seguridad

Compose no aplica migraciones ni ejecuta el bootstrap al iniciar. Con los
servicios disponibles, aplic&aacute; migraciones manualmente cuando corresponda:

```powershell
docker compose run --rm api dotnet tool restore
docker compose run --rm api dotnet ef database update --project src/DentalPro.Infrastructure --startup-project src/DentalPro.Api
```

Para el primer bootstrap, carg&aacute; temporalmente en `.env` las variables
`SECURITY_BOOTSTRAP_ADMIN_USERNAME`, `SECURITY_BOOTSTRAP_ADMIN_PASSWORD`,
`SECURITY_BOOTSTRAP_ADMIN_FIRST_NAME` y `SECURITY_BOOTSTRAP_ADMIN_LAST_NAME`
(el correo es opcional), y ejecut&aacute;:

```powershell
docker compose run --rm api dotnet run --project src/DentalPro.Api -- --bootstrap-security
```

Luego quit&aacute; las variables de bootstrap de `.env`. El comando crea roles y
permisos idempotentemente, y agrega el primer administrador solo si a&uacute;n no
existe.

### Detener, estado y logs

```powershell
docker compose stop
docker compose ps
docker compose logs -f postgres api frontend
```

`docker compose down` elimina los contenedores y la red, pero conserva el
volumen. No uses `docker compose down -v` si necesit&aacute;s conservar la base.

## Requisitos locales

- .NET SDK 10
- PostgreSQL de desarrollo iniciado con Docker Compose (`dentalpro_dev`, puerto 5432)
- Node.js 22.12 o superior y npm

## Iniciar backend

En una terminal PowerShell, configur&aacute; la cadena de conexi&oacute;n local sin guardarla en el repositorio y levant&aacute; la API:

```powershell
$env:ConnectionStrings__DentalPro = "Host=localhost;Port=5432;Database=dentalpro_dev;Username=<usuario>;Password=<clave>"
$env:Jwt__SigningKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
dotnet run --project src/DentalPro.Api --launch-profile http
```

La API queda disponible en `http://localhost:5010`.

La clave JWT se genera para la sesión local y no se guarda en archivos del
repositorio. La expiración se configura con `Jwt:AccessTokenLifetimeMinutes`
en `appsettings.json` y puede sobrescribirse mediante
`Jwt__AccessTokenLifetimeMinutes`. El login requiere que la migración aditiva
de Identity/RBAC haya sido aplicada previamente por el proceso de despliegue;
el arranque de la API no ejecuta migraciones.

Antes del primer inicio, ejecutá el bootstrap operativo una vez. La conexión,
el nombre, la contraseña y los datos del primer administrador deben llegar por
variables de entorno o un proveedor de secretos; no los agregues a archivos
versionados:

```powershell
$env:SecurityBootstrap__InitialAdministrator__UserName = "admin"
$env:SecurityBootstrap__InitialAdministrator__Password = "<contraseña-segura>"
$env:SecurityBootstrap__InitialAdministrator__FirstName = "DentalPro"
$env:SecurityBootstrap__InitialAdministrator__LastName = "Administrator"
dotnet run --project src/DentalPro.Api -- --bootstrap-security
```

El comando crea roles y permisos idempotentemente y agrega el primer
administrador solo si todavía no hay ninguno. No se ejecuta automáticamente
al iniciar la API normal; luego del bootstrap, retir&aacute; las variables secretas
del entorno y ejecut&aacute; la API de forma habitual.

## Iniciar frontend

En otra terminal:

```powershell
cd frontend
npm ci
npm start
```

Abr&iacute; `http://localhost:4200`. El servidor de desarrollo reenv&iacute;a `/api` a la API local en `http://localhost:5010`; no hace falta habilitar CORS para este recorrido.

## Validar frontend

Desde `frontend/`:

```powershell
npm test -- --watch=false
npm run build
```

## Build y tests de .NET

Desde la ra&iacute;z:

```powershell
dotnet build DentalPro.sln
dotnet test DentalPro.sln
```



## INSTALACIÓN NUEVA
# 1. Clonar
git clone URL_DEL_REPOSITORIO DentalPro
cd DentalPro

# 2. Configurar entorno
Copy-Item .env.example .env
notepad .env

# 3. Construir e iniciar
docker compose up --build -d

# 4. Verificar servicios
docker compose ps

# 5. Restaurar herramienta EF
docker compose run --rm api dotnet tool restore

# 6. Aplicar migraciones
docker compose run --rm api dotnet ef database update --project src/DentalPro.Infrastructure --startup-project src/DentalPro.Api

# 7. Inicializar seguridad
docker compose run --rm api dotnet run --project src/DentalPro.Api -- --bootstrap-security

# 8. Reiniciar API
docker compose up -d --force-recreate api




------------------------------------
# Guía de instalación de DentalPro en Windows

La configuración actual permite levantar PostgreSQL, API y Angular con Docker Compose. La API **no aplica migraciones automáticamente**: en una base nueva hay que aplicarlas manualmente antes del bootstrap y del uso normal.

No ejecuté contenedores, migraciones, bootstrap ni operaciones contra bases de datos.

## 1. Requisitos previos

Instalá:

- **Windows 10/11**.
- **Git for Windows**.
- **Docker Desktop**, configurado para usar contenedores Linux y el backend WSL 2.
- **Docker Compose v2**, incluido con Docker Desktop.

Comprobá Docker Compose en PowerShell:

```powershell
docker --version
docker compose version
```

No necesitás instalar localmente PostgreSQL, .NET SDK ni Node.js para este flujo: los contenedores usan PostgreSQL 17, .NET SDK 10 y Node 22.

**Diferencia de versiones a tener presente:** Compose especifica `postgres:17-alpine`, mientras que las instrucciones del repositorio indican PostgreSQL 18. Esta guía describe lo que ejecuta actualmente Compose: PostgreSQL 17. Si el requisito real es PostgreSQL 18, hay que resolver esa discrepancia antes de instalar.

## 2. Clonar el repositorio

En PowerShell, reemplazá `<URL-del-repositorio>` por la URL Git correspondiente:

```powershell
git clone <URL-del-repositorio> DentalPro
Set-Location .\DentalPro
```

Ejecutá los comandos posteriores desde la carpeta que contiene `docker-compose.yml`.

## 3. Crear `.env` y preparar secretos

El repositorio proporciona [.env.example](</X:/Proyectos/DentalPro/dentalPro/.env.example>). Creá la copia local, que está excluida de Git:

```powershell
Copy-Item .env.example .env
notepad .env
```

Configurá `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_PORT` y `JWT_SIGNING_KEY`. **No dejes los valores `REPLACE_WITH_...` del ejemplo.** Para la primera instalación podés conservar `dentalpro_dev`, `dentalpro` y el puerto `5432`.

Para generar una clave JWT aleatoria de 32 bytes y copiarla al portapapeles sin imprimirla en la terminal, ejecutá este bloque; luego pegá el valor en `JWT_SIGNING_KEY` en `.env`:

```powershell
$bytes = New-Object byte[] 32
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($bytes)
[Convert]::ToBase64String($bytes) | Set-Clipboard
$rng.Dispose()
```

En una **computadora nueva**, elegí también una contraseña nueva para PostgreSQL y guardala en `.env`. Si vas a usar un volumen ya persistido en esa misma computadora, necesitás la contraseña que corresponde a ese volumen: cambiar `POSTGRES_PASSWORD` en `.env` por sí solo **no cambia** la contraseña guardada dentro de PostgreSQL.

Para el primer bootstrap, completá además estos campos en `.env`:

- `SECURITY_BOOTSTRAP_ADMIN_USERNAME`
- `SECURITY_BOOTSTRAP_ADMIN_PASSWORD`
- `SECURITY_BOOTSTRAP_ADMIN_FIRST_NAME`
- `SECURITY_BOOTSTRAP_ADMIN_LAST_NAME`

`SECURITY_BOOTSTRAP_ADMIN_EMAIL` es opcional. No guardes este archivo en Git ni compartas su contenido. El emisor y la audiencia JWT no requieren variables adicionales: actualmente la API usa los valores predeterminados `DentalPro` y `DentalPro.Api`.

## 4. Construir e iniciar los contenedores

Para construir imágenes y dejar los servicios ejecutándose en segundo plano:

```powershell
docker compose up --build -d
```

Para iniciar en primer plano y ver los logs directamente:

```powershell
docker compose up --build
```

El servicio de API espera a que PostgreSQL pase su healthcheck; Angular espera a que la API pase el suyo. El healthcheck de PostgreSQL usa `pg_isready`. El de la API comprueba que el puerto `8080` acepte conexiones TCP: **no comprueba que las migraciones estén aplicadas ni que la API pueda consultar la base**. El servicio frontend no tiene healthcheck propio.

Comprobá el estado:

```powershell
docker compose ps
```

PostgreSQL y API deberían figurar como `healthy`; Angular debería estar ejecutándose. También podés comprobar PostgreSQL directamente:

```powershell
docker compose exec postgres sh -c 'pg_isready -U "$POSTGRES_USER" -d "$POSTGRES_DB"'
```

## 5. Aplicar las migraciones

Hacelo después de que PostgreSQL esté listo y antes del bootstrap. Estos comandos, documentados por el proyecto, restauran la herramienta local `dotnet-ef` y aplican todas las migraciones pendientes a la base configurada en `.env`:

```powershell
docker compose run --rm api dotnet tool restore
docker compose run --rm api dotnet ef database update --project src/DentalPro.Infrastructure --startup-project src/DentalPro.Api
```

En una base nueva, se aplicará la cadena completa de siete migraciones:

1. `20261013021751_InitialPatientPersistence`
2. `20261104034329_AddIdentityAndRbacPersistence`
3. `20261111005924_AddAppointmentPersistence`
4. `20261111032751_AddAppointmentAuditAndOverlapConstraints`
5. `20261115005635_AddClinicScheduleAndAppointmentDuration`
6. `20261115160242_ValidateClinicScheduleTimeZone`
7. `20261115224207_AddClinicalRecordsAndTreatments`

`database update` aplica migraciones pendientes, no es un comando para borrar y recrear la base. En una instalación con datos existentes, primero verificá qué migraciones figuran en `__EFMigrationsHistory` y hacé un respaldo antes de aplicar cambios.

## 6. Ejecutar el bootstrap de seguridad

Después de las migraciones, ejecutá exactamente:

```powershell
docker compose run --rm api dotnet run --project src/DentalPro.Api -- --bootstrap-security
```

Este comando es una operación puntual: crea o completa los roles y permisos y, si todavía no existe un usuario administrador, crea el administrador inicial con los datos configurados en `.env`. La API no ejecuta el bootstrap al arrancar normalmente.

Después, vaciá en `.env` los campos `SECURITY_BOOTSTRAP_ADMIN_*` que completaste y recreá la API para que el contenedor normal tampoco conserve esos valores:

```powershell
docker compose up -d --force-recreate api
```

Si ya existe un administrador en una base persistida, el bootstrap no necesita crear otro usuario; no completes ni cambies credenciales sin necesitarlo.

### Comprobar roles y permisos

Abrí una sesión `psql` dentro del contenedor:

```powershell
docker compose exec postgres psql -U dentalpro -d dentalpro_dev
```

Si personalizaste `POSTGRES_USER` o `POSTGRES_DB`, usá esos valores en el comando. En el prompt de `psql`, comprobá roles y cantidad de permisos asignados:

```sql
SELECT r."Name", COUNT(rp."PermissionId") AS permission_count
FROM "AspNetRoles" r
LEFT JOIN "RolePermissions" rp ON rp."RoleId" = r."Id"
GROUP BY r."Name"
ORDER BY r."Name";
```

Comprobá que exista un usuario con el rol Administrator:

```sql
SELECT u."UserName", u."Email", r."Name" AS role_name
FROM "AspNetUsers" u
JOIN "AspNetUserRoles" ur ON ur."UserId" = u."Id"
JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
WHERE r."Name" = 'Administrator';
```

Salí de `psql` con:

```text
\q
```

## 7. Abrir y verificar la aplicación

- Angular: [http://localhost:4200](http://localhost:4200)
- API: [http://localhost:5010](http://localhost:5010)
- PostgreSQL, para clientes locales: `localhost:5432`

El enlace Angular → API usa el proxy de Docker: el navegador envía las solicitudes `/api` al frontend y el contenedor frontend las deriva a `http://api:8080` mediante DNS de Compose. Dentro de Docker, la API conecta con PostgreSQL en `postgres:5432`; `localhost` no sería la dirección correcta entre esos contenedores.

No hay un endpoint HTTP de healthcheck de la API configurado en Compose. Para confirmar que responde la aplicación, podés abrir la pantalla de inicio de sesión de Angular e iniciar sesión con el usuario administrador. La comprobación TCP de Compose, por sí sola, no verifica la conexión a la base.

## 8. Logs y diagnóstico

Estado de servicios:

```powershell
docker compose ps
```

Logs de todos los servicios:

```powershell
docker compose logs -f postgres api frontend
```

Logs recientes de la API:

```powershell
docker compose logs --tail 100 api
```

Si falla el arranque, revisá primero los logs de PostgreSQL y API. Si PostgreSQL no pasa a `healthy`, comprobá que el puerto 5432 esté libre y que las credenciales coincidan con el volumen. Los puertos locales actuales son PostgreSQL `5432`, API `5010` y frontend `4200`; un conflicto con cualquiera puede impedir publicar el servicio.

## 9. Detener y volver a iniciar sin perder datos

Para detener los servicios y conservar los datos:

```powershell
docker compose stop
```

Para iniciarlos posteriormente sin reconstruir imágenes:

```powershell
docker compose start
```

También podés quitar los contenedores y conservar los volúmenes:

```powershell
docker compose down
```

Luego podés volver a crearlos sin reconstrucción:

```powershell
docker compose up -d
```

No ejecutes `docker compose down -v` si querés conservar la base de datos.

Compose asigna explícitamente el volumen PostgreSQL `dentalpro_postgres_data`, por lo que no cambia de nombre al cambiar el nombre de la carpeta del proyecto. Docker mantiene ese volumen en la computadora local; **clonar el repositorio en una computadora nueva no copia los datos del volumen anterior**. Para trasladar una base existente se necesita un respaldo y una restauración por separado.

## 10. Actualizar el proyecto después de `git pull`

Antes de actualizar, revisá el estado local para detectar cambios propios:

```powershell
git status --short
```

Si no hay cambios locales pendientes que debas proteger:

```powershell
docker compose down
git pull --ff-only
docker compose up --build -d
```

`docker compose down` sin `-v` conserva el volumen. Si la actualización añade migraciones, revisá los cambios y aplicá manualmente las pendientes con:

```powershell
docker compose run --rm api dotnet tool restore
docker compose run --rm api dotnet ef database update --project src/DentalPro.Infrastructure --startup-project src/DentalPro.Api
```

El bootstrap normalmente no hace falta en una instalación ya inicializada. Si la nueva versión o el estado de seguridad requiere ejecutarlo, sigue siendo explícito:

```powershell
docker compose run --rm api dotnet run --project src/DentalPro.Api -- --bootstrap-security
```

## Hallazgos que afectan una instalación limpia

- `.env.example` contiene marcadores de posición: hay que reemplazarlos antes de iniciar los servicios.
- La contraseña configurada en `.env` inicializa PostgreSQL cuando se crea un volumen vacío. En un volumen existente, cambiarla en el archivo no modifica automáticamente el usuario de la base.
- Una máquina nueva crea un volumen vacío aunque el volumen de la instalación anterior tuviera el mismo nombre.
- Compose usa PostgreSQL 17; hay una discrepancia con la indicación de PostgreSQL 18 en las instrucciones del proyecto.
- La API no migra ni inicializa RBAC automáticamente. Deben ejecutarse los comandos manuales anteriores, en ese orden.
- El healthcheck de API solo prueba el puerto TCP y frontend carece de healthcheck; la pantalla de login confirma mejor el flujo funcional.

Las rutas relevantes son [docker-compose.yml](</X:/Proyectos/DentalPro/dentalPro/docker-compose.yml>), [Dockerfile de la API](</X:/Proyectos/DentalPro/dentalPro/src/DentalPro.Api/Dockerfile>), [Dockerfile de Angular](</X:/Proyectos/DentalPro/dentalPro/frontend/Dockerfile>), [proxy Docker](</X:/Proyectos/DentalPro/dentalPro/frontend/proxy.docker.conf.json>) y [README](</X:/Proyectos/DentalPro/dentalPro/README.md>).

-------------------------------------
# GUIA FINAL
# DentalPro

Sistema de gestión para clínicas odontológicas.

DentalPro permite administrar pacientes, odontólogos, turnos, historias clínicas, tratamientos y usuarios mediante una aplicación web con control de acceso por roles.

## 1. Tecnologías utilizadas

| Componente | Tecnología |
|---|---|
| Frontend | Angular 21 |
| Backend | ASP.NET Core .NET 10 |
| ORM | Entity Framework Core 10 |
| Base de datos | PostgreSQL 17 |
| Contenedores | Docker y Docker Compose |
| Arquitectura | Domain, Application, Infrastructure, API |

## 2. Requisitos previos

Para ejecutar DentalPro en Windows se necesita:

- Windows 10 u 11.
- Git for Windows.
- Docker Desktop.
- Docker Compose v2, incluido con Docker Desktop.
- Docker Desktop configurado para utilizar contenedores Linux y WSL 2.

Descargas:

- Git: https://git-scm.com/downloads
- Docker Desktop: https://www.docker.com/products/docker-desktop/
- Visual Studio Code (opcional): https://code.visualstudio.com/

No es necesario instalar localmente PostgreSQL, .NET SDK ni Node.js para ejecutar la solución mediante Docker Compose.

Las imágenes de los contenedores incluyen las herramientas necesarias para construir y ejecutar los servicios.

### Verificar instalaciones

Abrir PowerShell y ejecutar:

```powershell
git --version
docker --version
docker compose version
```

## 3. Obtener el código fuente

Clonar el repositorio:

```powershell
cd X:\Proyectos
git clone URL_DEL_REPOSITORIO DentalPro
cd DentalPro
```

Reemplazar `URL_DEL_REPOSITORIO` por la dirección real del repositorio.

Los comandos posteriores deben ejecutarse desde la carpeta que contiene `docker-compose.yml`.

## 4. Configurar variables de entorno

El proyecto utiliza un archivo `.env` para configurar los servicios y sus credenciales.

Crear una copia del archivo de ejemplo:

```powershell
Copy-Item .env.example .env
```

Abrirlo:

```powershell
notepad .env
```

Completar las variables necesarias:

- `POSTGRES_DB`: nombre de la base de datos.
- `POSTGRES_USER`: usuario de PostgreSQL.
- `POSTGRES_PASSWORD`: contraseña de PostgreSQL.
- `POSTGRES_PORT`: puerto local de PostgreSQL.
- `JWT_SIGNING_KEY`: clave utilizada para firmar los tokens JWT.
- `SECURITY_BOOTSTRAP_ADMIN_USERNAME`: usuario del administrador inicial.
- `SECURITY_BOOTSTRAP_ADMIN_PASSWORD`: contraseña del administrador inicial.
- `SECURITY_BOOTSTRAP_ADMIN_FIRST_NAME`: nombre del administrador.
- `SECURITY_BOOTSTRAP_ADMIN_LAST_NAME`: apellido del administrador.
- `SECURITY_BOOTSTRAP_ADMIN_EMAIL`: correo del administrador (opcional).

No dejar valores `REPLACE_WITH_...` del archivo de ejemplo.

Para generar una clave JWT aleatoria de 32 bytes en PowerShell y copiarla al portapapeles:

```powershell
$bytes = New-Object byte[] 32
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($bytes)
[Convert]::ToBase64String($bytes) | Set-Clipboard
$rng.Dispose()
```

Pegar el resultado en `JWT_SIGNING_KEY`.

**Seguridad:** el archivo `.env` contiene información sensible. No debe subirse al repositorio ni compartirse públicamente. El repositorio debe conservar `.env.example` sin contraseñas ni claves reales.

### Reutilizar el archivo `.env` de otra computadora

Es posible copiar el `.env` de una instalación existente, siempre que se mantengan coherentes las variables y los servicios.

Sin embargo, copiar el archivo no copia la base de datos.

Una instalación nueva tendrá un volumen PostgreSQL vacío y necesitará sus propias migraciones y bootstrap.

## 5. Construir e iniciar DentalPro

Abrir Docker Desktop y esperar a que el motor esté iniciado.

Desde la carpeta del proyecto:

```powershell
docker compose config
```

Este comando permite comprobar la configuración de Docker Compose.

Construir las imágenes e iniciar los servicios:

```powershell
docker compose up --build -d
```

Consultar el estado:

```powershell
docker compose ps
```

Los servicios principales son:

| Servicio | Función | Puerto |
|---|---|---|
| postgres | Base de datos PostgreSQL | 5432 |
| api | Backend ASP.NET Core | 5010 |
| frontend | Aplicación Angular | 4200 |

PostgreSQL y API deberían figurar como `healthy`. El frontend debería estar ejecutándose.

El healthcheck de la API comprueba la disponibilidad del puerto TCP, pero no garantiza que las migraciones estén aplicadas ni que la conexión a la base de datos funcione.

## 6. Inicializar la base de datos

Este procedimiento corresponde a una instalación nueva con una base vacía.

DentalPro no ejecuta automáticamente las migraciones ni el bootstrap de seguridad al iniciar la API.

### 6.1 Restaurar la herramienta Entity Framework

```powershell
docker compose run --rm api dotnet tool restore
```

### 6.2 Aplicar las migraciones

```powershell
docker compose run --rm api dotnet ef database update --project src/DentalPro.Infrastructure --startup-project src/DentalPro.Api
```

En una base nueva se aplicará la cadena de migraciones del proyecto:

1. InitialPatientPersistence
2. AddIdentityAndRbacPersistence
3. AddAppointmentPersistence
4. AddAppointmentAuditAndOverlapConstraints
5. AddClinicScheduleAndAppointmentDuration
6. ValidateClinicScheduleTimeZone
7. AddClinicalRecordsAndTreatments

Entity Framework aplica las migraciones pendientes en orden.

No ejecutar este procedimiento a ciegas sobre una base existente sin revisar previamente las migraciones aplicadas y realizar un respaldo.

### 6.3 Ejecutar el bootstrap de seguridad

Después de aplicar las migraciones:

```powershell
docker compose run --rm api dotnet run --project src/DentalPro.Api -- --bootstrap-security
```

El bootstrap inicializa los roles y permisos del sistema y crea el administrador inicial si todavía no existe.

El usuario administrador se genera utilizando los valores configurados en las variables `SECURITY_BOOTSTRAP_ADMIN_*`.

El bootstrap es una operación explícita y no se ejecuta automáticamente al iniciar la API.

Una vez finalizado correctamente:

1. Guardar las credenciales del administrador en un lugar seguro.
2. Retirar los valores `SECURITY_BOOTSTRAP_ADMIN_*` del archivo `.env` cuando ya no sean necesarios.
3. Recrear el contenedor de API para que no conserve esas variables:

```powershell
docker compose up -d --force-recreate api
```

Si ya existe un administrador en una base persistida, no es necesario crear otro ni cambiar sus credenciales.

## 7. Verificar la instalación

Consultar el estado de los servicios:

```powershell
docker compose ps
```

Consultar los logs:

```powershell
docker compose logs --tail 100 api
```

Verificar PostgreSQL:

```powershell
docker compose exec postgres sh -c 'pg_isready -U "$POSTGRES_USER" -d "$POSTGRES_DB"'
```

Acceder a la aplicación:

- Frontend: http://localhost:4200
- API: http://localhost:5010
- PostgreSQL: localhost:5432

La mejor comprobación funcional es ingresar a Angular con el usuario administrador y verificar que la aplicación pueda consultar la API y la base de datos.

## 8. Iniciar DentalPro después de apagar la computadora

Si Docker Desktop ya está instalado y los contenedores fueron creados anteriormente, no es necesario reconstruir las imágenes ni volver a aplicar las migraciones.

Procedimiento:

1. Encender la computadora.
2. Iniciar Docker Desktop.
3. Esperar a que Docker esté funcionando.
4. Abrir PowerShell en la carpeta del proyecto.
5. Ejecutar:

```powershell
docker compose start
```

6. Comprobar el estado:

```powershell
docker compose ps
```

7. Esperar a que PostgreSQL y API estén saludables.
8. Abrir http://localhost:4200.

No ejecutar nuevamente el bootstrap ni las migraciones para un inicio normal.

## 9. Detener DentalPro antes de apagar la computadora

Para detener todos los servicios de manera ordenada y conservar la información:

```powershell
docker compose stop
```

Después se puede apagar Windows normalmente.

Este comando no elimina:

- Pacientes.
- Usuarios.
- Roles y permisos.
- Turnos.
- Historias clínicas.
- Tratamientos.
- Configuración horaria.

Los datos permanecen almacenados en el volumen persistente de PostgreSQL.

## 10. Actualizar DentalPro después de cambios en el repositorio

Antes de actualizar:

```powershell
git status
```

Verificar que no existan cambios locales que deban conservarse.

Obtener los últimos cambios:

```powershell
git pull --ff-only
```

Reconstruir y levantar los servicios:

```powershell
docker compose up --build -d
```

Si la actualización incorpora nuevas migraciones, revisar los cambios y aplicarlas manualmente:

```powershell
docker compose run --rm api dotnet tool restore

docker compose run --rm api dotnet ef database update --project src/DentalPro.Infrastructure --startup-project src/DentalPro.Api
```

El bootstrap normalmente no necesita ejecutarse nuevamente en una instalación ya inicializada.

## 11. Consultar logs y diagnosticar problemas

### Estado de los contenedores

```powershell
docker compose ps
```

### Logs de todos los servicios

```powershell
docker compose logs -f postgres api frontend
```

### Logs de la API

```powershell
docker compose logs --tail 150 api
```

### Logs de PostgreSQL

```powershell
docker compose logs --tail 100 postgres
```

### Problemas frecuentes

**PostgreSQL no inicia:**
- Verificar que el puerto 5432 no esté ocupado.
- Revisar los logs.
- Comprobar que las credenciales coincidan con las utilizadas al crear el volumen.

**La API no inicia:**
- Revisar los logs.
- Comprobar que PostgreSQL esté saludable.
- Verificar la configuración de conexión.
- Comprobar que las migraciones estén aplicadas.

**Angular no se comunica con la API:**
- Comprobar que API y frontend estén ejecutándose.
- Revisar los logs de ambos servicios.
- Verificar la configuración del proxy Docker.

**No se puede iniciar sesión:**
- Comprobar que el usuario administrador exista.
- Verificar que se haya ejecutado el bootstrap.
- Confirmar que las credenciales ingresadas sean las correctas.
- Revisar los logs de la API.
- Comprobar que Identity y las tablas correspondientes estén creadas.

Volver a ejecutar el bootstrap no restablece automáticamente la contraseña de un administrador existente.

## 12. Persistencia y respaldos

Docker Compose utiliza el volumen persistente:

`dentalpro_postgres_data`

Este volumen almacena los datos de PostgreSQL y sobrevive al apagado de los contenedores.

Es importante distinguir:

- Detener contenedores no elimina los datos.
- Eliminar contenedores con `docker compose down` conserva el volumen.
- Reconstruir imágenes no elimina el volumen.
- Clonar el repositorio en otra computadora no copia el volumen de la computadora original.

Para trasladar una instalación existente a otra computadora se necesita generar un backup de PostgreSQL y restaurarlo en el nuevo entorno.

### Comandos seguros para detener y reiniciar

Detener:

```powershell
docker compose stop
```

Iniciar:

```powershell
docker compose start
```

Reconstruir:

```powershell
docker compose up --build -d
```

Eliminar contenedores sin eliminar datos:

```powershell
docker compose down
```

**ADVERTENCIA:** no ejecutar `docker compose down -v` si se desea conservar la base de datos. La opción `-v` puede eliminar los volúmenes asociados y provocar la pérdida de información.

## 13. Consideraciones de seguridad

- No subir `.env` al repositorio.
- No compartir contraseñas ni claves JWT.
- Utilizar credenciales seguras para PostgreSQL y el administrador.
- No ejecutar scripts destructivos sobre bases persistentes sin un backup.
- No ejecutar migraciones sin revisar previamente los cambios.
- Mantener los permisos y roles administrados mediante el mecanismo RBAC de la aplicación.

---

## Resumen rápido de comandos

| Operación | Comando |
|---|---|
| Construir e iniciar | `docker compose up --build -d` |
| Iniciar contenedores existentes | `docker compose start` |
| Detener sin perder datos | `docker compose stop` |
| Consultar estado | `docker compose ps` |
| Consultar logs | `docker compose logs -f` |
| Actualizar código | `git pull --ff-only` |
| Reconstruir después de actualizar | `docker compose up --build -d` |
| Eliminar contenedores conservando datos | `docker compose down` |

**Recordatorio:** las migraciones y el bootstrap se ejecutan durante la instalación inicial o cuando existe una necesidad concreta. No forman parte del procedimiento habitual de encendido y apagado de DentalPro.