# Índice — Planes de mejora

Origen: análisis de julio 2026 en cuatro pasadas — **engagement** (T1-T8: "la app castiga bien pero invita mal"), **salud técnica** (T9-T16: "el diseño del juego es más maduro que la infraestructura"), **calidad de código** (T17-T20: contrato, código muerto, constantes, consistencia) y **revisiones pendientes** (T21-T28: lo que los análisis estáticos no alcanzaron a mirar). Cada plan documenta un quiebre, TODAS las vías de solución con pros/contras/esfuerzo, una recomendación y criterios de verificación. Se ejecutan 1 a 1; al empezar una, actualizar su **Estado** aquí y en el archivo.

> **Arranque:** T9 ✅ → T22 ✅ → T28 ✅ → T21+T11 ✅ → T12 ✅ → T18 ✅ → T15+T19 ✅ → T26 ✅ → T24 ✅ → T25 ✅ → T23 ✅ → T8 ✅ → T1 ✅ → T2 ✅ → T6 ✅ → T10 ✅ → T17 ✅ → T13 ✅ → T5 ✅ → T7 ✅ → T16 ✅ → T3 ✅ — quedan **T27 → T4 → T14**, las tres necesitan al dueño (sesión UX, diseño del endgame, decisión de publicar).

> **Política de consolidación (T16-1a, vigente):** no se abre ningún sistema nuevo (mecánica, pantalla mayor, integración) mientras queden planes abiertos aquí. Toda idea nueva se **anota en esta carpeta como candidata** antes de existir en código — este índice es el único backlog. Regla de idioma (T16-2a): *todo string que un usuario pueda leer nace en español*; las claves internas (stats, datos en BD) quedan en inglés y el cliente traduce solo para mostrar (`PetVisuals.StatDisplayName`).

## Serie A — Engagement (por qué volver a abrir la app)

| # | Plan | Quiebre | Esfuerzo | Estado |
|---|------|---------|----------|--------|
| T1 | [Racha diaria real](01-racha-diaria.md) | La racha es un contador de tareas disfrazado | M | ✅ hecho (2026-07-03): racha real unificada (tarea/foco, día local T8) + congelador 200 oro auto-consumible + chip 🔥 con "en riesgo" en Dashboard; 7 tests, 70 verdes; hitos (E) diferidos a arte |
| T2 | [Notificaciones](02-notificaciones.md) | El castigo ocurre a espaldas del usuario | M | ✅ hecho (2026-07-03): push de hambre/debilidad/cristal con histéresis + aviso nocturno de racha server-side (20-23h locales) + política anti-spam (quiet hours, 1/tipo/día); 12 tests, 82 verdes; entrega física en teléfono = sesión del dueño |
| T3 | [Fénix / resurrección](03-fenix-resurreccion.md) | El usuario caído enfrenta la barrera más alta | M-L | ✅ hecho (2026-07-03): escudo de ausencia (>3 días sin actividad = decadencia dormida y perdonada) + vía acumulativa (3 grietas en días distintos con foco/tarea 5+ → revive) + hazaña 9+ intacta; contador 💠 en el overlay; 7 tests (108 verdes) + E2E con IA real; sprites de grieta → arte F4, vía social → futuro |
| T4 | [Endgame / generaciones](04-endgame-generaciones.md) | El juego se acaba en ~2 semanas | L | 🟡 opción E hecha (2026-07-03): ceremonia de evolución (overlay ✨ dirigido por etapa persistida) + push del hito vía T2; verificado en emulador; A/B/C/D (generaciones, drops, niveles, logros) necesitan arte F4 y decisión del dueño |
| T5 | [Mascota expresiva](05-mascota-expresiva.md) | Barras muertas no generan cariño (Happiness es fake) | M | ✅ hecho (2026-07-03): Pet.Condition derivado (umbral hambre = push T2) + barra Felicidad→Salud real + burbuja ✨/🥺/💔 en Dashboard y grupo + saltito al subir XP; 7 tests (98 verdes) + verificado en emulador; sprites por humor → F4 (arte) |
| T6 | [Validación familiar](06-validacion-familiar.md) | Esfuerzo de grupo que expira en silencio | S-M | ✅ hecho (2026-07-03): auto-aprobación a 48h (el silencio otorga, idempotente con el lock de T11) + push "tu familia espera" (política T2) + countdown ⏳ en la UI; 83 tests verdes |
| T7 | [Meta del día](07-meta-del-dia.md) | Buzón de tareas sin objetivo diario; ritual sin significado | M | ✅ hecho (2026-07-03): celdas del ritual nombrables (RitualLabels + ✏️ en UI) + quick-log "Repetir de un toque" (4 chips del historial, tap→IA+recompensa) + tablero sincronizado al abrir; 3 tests regresión ritual (101 verdes) + verificado en emulador |
| T8 | [Día local (timezone)](08-dia-local-timezone.md) | El "día" cambia a las 8-9 pm de Chile | S-M | ✅ hecho (2026-07-03, opción A): helper `LocalDay` + `User.TimeZoneId` (IANA del cliente, migración AddUserTimeZone) + 3 call-sites migrados; 7 tests de bordes/DST; 63 verdes |

