# ROADMAP y Modelo de Datos — PetProductivity

Plan de trabajo grounded en el informe de estado del código (no en suposiciones). Decisiones de producto ya tomadas:

- **Mascotas:** personal + grupo (≤6) + pareja (= grupo de 2).
- **Ubicación del grupo:** sin decidir → se diseña para el caso general (distribuido); el backend en la nube ya cubre ambos.
- **IA local:** no es requisito → la IA corre en el servidor (hoy Gemini).

> El código ya está en la arquitectura de nube (Render + Supabase + Gemini). Esto no es un rediseño: es **terminar, arreglar y reconectar** lo que existe. El `DESIGN.md` es histórico.

---

## Modelo de datos objetivo

Al arranque del plan, `Pet` colgaba de un único `User` (1‑a‑1) y `Group`/`SharedPet`/`PetMood` existían sin DbSet, migración ni controlador. **Implementado en la Fase 3.** El modelo objetivo:

**User** — credenciales (con hash + sal), displayName, CurrentStreak, TotalTasksCompleted, grid de ritual, SyncStatus (🟢 🔨 🔴 ⚫), multiplicador de XP.

**Group** — Id, Name, Archetype (Hogar, Gremio, …), InviteCode, MaxMembers (2 = pareja, hasta 6), CreatedByUserId, CreatedAt. *Una pareja es un Group con MaxMembers = 2.*

**GroupMembership** — N‑a‑N entre User y Group: Id, GroupId, UserId, Role (Owner | Member), JoinedAt. Un usuario puede estar en varios grupos a la vez.

**Pet** — pertenece a **exactamente un** ámbito: usuario (personal) o grupo (compartida). Dos FKs nullable `OwnerUserId` / `OwnerGroupId` (solo una con valor). Campos actuales: Stats (diccionario), TotalXp, EvolutionStage (Egg→Baby→Adult→Master), Health/MaxHealth, Hunger, Happiness, GoldCoins, UnlockedSkins, Crystallized, GracePeriodExpiry, Archetype. (Aprovechar el `SharedPet` ya modelado.)

**TaskEntry** — Id, UserId, RawText, Category, Difficulty, XpAwarded, GoldAwarded, CreatedAt, y el/los ámbito(s) que alimentó (ver decisión abierta).

**ShopItem / InventoryItem** — catálogo formal (Id, Name, Price, EffectType, EffectValue) en vez de los efectos "por nombre" actuales.

---

## Decisión abierta (la abrió tu elección de mascotas)
Si tienes mascota personal **y** de grupo, ¿a qué mascota alimenta cada tarea?
- **Recomendada** (fiel a "el grupo crece con el trabajo de todos"): cada tarea **siempre alimenta tu mascota personal** y **además aporta** a la de cada grupo/pareja (puede ser a tasa menor para no inflar).
- **Más simple:** al registrar la tarea **eliges** un ámbito y solo esa mascota recibe.

Se implementa en la Fase 3.

---

## Fases

### Fase 0 — Saneamiento y seguridad (BLOQUEANTE, primero)
- [ ] **[lo hace el dueño en los paneles]** Rotar la contraseña de Supabase y regenerar la API key de Gemini. **NO HECHO** (confirmado por el dueño el 2026-07-23; antes este punto decía "ambas rotadas ✅", lo cual era falso). Ambas siguen **vivas** y en claro en el historial del repo **privado** (`archive-privado`, commits `29a3092`/`5cb601d`/`6151282`, 2025-12-13). El repo **público está limpio** (verificado 2026-07-23): la exposición exige acceso al repo privado. El dueño decidió posponerlo hasta la subida a Play Store.
- [x] Sacar los secretos del repo → user-secrets / variables de entorno. `appsettings.json` sin credenciales. (Completado con dotnet user-secrets ✅)
- [x] `.gitignore` correcto + `git rm --cached` de toda la basura (`cmdline-tools/`, `logs/`, `*.log`, `crash_*`, `pet_prod.db`, etc.).
- [x] Hash de contraseñas (ASP.NET Identity o hash+sal). Fin del login por igualdad en texto plano. (`PasswordHasher<User>` con verificación + fallback de migración de legados ✅)
- [x] `dotnet build` verde; limpiar restos de SQLite/Ollama (comentarios, `pet_prod.db`).

