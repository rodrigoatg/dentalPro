# 10. Rutinas habituales de desarrollo

Antes de trabajar, revisá `git status --short`, requisito, modelo de dominio y ADR relacionado. Buscá una implementación paralela y usa el cambio mínimo.

## Agregar campo a Patient

1. Define semántica/obligatoriedad en dominio.
2. Actualiza `Patient` y sus pruebas.
3. Propaga a contracts Application, use case, API request/response.
4. Actualiza `PatientRow`, configuración y mapeos de `PatientRepository`.
5. Genera migración aditiva, inspecciona Up/Down/snapshot/script.
6. Actualiza interfaz Angular, formulario y tests.
7. Ejecuta unit, API, PostgreSQL si cambia esquema y Angular tests/build.

Error común: modificar tipo persistente y omitir rehidratación, response Angular o migración.

## Agregar regla

Colocá la regla en la entidad/value object que posee los datos. Comprobaciones de participantes/permiso externos pertenecen a Application. Testea API pública y también que el fallo no muta estado. No dupliques reglas de dominio en un endpoint por conveniencia.

## Agregar use case y endpoint

1. Define permiso, request/response y scope.
2. Crea contrato/excepción/use case en Application.
3. Crea implementación de repositorio Infrastructure solo si hace falta.
4. Registra DI en `InfrastructureServiceCollectionExtensions` y use case en `Program.cs`.
5. Añade Minimal API handler y DTO en API; usa `.RequirePermission`.
6. Pasa actor leído de claim `sub`, nunca del body.
7. Mapea excepciones conocidas con filtro y escribe tests de éxito, errores y falta de permiso.

Error común: poner reglas de negocio dentro de handler o confiar en guard Angular.

## Agregar tabla/migración

1. Revisa modelo/ADR y relaciones.
2. Crea Row y `IEntityTypeConfiguration` en Infrastructure.
3. Añade DbSet si corresponde y repositorio/mapeo.
4. Desde raíz:

```powershell
dotnet tool restore
dotnet ef migrations add NombreDescriptivo --project src/DentalPro.Infrastructure --startup-project src/DentalPro.Api
```

5. Lee `Up`, `Down`, Designer y snapshot; emite script de inspección.
6. Prueba modelo y PostgreSQL real para provider-specific behavior.
7. Aplicá migración solamente como operación explícita, con destino confirmado y backup apropiado.

EF InMemory no verifica SQL PostgreSQL, trigger, lock ni GiST.

## Agregar permiso

Agrega definición en `PermissionCatalog`, asignación inicial en bootstrap, requirement del endpoint, chequeos de scope Application cuando aplique, pruebas RBAC/API y visibilidad Angular. Usa mínimo privilegio y actualiza matriz documentada.

## Agregar pantalla Angular

1. Route lazy en `app.routes.ts`.
2. Componente standalone `.ts/.html/.css`.
3. Modelo/servicio en `core/<módulo>`; usa `/api` relativo.
4. Carga, vacío, error, éxito, validación y accesibilidad.
5. Guard/control visual según permiso, recordando que backend es autoridad.
6. Tests de componente/servicio y prueba de proxy local/Docker.

## Agregar filtro o modificar formulario

Inspeccioná endpoint y contrato antes de editar UI. Propaga query param a Application/repository y al servicio Angular. Testea coincidencias parciales, texto vacío, filtros combinados y que filtros cliente nunca amplíen scope. Conserva ID seleccionado, no solo etiqueta visible.

## Actualizar documentación

Si cambia regla estable, actualiza requisitos/modelo y crea o modifica ADR. Luego corrige guía técnica, Mermaid, tablas y ejemplos. Distingue capacidades actuales de ideas futuras.

## Ejercicios para aprender progresivamente

1. **Trazado sin editar:** sigue `PatientsApiService.list` desde `patient-list.ts` hasta endpoint, `ListPatients`, `PatientRepository.SearchAsync` y la consulta SQL que EF genera. Anotá los archivos y el contrato de cada frontera.
2. **Cambio de interfaz aislado:** modifica un texto/estado vacío de una pantalla y añade/actualiza su spec. Comprueba que no haya cambiado la llamada API.
3. **Extensión de consulta:** agrega un filtro no destructivo a un listado. Empieza en contrato Application, usa una consulta parametrizada en repository, propaga el query param al servicio Angular y prueba valor presente/vacío.
4. **Regla Domain pequeña:** agrega un comportamiento aprobado a una entidad (no una regla inventada), crea tests para éxito, rechazo y estado que permanece igual al fallar.
5. **Ruta protegida:** implementa un endpoint de lectura con permiso existente; prueba 401, 403, 200 y confirma que el actor venga del claim.
6. **Persistencia local descartable:** modela una tabla de práctica, crea migración, compara `Up`, `Down`, snapshot y SQL; aplica únicamente a una base throwaway y prueba rollback allí.
7. **RBAC completo:** introduce un permiso justificado, conecta catálogo, asignación bootstrap, endpoint, use case, UI y pruebas; revisa mínimo privilegio.
8. **Recorrido vertical nuevo:** toma una acción de agenda, dibuja secuencia UI→HTTP→Application→Domain→Infrastructure→DB y ubica el test de cada transición.

Subí dificultad de un ejercicio a otro y mantené un registro de decisiones. En los ejercicios de base de datos, prepara un destino aislado antes de ejecutar cualquier comando que escriba.
