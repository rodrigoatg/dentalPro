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
dotnet run --project src/DentalPro.Api --launch-profile http
```

La API queda disponible en `http://localhost:5010`.

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
