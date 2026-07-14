# CODE-AUDIT.md — Auditoría de errores de código (2026-06-25)

Auditoría full-stack (Cliente MAUI + Server ASP.NET + Shared) buscando lo que **rompe en runtime o
corrompe datos**: crashes, null-deref, races, fugas, atomicidad EF, lógica de recompensa/foco, auth.

## Veredicto general
El código está **en muy buen estado**. Lo verificado y **sin hallazgos** (deliberadamente, no por
omisión):
- **Auth:** los 9 controllers y `FamilyHub` sacan el `userId` del **token** (`User.GetUserId()`), nunca
  del body; `[Authorize]` en todos; JWT con issuer/audience/firma/lifetime validados (`Program.cs`).
- **Concurrencia en memoria:** `PresenceService` está correctamente bloqueado (`lock`).
- **EF:** un `DbContext` scoped por request con un `SaveChanges` por operación → atómico; los
  `ValueComparer` JSON (`AppDbContext.JsonCmp`) detectan bien las mutaciones in-place de las colecciones.
- **Hosted services:** `HealthDecay`/`AffectionDecay`/`FocusCleanup` crean **scope propio**
  (`IServiceScopeFactory`) → sin "captive dependency".
- **Capa de red del cliente:** `GameDataService`, `GroupService` y `RealtimeService` envuelven **toda**
  llamada en try/catch → los `async void`/`OnAppearing` no propagan excepciones de red.

Por eso, varios riesgos "de manual" (incluido el de `async void` que sospechábamos al planear) resultaron
**mitigados**. Los hallazgos reales son pocos. Ninguno es 🔴 crítico.

---

## Hallazgos

### 🟠 Alto

**A1 — La mascota personal nunca se ve "cristalizada" en el cliente.**
`Pet.Status`, `Health`, `MaxHealth`, `GracePeriodExpiry` tienen **setter privado**
(`Shared/Models/Pet.cs:40-43`). EF los puebla por backing-field, pero `System.Text.Json` (el del cliente,
`ReadFromJsonAsync<User>` en `GameDataService`) **no setea propiedades con setter privado** → llegan con el
valor por defecto (`Alive`/`100`). El Dashboard lee `CurrentPet.Status`
(`Client/ViewModels/DashboardViewModel.cs:136`), así que si el server cristalizó la mascota (vía
`HealthDecayHostedService`), el usuario **la sigue viendo viva** y nunca se entera de que debe revivirla con
una tarea de dificultad ≥9.
**Fix:** `[JsonInclude]` en esas 4 propiedades de `Pet.cs`. → **APLICADO** ✅

**A2 — El reset diario del ritual usa hora local del server, no UTC.**
`PetService.ToggleRitualCell` compara contra `DateTime.Now.Date` (`PetService.cs:324`) y setea
`LastRitualReset = today` local (`:332,337`), mientras **todo** el resto de la lógica de fechas usa
`DateTime.UtcNow`. En un host cuya zona ≠ UTC, el tres-en-raya se resetea a la hora equivocada y el
multiplicador (`ActiveXpMultiplier`) se limpia/aplica con el día corrido.
**Fix:** usar `DateTime.UtcNow.Date`. → **APLICADO** ✅

### 🟡 Medio

**M1 — `int.Parse` sin proteger sobre `RitualGridState`.**
`PetService.ToggleRitualCell:326` hace `stateStr.Split(',').Select(int.Parse)`. La guarda de longitud está
**después**; si la cadena guardada está malformada (dato legado/corrupto), lanza y el endpoint responde 500.
**Fix:** parseo defensivo antes de la guarda. → **APLICADO** ✅

**M2 — La tienda permitía comprar progreso de evolución con oro.**
`ShopController.cs`, "Cristal Evolutivo" → `pet.AddStatXp("Magic", 500)` sumaba 500 a `TotalXp` → avanzaba la
evolución, contra "Oro = solo cosmético". **Decisión del dueño: la evolución avanza SOLO con XP de tareas.**
**Fix:** se eliminó el efecto de XP del Cristal (queda como ítem cosmético en inventario) y se ajustó su
descripción. La Poción sigue curando HP (no es evolución). → **APLICADO** ✅

**M3 — Sin serialización del read-modify-write de la mascota compartida / oro.**
`PetService.ApplyRewardAsync` y `ShopController.BuyItem` hacían read-modify-write sin protección. Dos
miembros premiando a la vez (o tarea + compra simultáneas) podían pisarse → "last-write-wins" (se pierde un
incremento de XP/oro/afecto). Frecuencia baja (grupos de 2–6).
**Fix (enfoque elegido: lock en memoria):** nuevo `PetWriteLock` (singleton) con un `SemaphoreSlim` por
`petId`; en `ApplyRewardAsync` y `BuyItem` se toma el lock con la **misma clave `pet.Id`**, se hace
`Entry(...).ReloadAsync()` para traer valores frescos y luego apply+save. El lock **no** cubre la llamada a la
IA (rápido). Válido para 1 instancia (Render); el código documenta la ruta a `xmin`+reintento si se escala.
→ **APLICADO** ✅ (31 tests verdes).

### ⚪ Bajo (robustez / cosmético)

**B1 — `async void` fire-and-forget.** `DashboardViewModel.UpdateStatus`, `FocusViewModel.TryAutoJoin`,
`SettingsViewModel.SyncPreferences` y los `OnAppearing` de varias páginas. Hoy **no crashean** porque la capa
de servicio traga las excepciones, pero es frágil ante un futuro método sin guardar. **Fix defensivo:**
try/catch en los no-handler que aún no lo tenían (`UpdateStatus`, `TryAutoJoin`). → **APLICADO** ✅

**B2 — Fallback de contraseña en texto plano.** `UsersController.cs:64` compara la contraseña en claro como
ruta de migración de cuentas legadas. **Decisión del dueño: conservar** (cero riesgo de bloquear una cuenta
legada; es seguridad menor y no expone nada nuevo). Eliminar cuando se confirme que no quedan cuentas sin
migrar. → **CONSERVADO (intencional).**

**B3 — Dependencia vulnerable en el proyecto de tests.** `dotnet test` avisaba `NU1903`:
`System.Security.Cryptography.Xml 9.0.0` (transitiva vía `Microsoft.NET.Test.Sdk`) con vulnerabilidad alta.
Solo afecta a `tests/PetProductivity.Tests` (no se despliega), por eso ⚪.
**Fix:** `PackageReference` directo a `9.0.17` (parcheada) en el `.csproj` de tests → el aviso desaparece y
los 31 tests siguen verdes. → **APLICADO** ✅

---

## Verificación
- `dotnet build` server → **0 errores / 0 warnings**.
- `dotnet build` cliente `net10.0-android` → **0 errores** (112 warnings preexistentes, XAML/MAUI).
- `dotnet test` → **31/31 verdes** tras los fixes.

## Resumen de acciones
| # | Severidad | Estado |
|---|-----------|--------|
| A1 | 🟠 | ✅ Aplicado (`Pet.cs` `[JsonInclude]`) |
| A2 | 🟠 | ✅ Aplicado (`PetService` UTC) |
| M1 | 🟡 | ✅ Aplicado (parseo defensivo) |
| M2 | 🟡 | ✅ Aplicado (oro 100% cosmético) |
| M3 | 🟡 | ✅ Aplicado (`PetWriteLock` en memoria) |
| B1 | ⚪ | ✅ Aplicado (guards) |
| B2 | ⚪ | ✅ Conservado (decisión: intencional) |
| B3 | ⚪ | ✅ Aplicado (pin `9.0.17`) |
