# T25 — Revisión del código de plataforma Android

**Estado:** ✅ hecho (2026-07-02) · **Tipo:** revisión · **Esfuerzo:** S-M · **Depende de:** T9

## Por qué esta revisión

El código específico de Android con permisos y ciclo de vida delicado nunca se revisó: `FocusGuard`/`FocusGuardService` (bloqueo de apps durante el foco — permisos sensibles), `RealtimeService` (SignalR y su reconexión), y el `AndroidManifest`. Es código que, cuando falla, falla de formas difíciles de depurar (en segundo plano, tras reconexión, con la app matada por el SO).

## Qué revisar (checklist)

1. **`FocusGuard`/`FocusGuardService`:** ¿qué hace exactamente (bloquea apps, overlay, accessibility service)? ¿Qué permisos exige y son los mínimos? ¿Se libera bien al terminar/cancelar el foco, o puede quedar "trabado" bloqueando el teléfono si la app muere a mitad? (riesgo de UX muy malo).
2. **`RealtimeService` (SignalR):** ¿reconexión automática configurada? Tras perder red y volver, ¿re-suscribe los grupos (`RefreshGroups`)? ¿Se pierden o duplican eventos de presencia/Frenesí? ¿Qué pasa si el token expira durante una conexión viva?
3. **`AndroidManifest.xml`:** inventario de permisos declarados vs. usados (relevante para T14-Play Store y Data Safety). ¿Hay permisos que ya no se usan? ¿`SCHEDULE_EXACT_ALARM` para T2-B?
4. **Ciclo de vida y push FCM:** el push de Frenesí funciona; revisar el `FirebaseMessagingService`/`MainApplication` por fugas o handlers que asuman la app viva.
5. **`OnAppearing` async y navegación:** los `async void OnAppearing` (varios) que hacen red — ¿protegidos? ¿doble carga al volver a una pestaña?
6. **Compresión de imágenes en el hilo correcto:** `ResizeJpeg` (SkiaSharp) — ¿corre fuera del hilo UI para no congelar la captura del comprobante?

## Cómo

- Lectura de `Platforms/Android/*` (`FocusGuard*`, `MainActivity`, `MainApplication`, `AndroidNotificationService`, `WebAuthenticationCallbackActivity`) y `Services/RealtimeService.cs`.
- Pruebas en emulador/teléfono: cortar red durante Frenesí; matar la app durante un foco con guard activo; volver de segundo plano.

## Salida esperada

Lista de riesgos de plataforma (fugas, permisos de más, reconexión frágil) con severidad. El inventario de permisos se pasa tal cual a T14.

## Dependencias

- **T9** primero. Alimenta **T14** (permisos/Data Safety) y **T2-B** (alarmas exactas locales). #2 cruza con **T21** (reconexión SignalR en vivo).

## Resultado (2026-07-02) — lectura completa de `Platforms/Android/*` + `RealtimeService`; 2 fixes aplicados

### Fixes aplicados (mismo commit)
1. **(#2) Reconexión SignalR incompleta:** había `WithAutomaticReconnect` pero ningún handler `Reconnected` — tras un corte de red el server re-une a los grupos (nueva conexión → `OnConnectedAsync`) pero el **estado del semáforo se perdía**: un "Trabajando" volvía como default y el Frenesí se caía. Ahora al reconectar se invoca `RefreshGroups` + re-envía `SetStatus(_myStatus)`. *(Pendiente verlo en vivo en la prueba de 2 personas del dueño.)*
2. **(#6) `ResizeJpeg` en el hilo UI:** decode+resize de la foto del comprobante (~12 MP, SkiaSharp) congelaba la UI tras capturar — movido a `Task.Run`.

### Veredictos por ítem
- **(#1) FocusGuard/Service — BIEN DISEÑADO:** foreground service `specialUse` + polling UsageStats (1 s, hilo de fondo) + overlay; NO es AccessibilityService. No puede dejar el teléfono trabado: `NotSticky`, `intent==null → StopSelf` (sin guardián zombie tras reinicio), overlay y servicio mueren con el proceso, `OnDestroy` limpia todo; no actúa con pantalla bloqueada; la cámara del comprobante se exime con `Suspended`. Detalle menor: `FocusGuard` se suscribe a un evento estático en su constructor — correcto mientras sea singleton en DI (lo es).
- **(#3) Inventario de permisos (→T14):** INTERNET/ACCESS_NETWORK_STATE/POST_NOTIFICATIONS ✓ usados · PACKAGE_USAGE_STATS ✓ (guard; sensible, requiere declaración en Play) · SYSTEM_ALERT_WINDOW ✓ (overlay; sensible) · FOREGROUND_SERVICE(+SPECIAL_USE) ✓ · CAMERA ✓ (MediaPicker del comprobante) · **QUERY_ALL_PACKAGES ⚠️: Play lo rechaza sin justificación fuerte y es REEMPLAZABLE** por un bloque `<queries>` (intent MAIN/LAUNCHER) para el picker de apps — cambiar en T14 y verificar el picker en emulador. Faltará `SCHEDULE_EXACT_ALARM` para T2-B. Extras para T14: `android:allowBackup="true"` respalda las Preferences (incl. `SavedPassword` en claro → M4) y `usesCleartextTraffic="true"` global (dev; pasar a networkSecurityConfig).
- **(#4) FCM/ciclo de vida:** sin `FirebaseMessagingService` propio (Plugin.Firebase lo maneja); `MainApplication` solo loguea crashes (tag ANTIGRAVITY); `AndroidNotificationService` correcto — su `ScheduleNotification` inmediato ya está asignado a T2 (T18-8).
- **(#5) `async void OnAppearing` (×6 páginas):** patrón MAUI estándar (no hay alternativa awaitable); el riesgo es una excepción no capturada → crash. Queda como ítem transversal T20-I2 (cada página que se toque gana su try/catch en la carga).
