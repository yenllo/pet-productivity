# T17 — Contrato compartido + helper HTTP (el refactor que paga T13)

**Estado:** ✅ hecho (2026-07-03: A+B; D/E siguen como oportunistas) · **Esfuerzo global:** M · **Depende de:** T9 · **Pre-requisito rentable de:** T13 (cola offline)

## El quiebre (por qué)

El proyecto `Shared` existe para compartir contratos entre server y cliente, pero el contrato real no lo usa: los controllers devuelven objetos anónimos y el cliente re-parsea a mano campo por campo, con DTOs espejo definidos en el propio cliente. Cada rename de propiedad es una rotura **silenciosa en runtime** (claves camelCase en strings que el compilador no ve). Encima, el cliente repite ~15 veces el mismo patrón URL + try/catch + `Console.WriteLine` (invisible en Android), así que los errores de red se tragan sin rastro y cualquier mejora transversal (retry, cola offline, logging) exige tocar 15 métodos en vez de 1.

## Evidencia en el código

- `src/PetProductivity.Server/Controllers/TasksController.cs:48-61` — `Ok(new { result.TaskId, ... })` anónimo; igual en todo `FocusController` (p. ej. `:99-109`, `:262`, `:273`).
- `src/PetProductivity.Client/Services/GameDataService.cs:122-141,327-334` — parseo manual con `JsonElement.TryGetProperty("xpEarned", ...)`.
- `GameDataService.cs:9-43` — DTOs espejo en el cliente (`GroupFocusInfo`, `ActiveGroupFocus`, `HistoryItem`) para formas que el server improvisa. `TaskResult` YA está en Shared y aun así se reconstruye a mano.
- `GameDataService.cs` — 15 repeticiones de `$"{_settingsService.ServerUrl.TrimEnd('/')}/api/..."` + `catch { Console.WriteLine(...) }`.
- `GameDataService.cs:435-489` — lógica de dominio del cuarto (`SeedPlacements`, `AutoPlaceAsync`, `FindFreeCell` con la grilla 6×6 y el centro (3,3) hardcodeados) dentro del servicio HTTP; el mismo conocimiento vive en `RoomGrid`/`RoomDiorama` → dos fuentes de verdad del layout.
- Contraste sano: `GroupsController` (104 líneas) delega a `GroupService`; `FocusController` (301) hace de servicio él mismo.

## Opciones

### A. Helper HTTP genérico en el cliente (el 80% del alivio)
Métodos privados `Task<T?> GetAsync<T>(string path)` / `Task<TRes?> PostAsync<TReq,TRes>(string path, TReq body)` que concentran: construcción de URL (base + path), serialización, manejo de error con `ILogger` real (no `Console.WriteLine`), y el punto único donde después se enchufan retry/cola de T13.
- **Pros:** mata ~150 líneas repetidas; T13 pasa de "tocar 15 métodos" a "tocar 1"; los errores dejan de desaparecer.
- **Contras:** algunos endpoints tienen matices (leer el body de error como mensaje, distinguir NeedsConfirmation) — el helper debe devolver algo más rico que `T?` en 2-3 casos (un `(T? data, string? error)`).
- **Esfuerzo:** S-M · **Toca:** solo cliente.

### B. DTOs de respuesta en Shared, end-to-end
Definir en `PetProductivity.Shared` los tipos de respuesta que hoy son anónimos (`FocusCompleteResponse`, `GroupFocusInfo`, `HistoryItem`, etc. — los espejo del cliente se **mueven**, no se reescriben), los controllers los devuelven tipados, y el cliente hace `ReadFromJsonAsync<T>` directo. `TaskResult` vuelve a ser el contrato real de `/api/tasks` (ya lleva casi todos los campos; añadir `EmotionalFeedback`).
- **Pros:** los renames vuelven a ser errores de compilación; desaparece todo el parseo `JsonElement`; el server deja de improvisar formas.
- **Contras:** hay que cuidar compatibilidad durante la transición (el JSON serializado no cambia si los nombres de propiedad se mantienen — verificar casing); toca controllers y cliente a la vez.
- **Esfuerzo:** M · **Toca:** shared + server + cliente.

### C. A + B por fases (recomendada)
Primero A (solo cliente, riesgo bajo, habilita T13); luego B endpoint por endpoint empezando por los de más tráfico (`/api/tasks`, `/api/focus/complete`).
- **Esfuerzo:** M total.

