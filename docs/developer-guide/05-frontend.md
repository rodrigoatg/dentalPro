# 5. Frontend Angular

## Estructura y arranque

`frontend/src/app` agrupa pantallas: `auth`, `dashboard`, `patients`, `appointments`, `clinical-records`, `admin`. `core` contiene servicios, modelos, autenticación, guards e interceptor. Cada página suele tener `.ts`, `.html`, `.css` y tests opcionales.

`main.ts` arranca `appConfig`; `app.config.ts` registra router, HttpClient con Fetch e `authInterceptor`. `app.routes.ts` declara rutas lazy con `loadComponent`. Login y áreas protegidas se diferencian mediante guards.

## Componentes, templates y estado

El componente TypeScript mantiene señales/estado y handlers, el template expresa renderizado/eventos y CSS diseño local. Angular moderno usa `@if`/`@for` en templates. No hagas HTTP en el HTML: delegalo al servicio. Las páginas deben tratar loading, error, vacío y éxito de forma visible.

## Servicios y modelos

Los modelos TypeScript describen contratos HTTP, no entidades EF. `PatientsApiService` usa `/api/patients`; `AppointmentsApiService` arma fechas/query params y rutas de mutación; `ClinicalRecordsApiService` construye URL anidada por patientId. HttpClient devuelve Observables RxJS; para que ocurra solicitud el observable debe consumirse/suscribirse.

Las llamadas usan rutas relativas `/api`; el proxy resuelve el host según entorno:

- `frontend/proxy.conf.json`: API local `localhost:5010`.
- `frontend/proxy.docker.conf.json`: API en red Docker `http://api:8080`.

El browser solo habla con Angular publicado; el hostname `api` no es DNS público ni de Windows. El proxy elimina necesidad de CORS en desarrollo.

## Formularios y errores

Frontend valida para feedback temprano, pero servidor repite validaciones esenciales. Interceptor resuelve errores globales 401/403. Para 400/404/409, componente puede presentar el Problem Details o errores por campo en lenguaje claro. No ocultes fallos ni asumas que un botón desactivado protege una operación.

## Autenticación en cliente

`AuthService` usa signals en memoria para token y usuario. Login hace POST `/api/auth/login`, conserva token y sigue con `GET /api/auth/me`. No usa localStorage, sessionStorage ni refresh token. `authInterceptor` añade `Authorization: Bearer ...`; 401 limpia sesión y redirige; 403 muestra mensaje.

`authenticatedGuard`, `permissionGuard` y `hasPermission` regulan navegación/controles para UX. La API realiza autorización real. Al agregar una acción sensible, hay que mantener las dos capas coherentes, con permiso exigido por backend.

## Tests y scripts

`package.json` define `start`, `build`, `watch`, `test`. `angular.json` define proxy local y builder Angular.

```powershell
Set-Location .\frontend
npm ci
npm test -- --watch=false
npm run build
```
