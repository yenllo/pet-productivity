# T10 — El server no puede asumir que siempre está despierto

**Estado:** ✅ hecho (2026-07-03, opción C: lazy como verdad + barrido best-effort) · **Esfuerzo global:** M · **Depende de:** T9 (commitear primero)

## El quiebre (por qué)

La decadencia —el corazón del castigo del juego— corre en un `BackgroundService` con tick cada 2 h. Eso solo funciona si el proceso vive sin interrupciones: si la instancia de Render duerme (plan free), se reinicia o se redeploya, los ticks no ocurren y **nadie lo nota** — las mascotas simplemente dejan de tener hambre. El estado del juego queda en función del uptime del hosting, que no es una garantía del diseño. Además, todos los push planificados en T2 (hambre, debilidad) heredan esta misma fragilidad.

## Evidencia en el código

- `src/PetProductivity.Server/Services/HealthDecayHostedService.cs:11,27` — tick fijo `FromHours(2)`; el primer `Task.Delay` significa que tras cada reinicio hay 2 h sin decadencia; los ticks perdidos durante el sleep **no se recuperan**.
- `HealthDecayHostedService.cs:47-50` — además carga TODAS las mascotas en memoria cada tick (hoy irrelevante, no escala).
- `src/PetProductivity.Server/Services/AffectionDecayHostedService.cs` y `FocusCleanupHostedService.cs` — mismo patrón, misma fragilidad (menos crítica: limpiar tarde es inocuo; decaer tarde cambia el juego).
- El modelo `Pet` no guarda cuándo se aplicó la última decadencia — no hay forma de reconstruir lo perdido.

## Opciones

### A. Decadencia lazy: aplicar lo acumulado al leer (recomendada)
Campo `LastDecayAt` en `Pet`. Un método puro `ApplyPendingDecay(pet, now)` computa cuántos ticks de 2 h pasaron desde `LastDecayAt` y aplica `n` veces la regla actual (−5 hambre; con hambre 0, −5 HP), respetando el escudo de gracia. Se invoca en los puntos donde la mascota se materializa: `GET /api/users/{id}`, `ProcessTaskCompletion`, detalle de grupo.
- **Pros:** matemáticamente idéntico al tick actual pero **independiente del uptime** — sobrevive reinicios, sleeps y redeploys; sin proceso que vigilar; el cálculo es puro y trivialmente testeable; sinergia directa con el "escudo de ausencia" de T3 (es un `if` dentro del mismo método).
- **Contras:** la decadencia solo "existe" cuando alguien mira — para los push de T2 ("Moko tiene hambre" con app cerrada) hace falta igualmente un barrido periódico (ver C); cuidado con aplicar dos veces en requests concurrentes (usar el `PetWriteLock` existente, mismo patrón que las compras).
- **Esfuerzo:** M · **Toca:** shared (campo + método) + migración + server (3-4 call-sites).

### B. Cron externo en vez de BackgroundService
Endpoint `POST /api/admin/tick` (protegido por secreto) invocado por un cron externo gratuito (cron-job.org, GitHub Actions schedule, o el Cron Job nativo de Render).
- **Pros:** despierta la instancia dormida (resuelve también el cold start del primer usuario del día); el tick queda observable (el cron registra fallos); poco código.
- **Contras:** dependencia operativa nueva fuera del repo; si el cron falla, se está igual que hoy pero con más piezas; no arregla la ventana de 2 h post-reinicio por sí solo.
- **Esfuerzo:** S-M · **Toca:** server (1 endpoint) + configuración externa.

### C. Híbrido: lazy como verdad + barrido ligero para efectos activos
A es la fuente de verdad del estado; se conserva UN hosted service ligero (o el cron de B) que cada 2 h llama al mismo `ApplyPendingDecay` para todas las mascotas **con dueño activo reciente** y dispara los push de T2 al cruzar umbrales.
- **Pros:** lo mejor de ambos: estado siempre correcto aunque el barrido falle, y los push salen a tiempo cuando el server está vivo; el barrido se vuelve *best-effort* en lugar de crítico.
- **Contras:** hay que garantizar idempotencia (el lazy y el barrido comparten `LastDecayAt`, así que no duplican por diseño, pero hay que testearlo).
- **Esfuerzo:** M · **Toca:** lo de A + refactor del hosted service.

