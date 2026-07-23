# AGENTS.md — PetProductivity

Contexto persistente para trabajar este proyecto con Claude Code. Léelo al inicio de cada sesión. El plan por fases y el modelo de datos están en `ROADMAP.md`. El `DESIGN.md` original es **documento histórico**: el código ya lo superó (ver más abajo). Este archivo manda sobre suposiciones.

## Qué es
App de productividad gamificada. El usuario describe en lenguaje natural una tarea que hizo; una IA la juzga (dificultad 1–10 + categoría) y entrega recompensa **dual**: *Stats* (hacen evolucionar a la mascota) y *Oro* (solo cosmético). Las mascotas evolucionan por etapas (Egg→Baby→Adult→Master) y según su stat dominante, con mecánica "Fénix" (mueren cristalizadas y reviven solo con una tarea de dificultad ≥9, con escudo de gracia de 24 h). Capa social prevista: grupos que comparten mascota, semáforo de disponibilidad y modo "Frenesí" cuando varios trabajan a la vez.

## Stack real (confirmado leyendo el código)
| Capa | Tecnología | Proyecto |
|---|---|---|
| Cliente móvil | **.NET 10 MAUI** (`net10.0-android`, `net10.0-windows10.0.19041.0`) | `PetProductivity.Client` |
| Servidor | **ASP.NET Core 10 Web API + EF Core** | `PetProductivity.Server` |
| Modelos compartidos | Class library .NET 10 | `PetProductivity.Shared` |
| Base de datos | **PostgreSQL** (alojado en **Supabase**) vía Npgsql | — |
| IA | **Google Gemini** (API en la nube) con fallback heurístico | `GeminiAiService` |
| Despliegue | Dockerfile (solo el servidor) → Heroku (`pet-productivity-c03ac5654dd2.herokuapp.com`), autodeploy on push desde `main` | — |

NO es Android nativo ni Kotlin ni C++. El `.sln` es de Visual Studio porque así vive el ecosistema .NET. MAUI es el sucesor de Xamarin. Migrado de .NET 8 a **.NET 10** en junio 2026 (Dockerfile en `sdk:10.0`/`aspnet:10.0`; breaks resueltos: Swashbuckle→OpenAPI nativo, SkiaSharp 4.x, Plugin.Firebase 4.x).

## Decisiones de arquitectura (ya tomadas y ya reflejadas en el código — no las reabras)
1. **Backend central en la nube = única fuente de verdad.** Se abandonó el modelo "Home Base" (teléfono → PC casero). Migrado de Render a **Heroku** el 2026-07-22/23 (Render queda decomisionado, ver [[heroku-migracion-dyno-crash]] en memoria).
2. **La IA (el "Tasador") corre en el servidor** llamando a una API de modelo alojada (hoy Gemini). No reintroducir Ollama/PC local como requisito.
3. **BD = PostgreSQL/Supabase.** El "SQLite" del DESIGN.md está obsoleto; quedan restos (`pet_prod.db`, comentarios "for SQLite") que son basura a limpiar.
4. **Tiempo real con SignalR** (salud compartida, semáforo, Frenesí por grupo) — `FamilyHub` en `/hubs/family`, `PresenceService` (por grupo, en memoria), Frenesí dinámico ×2; cliente `RealtimeService`. **Auth (Fase 5):** el hub va `[Authorize]` con el token por `access_token` (query). **Ojo:** la presencia se carga al conectar; si creas/te unes a una familia ya conectado, el cliente debe llamar `RefreshGroups()` (ya lo hace tras `StartAsync`).
7. **Auth de sesión = JWT (Fase 5).** `TokenService` (HS256, `sub`=userId). Todo endpoint y el hub sacan el userId del **token**, no del body. Cliente: `AuthHeaderHandler` pone `Bearer` en cada request leyendo `Preferences["AuthToken"]`. Login con Google = OAuth mediado por servidor (`AuthController` `/api/auth/google/start`+`/callback`, redirige a la app con `?token=`).
5. **Mascotas multi-ámbito:** cada usuario tiene una mascota **personal**; además puede pertenecer a **grupos** (2–6 miembros) que comparten una mascota. Una **pareja** es un grupo de 2 (arquetipo Hogar): no hay entidad "pareja" aparte.
6. **Mascota personal = neutra + especie cosmética.** La personal NO usa arquetipo especializado; progresa con hábitos cotidianos (4 dimensiones: Cuerpo/Mente/Hogar/Bienestar) y su apariencia es 1 de 3 especies aleatorias (`Pet.Species` = Sprout/Ember/Aqua, asignada server-side). Los arquetipos especializados son para mascotas de **grupo** (Fase 3). **Registro diferido:** invitado primero (sin muro de login), login después "reclama" la cuenta vía `UpgradeAccount`.