### Fase 1 — Una sola fuente de verdad + IA que funcione
- [x] Arreglar el bug de `TotalXp`/evolución en `PetService` (usar `AddStatXp`). (`AddStatXp` ahora incrementa `TotalXp` y `EvolutionStage` deriva de él ✅)
- [x] Reparar el Tasador: modelo Gemini vigente (o proveedor elegido) y pasar el arquetipo real al juez. (Gemini 3.1 Flash Lite conectado y prompt ajustado ✅)
- [x] Hacer que el cliente consuma el estado del servidor en vez de su JSON local; enlazar el Dashboard al usuario logueado; eliminar la mascota mock ("KassyBot") y el GUID hardcodeado. (La conexión base ya está, falta limpiar la data mock)

### Fase 2 — Bucle jugable completo ✅ (cerrada)
- [x] Onboarding: registro → elegir arquetipo → crear mascota persistida. (Selector de arquetipo en registro + `ArchetypeStats.InitializeStats`. **Nota:** la mascota personal se reorienta a NEUTRA — ver Fase 2.5.)
- [x] Sprites por etapa de evolución y por especie. (egg/crystal PNG + baby + 3 especies placeholder SVG `sprout/ember/aqua`; `DashboardViewModel.PetImageSource` elige por etapa+especie. **Verificado renderizando en emulador.**)
- [x] Cablear Stats / Shop / Profile a datos reales + catálogo de tienda formal. (Modelo `ShopItem`, endpoint `GET /api/shop/catalog`, `ShopViewModel` lo consume; `StatsViewModel` lista stats reales; precio validado server-side contra `ItemCatalog`, sin exploit de precio negativo.)
- [x] Decadencia de salud automática. (`HealthDecayHostedService : BackgroundService` con `IServiceScopeFactory`, −5 hambre cada 2 h, ignora cristalizadas; `DevController` gateado a `IsDevelopment`. Las tareas reabastecen hambre; `Pet.Hunger` nace en 100.)

### Fase 2.5 — Onboarding, identidad de mascota y ceremonia de nacimiento (EN CURSO)
Decisiones de producto tomadas (junio 2026):
- **Mascota personal = NEUTRA.** Sin arquetipo especializado; progresa con tareas cotidianas (hábitos). Sus 4 dimensiones de vida: **Cuerpo, Mente, Hogar, Bienestar**. Los arquetipos especializados (Scholar, Technologist, …) quedan reservados para mascotas de **grupo** (Fase 3). El fix de `CheckEvolution` (reverse-lookup en `ArchetypeStats`) ya mantiene neutra a la personal.
- **3 especies personales aleatorias, solo cosméticas** (mismas stats), exclusivas de personales. Asignación aleatoria **server-side** al nacer (`Pet.Species`, enum `PetSpecies = Sprout|Ember|Aqua`). Placeholders SVG actuales reemplazables por pixel art (formato: PNG 512², o GIF para "vida"; el huevo va como GIF/Lottie).
- **Registro diferido (sin muro de login).** Primer arranque → ceremonia de nacimiento, no Login. El invitado anónimo lo crea el servidor; el login posterior **reclama** esa cuenta (`UpgradeAccount` ya existe, con guarda anti-borrado de XP). Nunca encerrar el bucle central (tarea→alimentar→evolucionar) detrás del registro.
- **Menú principal futuro = cuadrícula de mascotas** (hub) → tap → detalle (tabs actuales) → volver con flecha/huevo. DIFERIDO: solo cobra sentido con multi-mascota (Fase 3); hoy sería un grid de 1 elemento.
- **Bloqueo + tutorial:** secciones en gris ("se necesita progresar con [nombre] para desbloquear X"); el **progreso de la mascota** desbloquea lugares de juego (Tienda/Vestuario); el **registro** desbloquea lo social/nube.

