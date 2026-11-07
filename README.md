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