## ✅ Estado a junio 2026 — Fases 0–5 CERRADAS (verificado en EMULADOR y TELÉFONO REAL contra Render)
> Fase 5 cerrada: **auth de sesión JWT** (el userId sale del token, no del body; `[Authorize]` en todos los controllers + `FamilyHub`; SignalR por `access_token`), **rate-limit** del endpoint de IA (10/min), **push FCM real** (notificación de Frenesí con app en segundo plano, verificada en teléfono real), **fail-fast de config**, y **primeros tests** (xUnit, 12 verdes). Detalle y **métodos de simulación/pruebas reproducibles** en `ROADMAP.md`. Package de la app: **`yenllo.org.PetProductivity`** (Firebase). Producción es **Heroku** desde el 2026-07-22/23 (autodeploy desde `main`); Render decomisionado.

Lo que antes eran bloqueadores 🔴 ya está resuelto:
- **Secretos:** fuera del repo (user-secrets); Supabase y Gemini **rotadas** por el dueño; `appsettings.json` solo con placeholders. (El historial de Git tiene las viejas, pero ya no sirven por la rotación. La rotación sigue siendo tarea del dueño, no del agente.)
- **IA real:** `GeminiAiService` apunta a `gemini-3.1-flash-lite` y responde 200 (juicio real verificado en logs). El fallback heurístico se conserva como red de seguridad.
- **XP/evolución:** `PetService` usa `AddStatXp(category, xpEarned)` (no `difficulty`); `TotalXp` sube con el XP mostrado y la evolución avanza.
- **Única fuente de verdad:** el cliente consume el estado del servidor (sin JSON local, sin "KassyBot", sin GUID hardcodeado); re-hidrata vía `/api/users/{id}` tras cada acción.
- También hecho: hash fuera de las respuestas, `Migrate()` al arranque, `ValueComparer` en los diccionarios JSON, tienda server-side segura (catálogo, sin exploit de precio), `HealthDecayHostedService` (decadencia automática), `DevController` gateado a `IsDevelopment`.

Pendientes / mejoras menores (no bloqueantes):
- 🟡 **Push en Xiaomi/MIUI:** llega bien con la app en segundo plano y batería sin restricción; si el usuario fuerza el cierre, el SO bloquea FCM (no es la app). El código ya manda prioridad alta.
- ✅ **Revisión visual del dueño: HECHA ×2** (confirmado 2026-07-15; exámenes terminados). Diorama/tienda/foco revisados en su dispositivo. El nombre del solicitante en "Solicitudes pendientes" también quedó verificado (el cliente se reconstruyó muchas veces desde el fix de código 2026-06-25).
- ✅ **Sentry + UptimeRobot: CONFIGURADOS por el dueño** (confirmado 2026-07-15).
- 🟡 **Foco grupal end-to-end con 2 personas: por confirmar** (familia de prueba `87se5q`; el dueño no confirmó si ya se probó). Es la única verificación humana del núcleo social que puede seguir pendiente.

(Ya NO son pendientes, no re-agregar: "tareas no persistidas" — `PetService.ApplyRewardAsync` SÍ inserta `TaskItem`; "solicitud sin nombre" — arreglado en código.)

## ➕ Post-Fase 5 (junio–julio 2026) — hecho tras el cierre de fases, desplegado a Render el 2026-07-02
- **Migración a .NET 10** (ver tabla de stack).
- **Foco (pomodoro):** sesiones con **comprobante por foto a mitad de sesión juzgado por Gemini Vision**, historial ✓/✗ por persona y **foco grupal sincronizado** (server: `FocusController`+`FocusMath`+`FocusCleanupHostedService` — borra fotos >30 días, el veredicto queda en `TaskItem.ProofVerdict`; cliente: `FocusPage`, `FocusGuard` Android).
- **Diorama de sala + tienda real:** `Controls/RoomDiorama.cs` (SKCanvasView; fondo `room_bg.png` cubo iso 1024² intercambiable + muebles por grilla, sprites Bongseng), estilos de sala equipables (`User.ActiveRoomStyle`), **colocación tipo Sims persistida** (`User.PlacedFurniture`, `POST api/shop/placements`, modo "Editar cuarto"), eventos con contador y premium con stub dev (dinero real = Google Play Billing pendiente, ver T14). **Catálogo = carpeta `Catalog/` en disco como fuente única de verdad** (189 objetos en 9 categorías; `CatalogLoader` en el server + `MauiAsset` en el cliente; editar `info.json` + reiniciar server = cambiar la tienda). Plan/specs: `docs/PLAN_DIORAMA_Y_TIENDA.md`.
- **Seguridad (T22):** auditoría completa + remediación server-side (rate-limit login/register, IDORs de grupos, `/proof` y juez IA endurecidos, secreto JWT con piso de entropía y **rotado** el 2026-07-02). Informe: `tareas/22-seguridad-INFORME.md`. Para publicar falta **T14-C0** (token corto+refresh+revocación, A1/M4/M5).
- **Backlog de mejora:** `tareas/00-indice.md` (28 planes T1–T28 con orden global; se ejecutan 1 a 1).

