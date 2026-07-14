# T2 — Notificaciones (la voz de la app fuera de la app)

**Estado:** ✅ hecho (2026-07-03: A+D + racha nocturna server-side; B quedó innecesaria) · **Esfuerzo global:** M · **Depende de:** — (T1 y T7 le dan más contenido que anunciar)

## El quiebre (por qué)

Todo el diseño de castigo (hambre cada 2 h, muerte por abandono) ocurre **a espaldas del usuario**: la mascota sufre en silencio y el usuario lo descubre al abrir la app — es decir, el castigo nunca genera una apertura, solo culpa retroactiva. En un Tamagotchi, "tu mascota tiene hambre" ES el gancho de re-engagement. Hoy el único push real es el de Frenesí; el resto de la app es muda.

## Evidencia en el código

- `src/PetProductivity.Client/Services/NotificationService.cs` — stub: `Debug.WriteLine`, no notifica nada.
- `src/PetProductivity.Client/Platforms/Android/AndroidNotificationService.cs:41` — la versión Android real existe pero `ScheduleNotification` **muestra inmediato**, no difiere (no hay AlarmManager/WorkManager).
- `src/PetProductivity.Server/Services/PushService.cs:47` — `SendToUsersAsync` (FCM) **ya funciona con la app cerrada**, respeta `NotificationsEnabled`, prioridad alta para MIUI. Hoy solo lo usa el Frenesí.
- `src/PetProductivity.Server/Services/HealthDecayHostedService.cs:41-73` — el tick de decadencia ya recorre todas las mascotas cada 2 h: el lugar natural para detectar cruces de umbral.
- `User.NotificationsEnabled` ya existe y es editable desde Ajustes (`UsersController.cs:167-179`).

## Opciones

### A. Push server-side reutilizando `PushService` (eventos que el server ya conoce)
Los hosted services y controllers disparan push en el momento en que detectan el evento:
- Hambre cruza umbral (100→ <30) en el tick de decadencia → "Moko tiene hambre 🥺".
- HP empieza a bajar (hambre llegó a 0) → "Moko se está debilitando".
- Cristalización → "Moko se ha cristalizado…".
- Aprobación de grupo pendiente → push a los demás miembros (ver T6).
- **Pros:** cero trabajo cliente nuevo (FCM ya cablea todo, verificado en teléfono real); funciona con app cerrada; el server es la fuente de verdad del estado.
- **Contras:** MIUI bloquea FCM si el usuario **fuerza el cierre** (limitación conocida del SO, ya documentada en CLAUDE.md — no hay fix del lado app); no sirve para avisos ligados a la *hora local* del usuario si el server no conoce su zona (→ T8).
- **Esfuerzo:** S-M · **Toca:** solo server.

### B. Notificaciones locales programadas client-side
Implementar programación real: `Plugin.LocalNotification` (dependencia) o AlarmManager/WorkManager a mano en `AndroidNotificationService`. Casos: "tu racha muere en 3 h" (21:00 local si no hubo actividad), "te falta 1 celda para la línea del ritual" (20:00 local).
- **Pros:** hora local exacta sin que el server sepa la zona; sobrevive a MIUI mejor que FCM (alarma exacta local); no gasta cuota FCM.
- **Contras:** el cliente puede tener datos viejos ("tu racha muere" cuando ya hiciste una tarea desde otro dispositivo) — mitigable cancelando/reprogramando la alarma en cada apertura y en cada acción; más código de plataforma; Android 12+ pide permiso para alarmas exactas.
- **Esfuerzo:** M · **Toca:** solo cliente (Android).

### C. Híbrido A + B (cada aviso por el canal que le corresponde)
Server-push para eventos de estado del server (hambre, HP, cristalización, aprobaciones, Frenesí ya existente); local para recordatorios por hora del reloj (racha nocturna, ritual).
- **Pros:** cada mensaje usa el canal más fiable para su naturaleza; A y B se pueden hacer en fases (A primero, B después).
- **Contras:** dos sistemas que mantener.
- **Esfuerzo:** M (suma de A + B) · **Toca:** server + cliente.

