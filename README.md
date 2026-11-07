# DentalPro

Aplicaci&oacute;n de gesti&oacute;n odontol&oacute;gica. El MVP local incluye dashboard y alta/listado de pacientes.

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