### D. Pagar plan always-on de Render y no tocar nada
- **Pros:** cero código.
- **Contras:** dinero recurrente para tapar una fragilidad de diseño que seguirá ahí (reinicios y deploys siguen perdiendo ticks); no arregla la falta de `LastDecayAt`.
- **Esfuerzo:** 0 · Solo aceptable como paliativo mientras llega A.

## Recomendación

**C** (con A como núcleo): la decadencia lazy con `LastDecayAt` convierte el estado del juego en algo que no depende de que un proceso viva, y el barrido/cron queda solo para lo que genuinamente necesita iniciativa del server (los push de T2). Aplicar la misma revisión de criticidad a los otros dos hosted services: la limpieza puede seguir como está (llegar tarde es inocuo); el decaimiento de afecto merece el mismo tratamiento lazy si el afecto se vuelve visible en UI.

## Criterios de éxito / verificación

1. Test xUnit de `ApplyPendingDecay`: 0 ticks pendientes, 1, N acumulados, cruce hambre→daño, escudo de gracia activo, mascota cristalizada (no decae).
2. Simular server "dormido": poner `LastDecayAt` 12 h atrás en BD → al hacer `GET /api/users/{id}`, la mascota llega con 6 ticks aplicados exactos.
3. Requests concurrentes no duplican decadencia (lock + reload, mismo patrón que `ShopController.BuyItem`).
4. El barrido y el lazy no se pisan: correr el barrido y luego leer no aplica nada extra.
5. Reiniciar el server local a mitad de "día" no cambia el estado resultante.

## Dependencias

- **T9** primero (working tree limpio).
- Alimenta a **T2** (los umbrales de push se detectan en `ApplyPendingDecay`/barrido) y a **T3** (el escudo de ausencia es una condición dentro del mismo método).

## Resultado (2026-07-03) — opción C

- **`Pet.LastDecayAt`** (migración `AddLastDecayAt`; null = primer contacto → el reloj parte AHORA, sin decadencia retroactiva a mascotas pre-migración) + **`DecayMath.ApplyPendingDecay`** puro: ticks de 2 h acumulados, misma regla de siempre (−5 hambre; en 0, −3 HP), respeta el escudo de gracia, se detiene al cristalizar y conserva el resto (<2 h) del reloj. Las constantes se movieron de `HealthDecayHostedService` a `DecayMath` (compartidas).
- **Lazy en los puntos de materialización:** `GET /api/users/{id}` (y `/me`) y `ApplyRewardAsync` — siempre **dentro del `PetWriteLock` y tras el reload** (criterio 3: sin dobles aplicaciones concurrentes).
- **Barrido best-effort:** `HealthDecayHostedService` quedó como detector de umbrales para los push de T2 (cada 30 min), usando el MISMO `ApplyPendingDecay` y reloj → lazy y barrido no se pisan por diseño (criterio 4); ahora con lock+reload+save **por mascota** (antes podía pisar recompensas concurrentes — de paso se arregló ese lost-update preexistente). Si el barrido no corre, el estado sigue siendo correcto: solo se retrasan los avisos.
- **AffectionDecay/FocusCleanup:** siguen como estaban (llegar tarde es inocuo), según recomendaba el plan. El cron externo (B) quedó cubierto por el ping de UptimeRobot (T15-D) cuando el dueño lo active.
- **Verificación:** 8 tests de `DecayMath` (init, <2 h, 12 h dormido = 6 ticks exactos — criterio 2, resto conservado, hambre→daño, gracia, cristalizada, hasta-cristal-y-para). Suite: **91 verdes**.
