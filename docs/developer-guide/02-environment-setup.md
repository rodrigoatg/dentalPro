# 2. Preparar y ejecutar el entorno

## Herramientas

Para ejecutar todo en contenedores: Windows 10/11, Git for Windows y Docker Desktop con contenedores Linux/WSL 2 y Compose v2. No se requiere instalar PostgreSQL, .NET SDK ni Node localmente en este camino. La primera construcción necesita descargar imágenes y dependencias.

```powershell
docker --version
docker compose version
```

Para desarrollar frontend o backend fuera de Docker sí se necesita Node 22/npm o .NET SDK 10 y una base PostgreSQL accesible. `package.json` declara npm 10.9.0. No hay `global.json` que fije el SDK .NET.

## Clonar y preparar secretos

```powershell
git clone <URL-del-repositorio> DentalPro
Set-Location .\DentalPro
Copy-Item .env.example .env
notepad .env
```

Reemplazá los marcadores de `.env.example`. Configurá `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_PORT` y `JWT_SIGNING_KEY`. En base nueva podés dejar base/usuario/puerto predeterminados; elegí contraseña nueva y clave JWT Base64 con al menos 32 bytes. En volumen existente necesitás la contraseña ya establecida: editar `.env` no cambia el rol de PostgreSQL. Las variables `SECURITY_BOOTSTRAP_ADMIN_*` solo hacen falta para el primer administrador; email es opcional.

Generador PowerShell de 32 bytes que copia el valor al portapapeles sin imprimirlo en consola:

```powershell
$bytes = New-Object byte[] 32
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($bytes)
[Convert]::ToBase64String($bytes) | Set-Clipboard
$rng.Dispose()
```

Pegá el resultado en el editor; generá otro secreto independiente para cada credencial.

## Construir y arrancar

Desde la raíz:

```powershell
docker compose up --build -d
docker compose ps
```

En primer plano: `docker compose up --build`. Esperá `postgres` y `api` en estado `healthy`, frontend en `Up`.

Los healthchecks reales son: `pg_isready` para PostgreSQL; prueba TCP al puerto interno 8080 para API; ninguno para frontend. El API check no demuestra que el esquema exista o que la conexión SQL funcione.

El comando Compose del frontend ejecuta `npm ci` antes de `ng serve` en cada arranque del contenedor. Para aislar artefactos Windows/Linux, fuentes se montan aparte de los volúmenes de `node_modules`, caché Angular, NuGet y `bin`/`obj` .NET.

```powershell
docker compose exec postgres sh -c 'pg_isready -U "$POSTGRES_USER" -d "$POSTGRES_DB"'
```

URLs: Angular `http://localhost:4200`; API `http://localhost:5010`; PostgreSQL desde host `localhost:5432`. No hay ruta `/health` definida en Compose. Angular usa el proxy Docker a `api:8080`.

## Migraciones y bootstrap

Compose no migra al arrancar. Para base nueva, tras esperar PostgreSQL healthy:

```powershell
docker compose run --rm api dotnet tool restore
docker compose run --rm api dotnet ef database update --project src/DentalPro.Infrastructure --startup-project src/DentalPro.Api
```

Con datos iniciales de Admin en `.env`:

```powershell
docker compose run --rm api dotnet run --project src/DentalPro.Api -- --bootstrap-security
```

Retirá esos valores y recreá API para retirar variables del contenedor:

```powershell
docker compose up -d --force-recreate api
```

En instalación persistida, inspeccioná primero `__EFMigrationsHistory` y respaldá. `database update` aplica pendientes; no reconstruye la base. Bootstrap crea Admin solo si no existe.

## Desarrollo local fuera de Docker

`DentalProDbContextFactory` lee `ConnectionStrings__DentalPro`, con fallback a localhost, puerto 5432 y `dentalpro_dev`. Configurá secretos solo en la sesión:

```powershell
$env:ConnectionStrings__DentalPro = 'Host=localhost;Port=5432;Database=<base>;Username=<usuario>;Password=<clave>'
$env:Jwt__SigningKey = '<base64 aleatorio de al menos 32 bytes>'
dotnet run --project src/DentalPro.Api --launch-profile http
```

API local escucha en 5010. Angular local:

```powershell
Set-Location .\frontend
npm ci
npm start
```

El proxy `proxy.conf.json` apunta a API local; `proxy.docker.conf.json` es solo para Docker. Así se mantienen llamadas `/api` sin CORS en desarrollo.

## Logs y detener

```powershell
docker compose logs -f postgres api frontend
docker compose logs --tail 100 api
docker compose stop
docker compose start
```

`docker compose down` elimina contenedores y red, conserva volúmenes. No ejecutar `docker compose down -v` si importan los datos. `dentalpro_postgres_data` está en el host local; Git no transfiere ese volumen a otra computadora.

## Actualizar

Revisá `git status --short` y protegé cambios propios. Para actualizar:

```powershell
docker compose down
git pull --ff-only
docker compose up --build -d
```

Conservá `.env`. Si hay nuevas migraciones, revisalas y ejecutá los dos comandos EF manualmente.
