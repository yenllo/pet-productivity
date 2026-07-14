# T15 — Red de seguridad: tests de la economía + observabilidad

**Estado:** ✅ hecho (2026-07-02: A+C+D+E; B pospuesta como estaba previsto) · **Esfuerzo global:** M · **Depende de:** T9 (y potencia a T1-T7)

## El quiebre (por qué)

La economía del juego —lo que un esfuerzo vale— encadena hoy ~6 multiplicadores (ritual ×1.2, plausibilidad /10, dedupe ×0.1, rendimientos decrecientes ×0.5/×0.25, frenesí ×2, foto ×2, fuera-de-contexto ×0.25) y **ningún test la cubre**: los 26 tests existentes prueban piezas periféricas (FocusMath, evolución, presencia), no `ProcessTaskCompletion`. Los planes T1-T7 proponen tocar justamente esa cadena; sin red, cada ajuste de balance es una regresión en potencia que nadie detectará hasta que un usuario note que su tarea "pagó raro". Y del lado operativo: no hay error tracking — los 500 de producción se conocen cuando el usuario los cuenta.

## Evidencia en el código

- `src/PetProductivity.Server/Services/PetService.cs:113-160` — la cadena completa de multiplicadores, con órdenes de aplicación que importan (la plausibilidad divide después del ritual; el frenesí no multiplica oro; la foto multiplica ambos).
- `tests/PetProductivity.Tests/` — 26 tests en 8 archivos; ninguno instancia `PetService` ni cubre la cadena de recompensa.
- `Program.cs`/logging — solo `ILogger` a consola de Render; sin Sentry/AppInsights, sin alertas; el endpoint de salud (si existe) no está monitorizado.
- Precedente útil: `ApproveTaskTests.cs` ya testea lógica de PetService-adyacente — el patrón de test con EF InMemory/SQLite-in-memory ya está resuelto en el repo (verificar cuál usa y reutilizarlo; regla "no SQLite" aplica a producción, no al harness de tests… si el patrón existente ya lo resolvió de otro modo, seguirlo).

## Opciones

### A. Test de tabla de la economía (recomendada, el 80% del valor)
Un solo archivo `RewardMathTests` con `[Theory]` + `[InlineData]`: (dificultad, plausibilidad, ritual, dedupe, nºtareas-hoy, frenesí, foto, contexto) → (XP, oro) esperados. ~15-20 casos que documentan el balance actual como contrato. Requiere o bien instanciar `PetService` con dobles (IA mockeada — `IAiService` ya es interfaz), o bien **extraer el cálculo puro** a una función estática `RewardMath.Compute(...)` (patrón ya usado: `FocusMath.cs` existe exactamente por esta razón) y testear eso sin BD.
- **Pros:** la extracción a `RewardMath` es además una mejora de diseño (el cálculo deja de estar entrelazado con I/O); los casos de tabla son la documentación viva del balance; T1-T7 se ajustan cambiando la tabla primero (TDD de balance).
- **Contras:** extraer el cálculo toca el método más delicado del server — hacerlo mecánicamente (mover líneas, sin "mejorar" nada en la misma pasada).
- **Esfuerzo:** M · **Toca:** server (extracción) + tests.

### B. Tests de integración del flujo completo
`WebApplicationFactory` + BD de test: POST /api/tasks de verdad, con auth y EF.
- **Pros:** cubre el cableado (auth, serialización, EF) que A no ve.
- **Contras:** infraestructura pesada (Postgres de test o compatibilidad InMemory con los tipos JSON del modelo), lenta, frágil; para un dev solo, el mantenimiento supera al beneficio hoy.
- **Esfuerzo:** L · Posponer hasta que A exista y haya un CI donde correrlos.

### C. Error tracking (Sentry, tier gratis)
SDK de Sentry en el server (3 líneas en `Program.cs` + DSN en config): excepciones no manejadas de producción con stack trace, agrupadas, con alerta por email. Opcional: también en el cliente MAUI (crashes reales de teléfono, hoy solo visibles vía `adb logcat`/crash.txt local).
- **Pros:** pasar de "me entero por el usuario" a "me entero antes que el usuario" cuesta una tarde; el tier gratis sobra para este volumen.
- **Contras:** una dependencia y un servicio externo más; cuidar que no se filtren descripciones de tareas/PII en los eventos (scrubbing configurado).
- **Esfuerzo:** S (server) / M (con cliente) · **Toca:** server (+ cliente opcional).