Checklist — primera parte (pantalla de Nacimiento):
- [x] Pantalla de Nacimiento en primer arranque (flag en `Preferences`), sin pasar por Login.
- [x] Reproduce `Resources/Images/eggCrack.gif` + flash en la app → revela la criatura de la especie.
- [x] Recuadro "¿Cómo se llamará tu mascota?" → el nombre se pasa a la creación del invitado (en vez de auto "X's Pet"). Botón deshabilitado hasta que el Entry tenga texto.
- [x] La mascota nace ya como **Bebé** (mostrando su especie) + **oro inicial** (~100).
- [x] Mover el selector de arquetipo fuera del registro (la personal es neutra — `Archetype.Neutral`).
- [x] Prompt posterior "Regístrate / inicia sesión para no perder a [nombre]" (banner one-time con dismiss en Dashboard).

### Fase 3 — Capa social (grupos) ✅ (cerrada, verificada en emulador)
- [x] Persistir `Group` (+`InviteCode`/`MaxMembers`/FK `SharedPet`) + `GroupMembership` + `JoinRequest`; cablear `SharedPet`. Migración `AddSocialLayer` **solo aditiva** (sin tocar el 1‑1 personal `User.UserPetId`).
- [x] `GroupService` + `GroupsController` (`api/groups`): crear, unirse por código, **aprobación unánime** de todos los miembros, listar, detalle (mascota + miembros con ánimo + solicitudes), salir (borra grupo+mascota al quedar vacío). Mascota **dormida** hasta ≥2 miembros.
- [x] Regla "tarea → qué mascota": se elige la mascota (Hub → detalle); la IA juzga contra **su** arquetipo. Tarea fuera de contexto (solo mascotas de grupo) → confirmación + recompensa **×0.25**. La personal Neutral es catch-all. (`AiJudgeService` devuelve `Relevant`; `PetService.ProcessTaskCompletion(userId, petId, desc, confirmed)`.)
- [x] Cliente: **rediseño Hub** (3 pestañas Hub+Tienda+Perfil); `HubPage` (cuadrícula de mascotas), `PetDetailPage` (grupo: tarea/miembros/ánimo/solicitudes/código), `CreateGroupPage`. Tarea/Stats viven en el detalle.
- [x] Afecto anti‑polizón completo: sube al contribuir, baja por inactividad (`AffectionDecayHostedService`, 12 h), ánimo por usuario en el detalle. Verificado: contribuyente 50→70→68, free‑rider 50→48.

> **Nota dev:** `Constants.BaseUrl` apunta por default a Render (producción); para dev local se cambia en la app (Ajustes → Dirección del Servidor). `DevController` ganó `POST /api/dev/decay-affection` (gated a Development) para probar el decaimiento.

### Decisión "tarea → mascota" (resuelta en Fase 3)
Se eligió **elección por contexto**: entras a una mascota y registras la tarea *a ella*; solo esa recibe. La mascota compartida **no tiene dueño** (todos son "padres"); la única con dueño es la personal.