Verificación: diorama/tienda/foco **verificados en emulador por el agente** (2026-06-30/07-01) y **revisados visualmente por el dueño ×2** (confirmado 2026-07-15).

## Estado: qué SÍ funciona (verificado en emulador y teléfono real)
Bucle completo tarea→IA real→recompensa→evolución (todo **autenticado por JWT**); ceremonia de nacimiento (egg crack → flash → criatura por especie → nombrar → invitado **con token**); tienda contra catálogo con compra persistida; decadencia automática; ritual tres-en-raya (×1.2); **semáforo/Frenesí por grupo en vivo (SignalR sobre JWT)** con ×2 dinámico; **login con Google** (OAuth mediado por servidor, WebAuthenticator); **push FCM de Frenesí** (app en segundo plano). Cliente: Ceremonia → **5 pestañas (Mascota · Familias · Tienda · Perfil · Ajustes)** — la mascota personal es su propia pestaña (con banner de invitado), Familias es el grid de grupos, Ajustes es pestaña propia; detalle por mascota de grupo (tarea/miembros/semáforo/código dentro). Perfil de invitado muestra "Iniciar Sesión / Registrarse".

## Modelos de grupo: cableados (Fase 3 ✅)
`Group` (+`InviteCode`/`MaxMembers`/FK `SharedPet`), `GroupMembership`, `JoinRequest`, `SharedPet` y `PetMood` están **persistidos y cableados**: `DbSet`s, migración `AddSocialLayer` (aditiva), `GroupService`+`GroupsController` (`api/groups`). Unión por **código + aprobación unánime**; mascota compartida **sin dueño** (todos "padres"), **dormida** hasta ≥2 miembros. Tareas se registran a una mascota concreta (Hub→detalle); fuera de contexto → confirmación + recompensa ×0.25 (solo grupos). Afecto anti‑polizón con decaimiento (`AffectionDecayHostedService`). **Verificado end‑to‑end en emulador.**

## Reglas no negociables al programar aquí
- **Nunca** contraseñas en texto plano: hash + sal (hecho en Fase 0 con `PasswordHasher<User>`).
- **No** reintroducir dependencia de un PC local con Ollama.
- BD = PostgreSQL; no añadir SQLite.
- Trabajar **fase por fase** según `ROADMAP.md`. Antes de editar, proponer el plan de la fase (qué archivos toca y en qué orden) y esperar visto bueno.
- Ignorar la basura del repo (`cmdline-tools/`, `logs/`, `crash_*.txt`, `*.log`, `apk_list_log`, `adb_devices.txt`, `pet_prod.db`): no es parte de la app.

## Comandos (los proyectos viven bajo `src/`)
- Compilar server: `dotnet build src/PetProductivity.Server/PetProductivity.Server.csproj`
- Servidor (loop local): `dotnet run --project src/PetProductivity.Server --launch-profile http` → `http://0.0.0.0:5051` (HTTP puro, Development). Hace `Migrate()` al arrancar.
- Cliente Android: `dotnet build src/PetProductivity.Client/PetProductivity.Client.csproj -f net10.0-android -t:Run` (tras reiniciar el PC añadir `-p:AndroidSdkDirectory="C:\Users\renzo\AppData\Local\Android\Sdk"`)
- Emulador (evitar crash de GPU): `& "C:\Users\renzo\AppData\Local\Android\Sdk\emulator\emulator.exe" -avd medium_phone -no-snapshot-load -gpu swiftshader_indirect`
- **Apuntar el cliente al server local:** app → Ajustes → Dirección del Servidor → `http://10.0.2.2:5051` → reiniciar app (el default es Heroku/producción).
- Migraciones EF: `dotnet ef migrations add <Nombre> --project src/PetProductivity.Server/PetProductivity.Server.csproj` (se aplican solas al arrancar el server).
- Logs en vivo: `adb logcat -s ANTIGRAVITY ANTIGRAVITY_CRASH` · crash a archivo: `adb shell run-as yenllo.org.PetProductivity cat files/crash.txt` (package: `yenllo.org.PetProductivity`)