### D. Monitoreo de disponibilidad
UptimeRobot (o similar, gratis) contra un `GET /health` del server: aviso si Render se cae o duerme más de la cuenta. Sinergia con T10-B (el mismo ping externo puede ser el "cron" que despierta la instancia).
- **Pros:** 15 minutos de configuración, cero código si ya hay endpoint de health (verificar; si no, es 1 línea de `MapHealthChecks`).
- **Contras:** ninguno serio.
- **Esfuerzo:** S.

### E. CI mínimo (GitHub Actions)
`dotnet build` + `dotnet test` del server en cada push. Sin pipelines elaborados: un workflow de 20 líneas.
- **Pros:** los tests de A solo valen si corren solos; hoy dependen de que alguien se acuerde; Render despliega cada push — que al menos compile y pase tests antes.
- **Contras:** el build MAUI/Android en CI es lento y quisquilloso — **excluirlo** (CI solo server+shared+tests; el cliente se sigue probando en emulador).
- **Esfuerzo:** S.

## Recomendación

**A + C + D + E; B no (todavía).** Orden interno: E primero (para que A nazca ejecutándose sola), luego A, y C/D en cualquier momento (independientes). La extracción de `RewardMath` hacerla ANTES de empezar T1/T7, que quieren tocar esa cadena.

## Criterios de éxito / verificación

1. `dotnet test` pasa con la tabla de economía cubriendo: caso base, cada multiplicador aislado, y 3-4 combinaciones (foco+foto+frenesí, dedupe+dim, fuera-de-contexto confirmada).
2. Cambiar un multiplicador a propósito rompe exactamente los casos esperados (prueba del contrato).
3. Un `throw` de prueba en producción-dev aparece en Sentry con stack completo y sin PII de descripciones.
4. Apagar el server local dispara la alerta del monitor en <5 min.
5. Un push con test roto aparece en rojo en GitHub antes de que Render lo tome (o al menos simultáneamente — Render no espera al CI; opcional: desactivar auto-deploy y desplegar con deploy hook desde el workflow verde).

## Dependencias

- **T9** primero. **T1, T6, T7** (cambios de balance/aprobaciones) deberían construirse sobre la tabla de A.
- **T10** comparte el ping externo con D.

## Resultado (2026-07-02) — A+C+D+E, con T19-A/C en el mismo PR (commit cbe4c6f)

- **A:** cadena de multiplicadores extraída a `RewardMath.Compute` (pura, redondeos idénticos paso a paso) + `RewardMathTests` con **17 casos de tabla** (valores esperados en literales a propósito). Criterio 2 verificado por **mutación**: FrenzyXpMultiplier 2→3 rompió exactamente los 3 casos de frenesí y nada más. Suite total: **51 verdes**.
- **C:** ✅ `Sentry.AspNetCore 6.6.0` gated a `Sentry__Dsn` (sin la var = no-op; `SendDefaultPii=false`). `Sentry__Dsn` puesto en Render → activo. **Criterio 3 VERIFICADO end-to-end (2026-07-04):** endpoint temporal `/debug/boom` lanzó un 500 en prod, el evento apareció en el dashboard de Sentry, y el endpoint se quitó enseguida (prod limpio).
- **D:** ✅ `GET /health` (keep-warm). **2026-07-03: el dueño montó el monitor de UptimeRobot** (`https://petproductivity.onrender.com/health`, 5 min). **Verificado:** el cold start de 22s desapareció — health responde en ~0.6s (instancia caliente). Status page: `stats.uptimerobot.com/tDPW4ODznm`.
- **E:** `.github/workflows/ci.yml` (dotnet test en cada push; MAUI excluido). ⚠️ `startup_failure` persistente en 0s sin logs (workflow válido y registrado) → causa probable: **billing/entitlement de Actions en repo privado**. Con OK del dueño quedó **deshabilitado** (`gh workflow disable CI`) para frenar las notificaciones; reactivar con `gh workflow enable CI` tras revisar GitHub → Settings → Billing and spending → Actions (o hacer el repo público).