### D. Política anti-spam (obligatoria, cualquiera sea la vía)
- Máximo **1 push por tipo por día** (guardar `LastNotified_<tipo>` por usuario, o un dict JSON `LastNotifications` — el patrón `ValueComparer` para diccionarios JSON ya existe en `AppDbContext`).
- Umbrales con histéresis: notificar al *cruzar* <30 de hambre, no en cada tick mientras siga bajo.
- Quiet hours (23:00–08:00 local — necesita T8 para saber la hora local).
- Respetar `NotificationsEnabled` (PushService ya lo hace) y, opcional, opt-out por tipo en Ajustes.
- **Por qué es parte del quiebre y no un extra:** una app que notifica de más se silencia o desinstala; el primer push molesto quema el canal para siempre.
- **Esfuerzo:** S sobre A · **Toca:** server (+ Ajustes si hay opt-out por tipo).

## Recomendación

**C con A primero** (push server de hambre/HP/cristalización/aprobaciones — es casi solo server y la infraestructura está verificada en producción), con **D desde el día uno** (el rate-limit por tipo va en el mismo commit que el primer push). **B** en segunda fase, cuando exista la racha real de T1 que anunciar de noche.

Mensajes en voz de la mascota ("Moko tiene hambre"), no del sistema ("Recordatorio: …") — el apego es el producto.

## Criterios de éxito / verificación

1. Con la app en segundo plano, dejar caer el hambre bajo 30 (usar `DevController` para acelerar) → llega exactamente 1 push, y no se repite en el siguiente tick.
2. Con `NotificationsEnabled = false` no llega nada.
3. Push de cristalización llega una sola vez.
4. (Fase B) A las 21:00 hora local sin actividad ese día → aviso de racha; si hubo actividad, no.
5. Ninguna notificación entre 23:00 y 08:00.

## Dependencias

- Quiet hours y avisos nocturnos precisos → **T8** (hora local).
- El aviso de racha → **T1**.
- El push de aprobación pendiente → **T6**.

## Resultado (2026-07-03) — A + D, y el aviso nocturno TAMBIÉN server-side (T8 lo desbloqueó)

- **D primero (`NotificationPolicy`):** opt-out + token, **quiet hours 23:00–08:00 locales** (LocalDay) y **1 push por tipo por día** (`User.LastNotifications`, dict JSON con ValueComparer; migración `AddNotificationLog`). Todo aviso pasa por aquí.
- **A (estado de la mascota):** el tick de decadencia detecta **cruces** (histéresis) y avisa en voz de la mascota: hambre <30 → "{nombre} tiene hambre 🥺"; hambre llegó a 0 → "se está debilitando 💔"; cristalización → "💎 se ha cristalizado". Prioridad: cristal > débil > hambre (1 aviso por tick).
- **Racha nocturna (era la fase B, pero sin cliente):** como T8 dio la hora local al server, `StreakReminderHostedService` barre cada 30 min y avisa "🔥 tu racha de N días está en juego" solo a quien hizo algo AYER y nada HOY, con su reloj local entre 20:00–22:59. **`Plugin.LocalNotification`/AlarmManager quedaron innecesarios** (cero dependencia nueva, cero permiso de alarmas); B se retoma solo si MIUI demuestra bloquear estos push en la práctica.
- **Verificación:** 12 tests (quiet hours, 1/tipo/día, opt-out, sin token, elegibilidad de racha ×4). Suite: **82 verdes**. La entrega física de FCM reutiliza `SendToUsersAsync` textual (ya verificado en teléfono real con el Frenesí); la prueba en dispositivo con app cerrada queda para la sesión de teléfono del dueño (criterios 1-3 en vivo).
- Pendiente de T6: el push de "aprobación pendiente" (usa esta misma política).