### Fase 4 — Tiempo real ✅ (cerrada, verificada en emulador)
- [x] **Hub SignalR** (`FamilyHub` en `/hubs/family`): semáforo (presencia/estado por familia), Frenesí y estado de mascota **en vivo por grupo**. Reemplazado el `SyncManager` global por `PresenceService` (en memoria, por grupo); `SyncController` eliminado (el estado va por el hub).
- [x] **Frenesí dinámico**: activo mientras ≥2 miembros de una familia están "Trabajando"; se apaga al bajar. Aplica **×2 XP** real a las tareas de esa mascota (antes ni se aplicaba).
- [x] **Estado compartido en vivo**: `PetUpdate` difunde salud/XP/hambre/afecto tras alimentar o decaer. "Huraña solo para ti": cada miembro ve su propio ánimo (contribuyente → Feliz, free‑rider → Neutral/Huraño).
- [x] **4 estados** (🟢 Disponible / 🔨 Trabajando / 🔴 Ocupado / ⚫ Desconectado); "Disponible = avísame" → aviso **en‑app** cuando un compañero trabaja.
- [x] **Cliente**: `Microsoft.AspNetCore.SignalR.Client` + `RealtimeService`; `PetDetailViewModel` muestra semáforo en vivo (punto de color por miembro) + banner Frenesí + salud/XP/ánimo en vivo; el selector de estado del dashboard va por el hub.

> **Verificado en emulador** (app = usuario A + cliente de consola SignalR = usuario B): presencia en vivo, aviso "alguien trabaja", Frenesí dinámico on/off, tarea durante Frenesí = ×2 (120 XP vs 60), `PetUpdate` en vivo (XP y ánimo).

### Fase 5 — Endurecimiento ✅ (cerrada, verificada en TELÉFONO REAL contra Render)
- [x] **Auth de sesión (JWT).** `TokenService` (HS256, claim `sub`=userId). Login/registro/upgrade/Google devuelven `AuthResponse {User, Token}`. `[Authorize]` en `Users/Tasks/Shop/Groups` + `FamilyHub`; el userId sale del **token** (`ClaimsPrincipalExtensions.GetUserId()`), no del body. `GET /api/users/me`. SignalR autentica por **`access_token` en query** (`JwtBearerEvents.OnMessageReceived` para paths `/hubs`). Cliente: `AuthHeaderHandler` (DelegatingHandler que pone `Bearer` leyendo `Preferences["AuthToken"]`, sin ciclo de DI), `AuthService` guarda token + rehidrata por `/me`, `RealtimeService` con `AccessTokenProvider`. El invitado también recibe token.
- [x] **Rate-limit del endpoint de IA.** `AddRateLimiter` política `"ai"` (10/min particionado por userId del token; fallback IP), `[EnableRateLimiting("ai")]` en `POST /api/tasks`. Verificado: 13 reqs → 10×200 + 429.
- [x] **Push FCM real (app en segundo plano).** Cliente: `Plugin.Firebase.CloudMessaging` + `google-services.json` (gitignored, build action `GoogleServicesJson`) + `CrossFirebase.Initialize(this)` en `MainActivity` + permiso `POST_NOTIFICATIONS`; `PushRegistration` pide permiso, obtiene el token FCM y lo registra (`POST /api/users/me/device-token`), llamado tras login en `HubViewModel`. Servidor: `FirebaseAdmin` + `PushService` (init lazy desde service account; **no-op si no hay credencial**), `User.DeviceToken` (migración aditiva `AddDeviceToken`), `FamilyHub` empuja push **prioridad alta** a los miembros **offline** al **encenderse** el Frenesí (en `SetStatus` Y en `OnConnectedAsync`). **Verificado en teléfono real:** llega "🔥 ¡Frenesí!" desde Render.
- [x] **Secrets/config.** Fail-fast al arranque (`Program.cs`) si falta `DefaultConnection`/`Gemini:ApiKey`/`Jwt:Key`. `.gitignore` blinda `google-services.json` y service-account. `DEPLOY.md` con las env vars de Render.
- [x] **Primeros tests.** `tests/PetProductivity.Tests` (xUnit, **12 verdes**, en el `.sln`): evolución por `TotalXp` + mecánica Fénix, `PresenceService` (Frenesí ≥2 working / transición / desconexión), `TokenService` (sub+issuer), fallback determinista de `AiJudgeService`. Correr: `dotnet test tests/PetProductivity.Tests/PetProductivity.Tests.csproj`.

