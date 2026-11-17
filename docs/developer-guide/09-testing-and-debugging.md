# 9. Pruebas y diagnóstico

## Qué prueban las suites

- `DentalPro.UnitTests`: dominio y Application con doubles; no necesita servidor HTTP ni PostgreSQL.
- `DentalPro.IntegrationTests`: API mediante `WebApplicationFactory`, auth/RBAC, administración, modelo EF y algunas rutas con proveedor InMemory.
- Hechos `PostgreSqlAppointmentFact` requieren `DENTALPRO_TEST_POSTGRES_CONNECTION_STRING`; si no está definido el test se omite con mensaje que pide base PostgreSQL 17 aislada.
- Angular specs viven junto a servicios/componentes; builder de test se configura en `angular.json`.

EF InMemory sirve para pruebas de lógica/modelado parcial; no valida exclusion constraints GiST, triggers, locks, `FOR UPDATE`, SQLSTATE ni transacciones PostgreSQL. Para esas garantías hace falta PostgreSQL real aislado. No apuntes las pruebas a `dentalpro_dev`.

## Comandos exactos

Desde raíz:

```powershell
dotnet test tests/DentalPro.UnitTests/DentalPro.UnitTests.csproj
dotnet test tests/DentalPro.IntegrationTests/DentalPro.IntegrationTests.csproj
dotnet test DentalPro.sln
dotnet build DentalPro.sln
```

Desde `frontend`:

```powershell
npm ci
npm test -- --watch=false
npm run build
```

Pruebas PostgreSQL exigen configurar la variable sobre una base de pruebas aislada y preparada. La guía no afirma que se hayan ejecutado ni que Docker esté disponible.

## Diagnóstico

### API no arranca

```powershell
docker compose ps
docker compose logs --tail 150 api
```

Verificá signing key, connection string, API escuchando 8080 y Postgres healthy. Si API dice que falta relation/table, podría faltar migración: healthcheck solo revisa TCP.

### Frontend no conecta

Revisá `frontend` logs, proxy según modo, URL `/api`, puertos 4200/5010 y autenticación. Dentro de Compose el DNS es `api`; Windows/navegador no resuelven ese nombre. El browser usa el puerto publicado de Angular.

### HTTP

- 401: sesión ausente/inválida/expirada; interceptor limpia la sesión.
- 403: usuario autenticado sin permiso o scope permitido.
- 404: ruta o entidad no encontrada.
- 400: request inválido o regla de horario/estado mapeada como validación.
- 409: duplicado o conflicto de reserva, según endpoint/filtro.

Consultá el filtro del módulo y los API tests para confirmar título/cuerpo exacto; errores no controlados no deben divulgar internals.

### Datos/concurrencia

Logs, connection string dentro del contenedor, `__EFMigrationsHistory` y estado de servicio ayudan. Si falla concurrencia, probá PostgreSQL real: un fake no reproduce locks ni exclusión GiST. Investiga UoW, adquisición de lock y constraint antes de aumentar timeouts.

## Agregar test

1. Invariante pura → UnitTests.
2. Coordinación y casos de uso → UnitTests con doubles.
3. Bindings, permisos/status/DTO → IntegrationTests API.
4. SQL, constraints, transacción y concurrencia → PostgreSQL aislado.
5. Vista/servicio → spec Angular vecino.

No debilites pruebas existentes para forzar verde; marca dependencia externa claramente y conserva forma segura de ejecución.
