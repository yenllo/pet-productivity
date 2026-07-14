# T8 — Día local: el "día" cambia a las 8-9 pm de Chile

**Estado:** ✅ hecho (2026-07-03, opción A) · **Esfuerzo global:** S-M · **Depende de:** — (es la base de T1, T2 y T7)

## El quiebre (por qué)

Todo corte diario del server usa medianoche **UTC**. Para un usuario en Chile (UTC-3/-4), el "nuevo día" empieza a las 20:00-21:00 de la tarde: el ritual se resetea en plena noche útil, los rendimientos decrecientes se reinician antes de dormir, y la futura racha diaria se rompería aunque hicieras algo "hoy" a las 22:00. El usuario no ve un bug, ve una app que se siente arbitraria y rota justo en el horario de mayor uso de una app de productividad personal. Ninguna mecánica diaria seria (T1, T2, T7) puede construirse sobre un "hoy" equivocado.

## Evidencia en el código

Todos los cortes usan `DateTime.UtcNow.Date` (o ventanas UTC):

- `src/PetProductivity.Server/Services/PetService.cs:331` — reset del ritual (`ToggleRitualCell`).
- `src/PetProductivity.Server/Services/PetService.cs:148` — conteo de tareas "de hoy" para rendimientos decrecientes.
- `src/PetProductivity.Server/Services/PetService.cs:134` — dedupe: ventana rodante de 24 h (esta está bien — no depende de medianoche; **no tocar**).
- `src/PetProductivity.Server/Controllers/FocusController.cs:89-94` — `FocusStreak` compara fechas UTC.
- La futura `LastActivityDate` de T1 heredaría el problema si nace antes que esta tarea.

## Opciones

### A. Zona horaria del usuario, persistida y aplicada server-side (helper único)
`User.TimeZoneId` (IANA, ej. `America/Santiago`); el cliente lo manda al login/registro y cuando cambie (`TimeZoneInfo.Local.Id` en .NET ya da IANA en Android). Server: un helper único — `DateTime TodayFor(User u)` / `DateOnly LocalDate(User u)` usando `TimeZoneInfo.FindSystemTimeZoneById` — que reemplaza **cada** `UtcNow.Date` de la lista de arriba. Default si falta: `America/Santiago` (o UTC).
- **Pros:** correcto para siempre, incluido DST chileno (IANA lo maneja solo, nada de offsets fijos); anti-trampa razonable (cambiar la zona del teléfono es posible pero costoso y detectable si se quisiera); un solo punto de verdad para "hoy".
- **Contras:** migración + touch de login/registro en cliente; contenedor Linux (Render) necesita tzdata — los IDs IANA funcionan nativos en Linux, verificar solo que la imagen base los tenga.
- **Esfuerzo:** M · **Toca:** shared + server + migración + cliente (1 campo en login).

### B. El cliente manda su fecha local por request
Cada endpoint relevante recibe `ClientDate` del dispositivo.
- **Pros:** sin estado nuevo en BD.
- **Contras:** frágil y trampeable a voluntad (mandar la fecha que convenga para farmear rachas/resets); contamina las firmas de N endpoints en vez de 1 campo; los hosted services (decadencia, cleanup, futuros push nocturnos) **no tienen request** del cual leer la fecha — la opción ni siquiera cubre los casos de T2. Descartable.
- **Esfuerzo:** M (y deja deuda) · **Toca:** server + cliente en cada endpoint.

### C. Cutoff fijo hardcodeado (UTC-4)
Constante `DayOffset = -4h`: "hoy" = `(UtcNow - 4h).Date` en el mismo helper único.
- **Pros:** 1 línea + reemplazos, cero migración, cero trabajo cliente; para una base de usuarios 100% chilena es indistinguible de A el 95% del año.
- **Contras:** DST chileno (UTC-3 en verano) desplaza el corte a la 1 am — tolerable; se rompe con el primer usuario fuera de Chile; es deuda declarada.
- **Esfuerzo:** S · **Toca:** solo server.

## Recomendación

**A**, con **C como escalón intermedio válido** si se quiere desbloquear T1 esta semana: como ambas pasan por el mismo helper único, migrar de C a A después es cambiar la implementación del helper, no los call-sites. Lo innegociable es el **helper único desde el primer commit** — el quiebre real del código actual no es UTC, es que "hoy" está definido 4 veces en 4 archivos.

Inventario de reemplazo (checklist de implementación):
1. `PetService.ToggleRitualCell` — reset del ritual.
2. `PetService.ProcessTaskCompletion` — conteo diario de rendimientos decrecientes.
3. `FocusController.Complete` — racha de foco.
4. Futuro: `LastActivityDate` (T1), quiet hours y avisos nocturnos (T2), reset de meta del día (T7).
5. **No tocar:** dedupe de 24 h (ventana rodante, correcta como está) ni timestamps de almacenamiento (`CreatedAt` sigue en UTC — solo los *cortes de día* cambian).

## Criterios de éxito / verificación

1. A las 23:00 de Chile, completar una tarea cuenta para "hoy" (la racha/ritual no se resetean hasta la medianoche local).
2. El ritual completado a las 21:30 sigue completo a las 22:00 (hoy se borra al cruzar medianoche UTC).
3. Los rendimientos decrecientes se reinician a medianoche local, no a las 20-21 h.
4. Test xUnit del helper: casos borde (23:59 y 00:01 locales, cambio DST de abril/septiembre si es opción A).
5. `CreatedAt` de nuevos registros sigue siendo UTC (grep de regresión).

## Dependencias

- Ninguna entrante. **Salientes:** T1 (racha), T2 (quiet hours/avisos nocturnos), T7 (reset de meta) deben construirse sobre este helper — por eso esta tarea va primera en el orden sugerido.

## Resultado (2026-07-03) — opción A completa

- **Helper único `LocalDay`** (`Services/LocalDay.cs`): `ZoneFor` (IANA con fallback: vacío→`America/Santiago`, inválido→UTC sin 500), `TodayTokenFor` (etiqueta de día local, `Kind=Utc` porque Npgsql lo exige) y `StartOfTodayUtc` (instante real de la medianoche local, para filtrar `CreatedAt`).
- **`User.TimeZoneId`** + migración `AddUserTimeZone` (aditiva; usuarios existentes quedan en el default chileno). El cliente manda `TimeZoneInfo.Local.Id` en login/registro/upgrade (en .NET 10 los IDs Windows también resuelven vía ICU); el server lo sanea (≤64 chars) y lo refresca en cada login.
- **Los 3 call-sites migrados:** reset del ritual (`ToggleRitualCell`), rendimientos decrecientes (variable hoisted para que EF traduzca el filtro), racha de foco (`FocusController`). El dedupe de 24 h y los `CreatedAt` quedaron intactos (criterio 5 ✓, ventana rodante y almacenamiento siguen UTC).
- **Tests (7):** bordes 23:59/00:01 en invierno (UTC-4) y verano (UTC-3) chilenos, `StartOfTodayUtc`, zona desconocida→UTC, vacía→default. Suite: **63 verdes**.
- Transición: valores viejos (fecha UTC) vs. tokens locales difieren ≤1 día → a lo más un reset de ritual se salta una vez; sin corrupción.
- Google-only users sin login por contraseña quedan en el default hasta su próximo login/upgrade (anotado).