> **Bugs reales encontrados durante la verificación** (no eran flakiness del emulador):
> - **Presencia "Desconectado" de uno mismo:** si el cliente conectaba al hub ANTES de tener la familia (caso común: abrir Familias y luego crear/unirse), `OnConnectedAsync` lo unía a 0 grupos. Fix: `FamilyHub.RefreshGroups()` que re-sincroniza grupos/presencia; el cliente lo invoca tras `StartAsync` (Hub y detalle de familia).
> - **Push de Frenesí no disparaba** si un miembro se reconectaba **ya-Trabajando** (la transición ocurría en `OnConnectedAsync`, no en `SetStatus`). Fix: detectar la transición off→on también al conectar.

### Post-Fase 5 (junio–julio 2026) — hecho fuera de fases ✅
- **Migración a .NET 10** (TFMs `net10.0-*` + Dockerfile `sdk:10.0`/`aspnet:10.0`).
- **Foco con comprobante:** foto a mitad de sesión juzgada por **Gemini Vision**, historial ✓/✗ por persona, **foco grupal sincronizado** (`FocusController`/`FocusMath`/`FocusCleanupHostedService`).
- **Diorama + tienda real:** fondo `room_bg` + muebles Bongseng, colocación tipo Sims persistida (`User.PlacedFurniture`), eventos, premium stub; **catálogo = `Catalog/` en disco** (189 objetos, 9 categorías). Plan: `docs/PLAN_DIORAMA_Y_TIENDA.md`.
- **Auditoría de seguridad + remediación server-side** (informe: `tareas/22-seguridad-INFORME.md`; secreto JWT rotado).
- **Backlog de mejora:** `tareas/00-indice.md` (T1–T28, se ejecutan 1 a 1).

> Desplegado a Render el **2026-07-02** (migraciones `AddRoomStyle`/`AddPlacedFurniture` incluidas). Verificado en emulador por el agente; **revisión visual del dueño pendiente** (post-exámenes). Dinero real (Play Billing) pendiente → T14.

---

## Correcciones de estabilización (hechas, junio 2026)
Ronda de endurecimiento sobre Fases 0–2 (todo verificado: build verde + smoke test en emulador contra server local):
- **Migración automática al arranque:** `db.Database.Migrate()` en `Program.cs`.
- **Persistencia de diccionarios JSON:** `ValueComparer` en `Inventory`, `Stats`, `UnlockedSkins`, `UserAffection`, `MemberIds` (antes EF no detectaba mutaciones in-place).
- **Tienda segura:** ignora el `Price` del cliente, usa catálogo server-side (sin exploit de precio negativo).
- **`DevController` gateado** a `IsDevelopment` (antes cualquiera podía cristalizar mascotas en prod).
- **Cliente sin `.Wait()` bloqueante** en el constructor del Dashboard (riesgo de ANR/NRE); init asíncrono en `OnAppearing`.
- **Login Google** ya no simula entrada falsa; **Logout** sí limpia sesión; ruta `TaskPage` desambiguada.
- **Bug de XP/evolución (clave):** `PetService` otorgaba solo `difficulty` (1–10) al `TotalXp` en vez del `xpEarned` mostrado (`difficulty×10`) → la evolución iba 10× más lenta que los números en pantalla. Corregido a `AddStatXp(category, xpEarned)`.
- **Modelo Gemini:** estaba en `gemini-1.5-pro` (404, siempre fallback) → vuelto a `gemini-3.1-flash-lite` (responde 200, juicio real verificado).
- **Limpieza:** `OllamaDtos`/`Class1` borrados (conservando `AiJudgmentResult`), `RewardCalculator`/`ComboManager` fuera del DI.

## Métodos de simulación y pruebas (REPRODUCIBLES — léelo antes de probar en otro chat)
Todo esto se usó para verificar Fases 0–5. El objetivo: poder repetir sin redescubrir.