## Serie B — Salud técnica (que el juego no pierda ni duplique nada)

| # | Plan | Quiebre | Esfuerzo | Estado |
|---|------|---------|----------|--------|
| T9 | [Commitear el working tree](09-commitear-working-tree.md) | Semanas de trabajo sin commit (riesgo #1) | S | ✅ hecho y **desplegado** (commits bd86a77..0aff606; push a Render 2026-07-02; ramas sobrantes borradas) |
| T10 | [Decadencia lazy](10-decadencia-lazy.md) | El estado del juego depende del uptime de Render | M | ✅ hecho (2026-07-03): Pet.LastDecayAt + DecayMath puro aplicado al materializar (bajo lock) + barrido best-effort solo para push; 8 tests, 91 verdes |
| T11 | [Bucle tolerante a fallos](11-bucle-tolerante-a-fallos.md) | 4 defectos: foco pierde recompensa, doble aprobación, TaskId falso, fallback texto plano | S-M | ✅ hecho (2026-07-02): D1 transacción, D2 lock por approvalId (+voto perdido), D3 id real, D4 fallback borrado (BD: 0 legados); test concurrente nuevo, 32 verdes |
| T12 | [Una llamada a la IA](12-una-llamada-ia.md) | Doble latencia y costo por tarea; feedback en inglés | S | ✅ hecho (2026-07-02): juicio+feedback en 1 llamada (campo `feedback` en el JSON del juez, en español, con fallback local); verificado en vivo con logs; 34 tests verdes |
| T13 | [Cola offline](13-cola-offline.md) | Sin señal, el texto del usuario se pierde | M | ✅ hecho (2026-07-03): ping /health + retry 2s + cola Preferences (tope 20, TTL 48h) solo tarea/focus-complete; drena al abrir, al volver la red y re-drenado 60s (cold start con wifi); 5 criterios verificados en emulador; de paso fix: AuthService ya no borra el token por fallo de red (mataba sesiones Google offline) |
| T14 | [Camino a Play Store](14-camino-play-store.md) | Privacidad de fotos sin declarar, billing pendiente, targets fantasma · **🔴 C0: endurecer auth (rotar JWT, token+refresh+revocación, A1/M4/M5) es BLOQUEANTE de lanzamiento** | M-L | 🟡 **C0 HECHO** (2026-07-09, commits 02db63e/6fe9d26): access 60 min + refresh rotatorio revocable (tabla RefreshTokens), tokens en SecureStorage, contraseña fuera del disco (con migración one-shot de sesiones legadas), Google por código de un solo uso (M5), cleartext solo dev (M4), targets fantasma fuera (C3); 116 tests verdes + flujo verificado end-to-end. **C1 HECHO** (2026-07-10, commit bd31b20): política bilingüe en `/privacidad.html`, `DELETE /api/users/me` (reusa LeaveGroup; 4 tests) con doble confirmación en Ajustes → Cuenta, consentimiento de foto la 1ª vez; 120 tests verdes + verificado en emulador. Falta: C4 trámites Play Console (cuenta del dueño, USD 25), C2 billing (lanzar sin premium) |
| T15 | [Tests de economía + observabilidad](15-tests-economia-observabilidad.md) | La cadena de multiplicadores no tiene red; los 500 se conocen por el usuario | M | ✅ hecho (2026-07-02): RewardMath + tabla 17 casos (51 verdes, mutación verificada), CI GitHub Actions, Sentry gated a DSN, /health+instrucciones UptimeRobot. Pendiente dueño: cuentas Sentry/UptimeRobot |
| T16 | [Consolidación + idioma](16-consolidacion-idioma.md) | Superficie enorme y voz mezclada ES/EN | S-M | ✅ hecho (2026-07-03): política de consolidación anotada arriba + barrido ES (errores de controllers, defaults "Egg"→"Huevo", stats traducidas solo-display vía PetVisuals.StatDisplayName — claves BD/IA intactas); 101 tests verdes |

## Serie C — Calidad de código (que sea barato de leer y extender)

| # | Plan | Quiebre | Esfuerzo | Estado |
|---|------|---------|----------|--------|
| T17 | [Contrato compartido + helper HTTP](17-contrato-compartido-http.md) | Objetos anónimos + parseo JsonElement a mano; URL+try/catch repetido ×15 | M | ✅ hecho (2026-07-03): helper HTTP único con ILogger + DTOs en Shared (TaskResult directo, FocusDtos); claves JSON intactas, smoke REST ✓; de paso hotfix `ea77a41` (login de prod caído por columna JSON vacía de la migración T2) |
| T18 | [Purga de código muerto](18-purga-codigo-muerto.md) | UnlockedSkins, AddGold, UserId fantasma en DTOs, CreateUserRequest… | S | ✅ hecho (2026-07-02): ítems 1-5/7/9/10 borrados (incl. migración DropPetUnlockedSkins y EmotionalSupportService entero); 6→T5, 8→T2; 34 tests verdes |
| T19 | [Constantes de economía + efectos declarativos](19-constantes-economia-efectos.md) | Números mágicos regados; la Poción cura por substring del nombre | S-M | ✅ hecho (2026-07-02): consts nombradas (RewardMath/HealthDecay/Pet) + efectos declarativos (`effect` en info.json, switch en BuyItem); curación +50 verificada en vivo |
| T20 | [Consistencia menor (transversal)](20-consistencia-menores.md) | 3 estilos de error, async void sin red, Console.WriteLine, CurrentUser nullable | S repartido | pendiente |

## Serie D — Revisiones pendientes (lo que la lectura estática no alcanzó)

| # | Plan | Qué falta revisar | Esfuerzo | Estado |
|---|------|-------------------|----------|--------|
| T21 | [Verificación dinámica](21-verificacion-dinamica.md) | Reproducir los bugs de T11 en vivo antes de arreglarlos | M | ✅ hecho (2026-07-02): D1/D3/voto-perdido reproducidos y re-verificados tras el fix; doble premio no cae (guard accidental) pero el race daba 7×500; decadencia confirmada por código (→T10); cold start Render 22.7s (→T13); reconexión SignalR →T25 |
| T22 | [Seguridad](22-seguridad-PROMPT-CHAT-NUEVO.md) · [**INFORME**](22-seguridad-INFORME.md) | Auditoría completa — IDOR, auth, DoS, prompt injection, secretos | L | ✅ informe + **arreglos server-side hechos** (A2/A3/M1/M2/M3/M7/B3/B4/B8 + C1-piso; commits `52cb719`..`70f02a9`) + **C1: secreto JWT rotado y verificado (2026-07-02)**. Pendiente para el lanzamiento: token corto+refresh+revocación, A1/M4/M5 (cliente) → ver T14-C0 |
| T23 | [Rendimiento y base de datos](23-rendimiento-base-datos.md) | Índices, N+1, dedupe en memoria, cascadas de borrado | M | ✅ hecho (2026-07-02): migración AddHotPathIndexes (9 índices; Email y JoinRequests únicos — cierran carreras de registro/doble solicitud); sin N+1; sin cascadas → borrado de cuenta limpia a mano (T14); bytea de fotos OK; dedupe en memoria diferido a ~500 usuarios |
| T24 | [Capa social + auth interna](24-capa-social-auth-interna.md) | GroupService y flujos de auth por dentro (corrección, no seguridad) | M | ✅ hecho (2026-07-02): 4 fixes (voto fantasma en approvals al salir, unanimidad pasiva join/hatch, locks de voto, rama muerta upgrade) + 8 veredictos anotados (token 30d→T14-C0, aviso invitado-Google→T27, decay de compartidas = decisión dueño→T10); 56 tests verdes |
| T25 | [Plataforma Android](25-plataforma-android.md) | FocusGuard, reconexión SignalR, permisos del manifest | S-M | ✅ hecho (2026-07-02): 2 fixes (re-sync de estado al reconectar SignalR; ResizeJpeg fuera del hilo UI) + FocusGuard auditado (sin riesgo de trabado) + inventario de permisos para T14 (⚠️ QUERY_ALL_PACKAGES reemplazable, allowBackup, cleartext) |
| T26 | [Balance numérico](26-balance-economia-numerico.md) | Oro/día vs. precios, ritmo de evolución (hoja de cálculo) | S-M | ✅ hecho (2026-07-02): informe con veredicto por métrica — hallazgos clave: Master en ~3 días (meta 2-4 sem → umbrales 50/600/2500), cristal a ~3.3 días reales, foco sufre rendimientos decrecientes injustamente, congelador T1-D a 200 oro, T4 no urgente |
| T27 | [UX: recorrido de la app](27-ux-recorrido-app.md) | Fricción real navegando (mejor en sesión con el dueño) | M | 🟡 pre-pase autónomo hecho (2026-07-03): checklist objetivo revisado por código + fix del cold-start de PetDetailPage (spinner + toast de error); la sesión guiada "de sentir" con el dueño queda pendiente |
| T28 | [Docs vs. realidad](28-docs-vs-realidad.md) | ROADMAP/CLAUDE.md/docs desactualizados vs. estado real | S | ✅ hecho (2026-07-02): CLAUDE.md/ROADMAP al día (.NET 10, diorama+foco, comandos, pendientes 🟡 depurados); `docs/` se queda (plan+specs del diorama, con addendum de estado); DESIGN.md ya tenía banner histórico; sin contradicción tareas↔roadmap (único solape: PLAN-diorama F5.4 billing ⊂ T14) |

## Serie E — Banco de ideas (no son tareas, son candidatas)

| # | Plan | Qué es | Estado |
|---|------|--------|--------|
| T29 | [Ideas futuras](29-ideas-futuras.md) | Candidatas sin implementar, alimentadas por revisión continua de código/emulador (drag de muebles, estilo del popup de foto, monetización más allá de T14, comentario engañoso en `ShopController.SavePlacements`) | vivo — no consume el orden global, se mira al retomar T27/T4/T14 |

## Orden global sugerido

**T9 → T22(seguridad, chat nuevo) → T28 → T21+T11 → T12 → T18 → T15(+T19-A) → T19-C → T26 → T24 → T25 → T23 → T8 → T1 → T2 → T6 → T10 → T17 → T13 → T5 → T7 → T16 → T27 → T3 → T4 → T14**
(T20 no se agenda: transversal — cada tarea que abra uno de sus archivos paga su ítem.)

> Las revisiones (Serie D) se intercalan donde alimentan a otras: **T21 con T11** (reproducir→arreglar), **T26 antes** de tocar balance (T1-D/T4), **T24/T25 cerca de T22** (comparten superficie), **T27 después** de T12/T2 (juzgar el estado ya mejorado), **T23** cuando la carga lo pida.

1. **T9 antes que todo**: nada se edita con semanas de trabajo sin commit.
2. **T11 + T12** — arreglos pequeños del bucle central; T11-D2 debe estar listo antes de la auto-aprobación de T6; T11-D1 es la ocasión para T17-E (FocusService).
3. **T18** — una tarde de borrado; menos código hace más barato todo lo que sigue.
4. **T15 (tabla de economía + CI) con T19-A en el mismo PR** (extraer RewardMath = nombrar las constantes) — la red debe estar puesta antes de tocar balance. **T19-C** (efectos declarativos) enseguida, antes de que T1-D cree el segundo consumible.
5. **T8 → T1 → T2 → T6** — el núcleo de retención, en su orden de dependencia (día local → racha → avisos → aprobaciones).
6. **T10, T17, T13** — fiabilidad de fondo; T17 (helper HTTP) va inmediatamente ANTES de T13, que se construye sobre él.
7. **T5, T7, T16** — profundidad y coherencia.
8. **T3, T4** — las de más diseño/arte (T3 pierde urgencia cuando T2 avisa antes de la muerte).
9. **T14** cuando exista decisión firme de publicar (sus trámites corren en paralelo a todo).

## Dependencias cruzadas

- **T9 → todas**: working tree limpio primero.
- **T8 → T1, T2, T7**: define "hoy" (helper único de día local).
- **T1 → T2, T3**: `LastActivityDate` alimenta el aviso nocturno de racha y el escudo de ausencia del Fénix.
- **T2 → T3, T6**: los push de "se debilita" y "aprobación pendiente" usan la infraestructura y política anti-spam de T2.
- **T11-D2 → T6**: la auto-aprobación multiplica la concurrencia; arreglar el doble premio antes.
- **T11-D1 → T17-E**: extraer FocusService en la misma pasada que abre FocusController.
- **T15-A + T19-A = un solo PR**: extraer el cálculo Y nombrar sus constantes; la tabla de tests referencia las consts.
- **T15-A → T1, T6, T7**: la tabla de economía es el contrato que esos cambios de balance deben actualizar primero.
- **T19-C → T1-D**: el congelador de racha debe nacer como efecto declarativo del catálogo.
- **T17-A → T13**: la cola offline se enchufa en el helper HTTP único (si T13 va primero, se paga dos veces).
- **T17 resuelve de paso** T20-I3 (Console.WriteLine) y T20-I6 (CurrentUser nullable); **T18** coordina con T17 los DTOs fantasma (ítems 3-4).
- **T10 ↔ T2/T3**: la decadencia lazy detecta los umbrales de push y contiene el escudo de ausencia.
- **T12 → T16 y T18**: fusionar la llamada arregla el string en inglés más visible y deja muerto `GenerateFeedbackAsync` (T18-7).
- **Diorama F4 (plan existente) → T4, T5, T3, T20-I5**: estatuas, sprites de humor, cristal agrietado y el umbral de tamaño de RoomDiorama.
- **T2 y T15-D reutilizan infraestructura existente** (`PushService` verificado en producción; ping externo compartido con T10-B) — no construir segundos canales.