### D. Partir el god-object `GameDataService` (508 líneas, 4 responsabilidades)
Tras A/B queda natural: (1) el HTTP se reduce al helper; (2) `ResizeJpeg` → utilidad aparte o junto al flujo de proof; (3) `SeedPlacements`/`AutoPlace`/`FindFreeCell` → una clase de layout junto a `RoomGrid` (una sola fuente de verdad de la grilla 6×6); (4) `GameDataService` queda como caché de `CurrentUser` + fachada.
- **Pros:** el conocimiento del cuarto deja de estar duplicado justo antes de que F4 lo haga crecer.
- **Contras:** movimiento de código con el diorama aún caliente en el working tree — hacerlo DESPUÉS de T9 y de que F1-F3 estén desplegadas.
- **Esfuerzo:** M · **Toca:** cliente.

### E. Extraer `FocusService` del `FocusController`
Mover la lógica de sesiones/proof/foco grupal a un servicio, dejando el controller como binding+auth (patrón `GroupsController`→`GroupService` ya existente).
- **Pros:** T11-D1 y T13 van a tocar exactamente ese código; testearlo sin `ControllerBase` se vuelve posible (T15).
- **Contras:** valor sobre todo futuro; para un dev solo, hacerlo **de paso** cuando T11-D1 abra el archivo, no como tarea aparte.
- **Esfuerzo:** M · **Toca:** server.

## Recomendación

**C (A→B)**, con **D** y **E** como refactors *oportunistas*: D al cerrar el ciclo del diorama, E en la misma pasada de T11-D1. No hacer D/E "en frío" — son mejoras de forma que solo pagan si se hacen cuando ese código ya está abierto por otra razón.

## Criterios de éxito / verificación

1. Cero `JsonElement`/`TryGetProperty` en `GameDataService` (grep).
2. Una sola aparición de `ServerUrl.TrimEnd` en el cliente (el helper).
3. Renombrar una propiedad de un DTO compartido rompe la compilación del cliente (prueba del contrato: hacerlo y revertirlo).
4. Recorrido completo en emulador contra server local: tarea, foco+proof, tienda, grupos — sin regresiones de deserialización.
5. Un fallo de red aparece en el log del cliente con el endpoint y el error (no silencio).

## Dependencias

- **T9** primero (GameDataService está modificado en el working tree).
- **T13** debe construirse SOBRE el helper de A (si T13 llega antes, se paga dos veces).
- **T11-D1** es la ocasión natural para E.

## Resultado (2026-07-03) — C (A+B en la misma pasada)

- **A (helper HTTP):** `GameDataService` reescrito sobre 3 helpers privados (`GetAsync<T>`, `PostAsync<T>`, `PostAsync` ok/error) con **`ILogger` real** (adiós `Console.WriteLine` invisible — T20-I3 pagado aquí) y URL construida en UN solo lugar. Los ~15 try/catch repetidos desaparecieron; T13 ahora se enchufa en 1 punto. Criterios 1-2 ✓ por grep (0 `JsonElement`/`TryGetProperty`; 1 `TrimEnd` en GameDataService — AuthService conserva el suyo propio, fuera de este alcance).
- **B (contrato en Shared):** `/api/tasks` devuelve **`TaskResult`** directo (adiós objeto anónimo + parseo espejo); los DTOs espejo del cliente se MOVIERON a `Shared/Models/FocusDtos.cs` (`GroupFocusInfo`, `ActiveGroupFocus`, `HistoryItem`) + nuevos `FocusStartResponse`/`FocusCompleteResponse`/`ProofResponse`; todo `FocusController` devuelve tipado. Claves JSON idénticas a las anónimas (compatible con APK viejo).
- **Verificación:** smoke REST contra server local — login, `/api/tasks` (claves taskId/message/xpEarned/emotionalFeedback/needsConfirmation ✓), foco start (`sessionId`/`startedAt` ✓) + complete (`minutes…newTotalXp` ✓), history (claves intactas, `username` null en personal ✓). 91 tests verdes; server y cliente compilan.
- **🔥 Hallazgo colateral del smoke (hotfix `ea77a41`):** el login estaba **caído en producción** desde el deploy de T2/T10 — la migración `AddNotificationLog` dejó `LastNotifications=''` y `FromJson` lanzaba con string vacío → 500 en toda materialización de `User`. Arreglado (`FromJson` tolerante a ''/null para TODAS las columnas JSON) y verificado en prod (login 200). Moraleja: toda migración que agregue columna JSON necesita smoke de login inmediato.
- **D (partir GameDataService) y E (FocusService):** siguen como refactors oportunistas, según el plan.