**Datos base**
- Shell: **PowerShell** (Windows). SDK Android: `C:\Users\renzo\AppData\Local\Android\Sdk`. adb: `…\platform-tools\adb.exe`. AVD único: **`medium_phone`** (Android 36, imagen `google_apis_playstore` → tiene Play Services, necesario para FCM). Teléfono real de prueba: Xiaomi/MIUI `bb3162ae` (garnet). Package de la app: **`yenllo.org.PetProductivity`**.
- **Tras reiniciar el PC se pierde el SDK env** → en TODO build del cliente pasar `-p:AndroidSdkDirectory="C:\Users\renzo\AppData\Local\Android\Sdk"` (y `$env:ANDROID_HOME=...`).

**Emulador**
- Lanzar **headless** (evita crash de GPU `UpdateLayeredWindowIndirect`): `& "$SDK\emulator\emulator.exe" -avd medium_phone -no-snapshot-load -no-window -gpu swiftshader_indirect -no-boot-anim`. Las screenshots por adb funcionan headless.
- **Dos emuladores del mismo AVD**: TODAS las instancias con **`-read-only`** + `-port 5554` / `-port 5556` (sin `-read-only` la 2ª no arranca: "Another emulator instance is running").
- Esperar boot: poll `adb -s emulator-5554 shell getprop sys.boot_completed` == `1`.

**Server local** (mismo Supabase que Render → datos compartidos)
- Correr el **DLL ya compilado** (más fiable que `dotnet run`, que a veces se cuelga en "Compilando..."): `$env:ASPNETCORE_ENVIRONMENT="Development"; $env:ASPNETCORE_URLS="http://0.0.0.0:5051"; dotnet "src/PetProductivity.Server/bin/Debug/net10.0/PetProductivity.Server.dll"`.
- Verlo arriba: `GET http://localhost:5051/api/shop/catalog` → 200. Útil cuando hace falta **ver logs del servidor** (los del cliente .NET NO salen).
- Emulador→server local: `http://10.0.2.2:5051`. Teléfono USB→server local: `adb -s <serial> reverse tcp:5051 tcp:5051` y apuntar a `http://localhost:5051`.

**Deploy del cliente**
- A un device: `dotnet build src/PetProductivity.Client/PetProductivity.Client.csproj -f net10.0-android -t:Run -p:AndroidSdkDirectory="..." -p:AdbTarget="-s <serial>"`.
- **MIUI/Xiaomi**: `-t:Run` falla en la instalación (`INSTALL_FAILED_USER_RESTRICTED`, popup). Camino fiable: build SIN `-t:Run`, luego `adb -s <serial> install -r src\…\bin\Debug\net10.0-android\yenllo.org.PetProductivity-Signed.apk` y lanzar `adb -s <serial> shell monkey -p yenllo.org.PetProductivity -c android.intent.category.LAUNCHER 1`. (Requiere en el teléfono: "Instalar vía USB" + "Depuración USB (ajustes de seguridad)".)
- adb se cae a veces: `adb kill-server; adb start-server`.

**Screenshots para leerlas con Claude**
- `adb -s <serial> shell screencap -p /sdcard/s.png; adb -s <serial> pull /sdcard/s.png $env:TEMP\s.png`, luego **redimensionar a 540px de ancho** (las 1080×2400 exceden el límite de imagen) con `System.Drawing.Bitmap`.

**Driving la UI por adb**
- Tap (coords REALES, device 1080 ancho): 5 pestañas inferiores ≈ x: 108 / 320 / 540 / 756 / 972, y ≈ 2260.
- `adb shell input text "..."` **corta en el primer espacio** → usar texto sin espacios. Cerrar teclado: `keyevent 111`. Home: `keyevent 3`. Back: `keyevent 4`. Notificaciones: `cmd statusbar expand-notifications`.

**Leer estado interno de la app (los `Console.WriteLine` de .NET NO aparecen en logcat)**
- Diagnóstico: escribir a `Preferences` y leerlas por adb. `adb -s <serial> shell run-as yenllo.org.PetProductivity cat shared_prefs/yenllo.org.PetProductivity_preferences.xml`. De ahí se saca el **`AuthToken`** para consultar el REST como si fueras la app.

**Smoke REST con token (sin UI, rápido)**
- `POST /api/users/register` → `AuthResponse {user, token}`. Endpoints con `Authorization: Bearer <token>`. Verificar auth: sin token → 401, con token → 200, `/api/users/me` = el user del token.

**Realtime / Frenesí / push SIN 2 dispositivos: cliente de consola SignalR**
- Carpeta `C:\Users\renzo\rt_test\` (NO en el repo): `dotnet run -- <jwt> [working|available|busy|offline] [ms]`. Conecta a `$env:RT_URL/hubs/family` con `AccessTokenProvider` (idéntico al MAUI). Imprime Presence/Frenzy/SomeoneWorking/PetUpdate.
- **Frenesí**: 2 clientes de consola con tokens de 2 miembros de la misma familia, ambos `working` → `isFrenzyActive=True`. Tarea a la mascota compartida con Frenesí → XP **×2** (ratio `xpEarned/dificultad = 20`).
- Para depurar el **hub** server-side: server local en consola + cliente de consola con token → se ven `OnConnectedAsync` (userId, claims) y los broadcasts.
- Montar una familia por API: crear (token A) → `POST /groups/join {inviteCode}` (token B) → aprobar (`POST /groups/requests/{id}/approve` por cada miembro actual, **unánime**: 202 mientras falte alguien, 200 al completar).

**Probar push FCM**
- El **emulador NO entrega FCM de forma fiable** (aunque tenga Play Services) → push se prueba en **teléfono real**.
- El teléfono debe estar en **segundo plano** (Home), **NO forzado a cerrar** (Android bloquea FCM a apps "stopped"). **MIUI/Xiaomi**: desactivar "Pausar la actividad si no se utiliza" / batería **"Sin restricciones"** (limitación del SO que sufren todas las apps; el código ya manda prioridad alta).
- El server devuelve `messageId` de Google = mensaje **aceptado**; si no llega, el problema es la **entrega del SO**, no el envío.
- Escenario: familia con ≥3 miembros, el **destinatario offline** + 2 `working` → al encenderse el Frenesí, push al offline.

## Despliegue y secretos (NO van al repo)
- **Render** redeploya solo al hacer `git push origin main`. El **código** del push va en el repo, pero las **credenciales NO**: hay que ponerlas como **env vars / Secret Files en Render**. Lista completa en `DEPLOY.md`. Imprescindibles: `Jwt__Key`/`Jwt__Issuer` (sin esto el server no arranca por el fail-fast) y, para push, `Firebase__ProjectId` + Secret File `firebase-service-account.json` con `Firebase__ServiceAccountPath=/etc/secrets/firebase-service-account.json`.
- **Local**: secretos del server en `dotnet user-secrets` (Supabase, Gemini, Google, Jwt, Firebase). Service account en `C:\Users\renzo\.secrets\firebase-service-account.json` (FUERA del repo). `google-services.json` en `Platforms/Android/` (gitignored). El selector Node.js/Java/… de Firebase solo cambia el ejemplo de código: la "clave privada" JSON es la misma.
- `Constants.BaseUrl` = `https://pet-productivity-c03ac5654dd2.herokuapp.com` (producción, default; migrado de Render a Heroku 2026-07-22/23). Para dev local cambiar en Ajustes → Dirección del Servidor.

## Cómo avanzar con Claude Code
Empieza cada fase pidiéndole un **plan de la fase** (qué archivos toca y en qué orden) y dale el visto bueno antes de que edite. Una fase no se cierra hasta que **algo corre y se verifica** (build verde no basta: las pruebas reales destaparon bugs que el build no veía). No saltes de fase sin cerrar la anterior.
