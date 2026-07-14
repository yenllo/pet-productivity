# T20 — Consistencia menor (checklist transversal, no una tarea con fecha)

**Estado:** pendiente (transversal) · **Esfuerzo global:** S repartido · **Depende de:** —

## El quiebre (por qué)

Ninguno de estos ítems justifica una tarea propia, pero juntos definen cuánto cuesta leer y extender el código: tres estilos de manejo de errores conviviendo, `async void` sin red en ViewModels (una excepción ahí tumba la app entera en Android), y desalineaciones de estilo que hacen que cada archivo se lea "de su época". Este archivo es un checklist para resolverlos **de paso** — la regla es: quien abra un archivo por otra tarea, deja estos ítems arreglados en lo que tocó.

## Inventario y opciones por ítem

### I1. Tres estilos de error server-side
Conviven: (a) `GroupException(status, msg)` + catch en controller (`GroupsController.cs:29` — el patrón bueno), (b) strings mágicos comparados literal — `ToggleRitualCell` devuelve `"User not found"` y `UsersController.cs:152` hace `if (result == "User not found")`, (c) `TaskResult.Message` como canal de error sin flag de éxito (`PetService.cs:43,61,66`).
- **Opción a — unificar sobre el patrón (a):** renombrar `GroupException` → `ApiException` (vive en `GroupService.cs:8`, moverla a un archivo propio) y usarla en `PetService`/`ToggleRitualCell`; un middleware o filtro pequeño puede reemplazar los catch repetidos por controller.
- **Opción b — dejar cada estilo donde está** y solo matar el string mágico de ritual (el caso más frágil: un cambio de tilde rompe el 404).
- **Recomendada:** **b ya** (5 minutos, de paso en cualquier tarea que toque `PetService`), **a** progresiva al abrir cada servicio. No hacer una cruzada dedicada.

### I2. `async void` en métodos de ViewModel
`FocusViewModel.cs:84` (`TryAutoJoin`), `:126` (`OnMidpoint`), `:176` (`EnsurePetImage`), `SettingsViewModel.cs:204` (`SyncPreferences`). Una excepción no capturada en `async void` mata el proceso en Android. `DashboardViewModel.cs:302` (`UpdateStatus`) ya muestra la mitigación local: try/catch total con comentario.
- **Opción a — convertir a `async Task`** donde el llamador pueda await (los `[RelayCommand]` de CommunityToolkit ya lo soportan).
- **Opción b — guard try/catch total** (patrón `UpdateStatus`) donde el llamador es un evento y `async void` es inevitable (`OnMidpoint` es handler de evento: este es su caso legítimo).
- **Los `OnAppearing` de las Views** son el patrón estándar MAUI — dejarlos, pero con el cuerpo protegido si hacen red.
- **Recomendada:** a donde se pueda, b en handlers. De paso, al tocar cada VM.

### I3. `Console.WriteLine` como logging del cliente
~15 usos en `GameDataService` y otros. En Android nadie lo ve (ni siquiera es `Debug.WriteLine`).
- **Resolución:** muere junto con **T17-A** (el helper HTTP centraliza el logging con `ILogger` de MAUI). Los usos fuera de GameDataService se cambian al abrir cada archivo.

### I4. Idioma de los comentarios mezclado (ES/EN por épocas)
- **Opción a — política hacia adelante:** todo comentario nuevo en español (coherente con T16); NO retraducir masivamente lo existente (un diff gigante sin valor funcional que ensucia el blame).
- **Recomendada:** a. El pasado se queda como está; se traduce solo lo que se reescribe.

### I5. `RoomDiorama.cs` (739 líneas, el archivo más grande del repo)
Aceptable para un control custom de SkiaSharp, y ya delegó a `RoomSprites`/`RoomGrid`. Riesgo: F4 (animaciones/Lottie, estados de humor de T5) va a crecer ahí.
- **Resolución:** no tocar ahora; regla de umbral — si F4/T5 lo empujan sobre ~1000 líneas, extraer la capa de animación a su propio archivo en esa misma pasada. Anotado aquí para que la decisión ya esté tomada cuando ocurra.

### I6. `CurrentUser` no-nullable asignado con null
`GameDataService.cs:47` — `public User CurrentUser { get; private set; }` sin `?`, con flujos que lo dejan null (todos los métodos hacen `if (CurrentUser == null)`).
- **Resolución:** marcarlo `User?` y dejar que el compilador señale los accesos sin guard. De paso en T17-A/D, que reescribe ese archivo.

## Recomendación global

No agendar T20 como sprint: es la **lista de peaje** — cada tarea que abra uno de estos archivos paga su ítem. Las únicas acciones inmediatas razonables (5-10 min cada una): I1-b (matar el string mágico del ritual) e I2-b (guard en `TryAutoJoin`/`EnsurePetImage`, que hacen red y hoy pueden tumbar la app).

## Criterios de éxito / verificación

1. `grep -rn "== \"User not found\"" src/` → vacío.
2. Ningún `async void` en ViewModels sin try/catch total o conversión a `async Task` (grep + revisión).
3. Cero `Console.WriteLine` en el cliente al cerrar T17.
4. `RoomDiorama.cs` < 1000 líneas después de F4 (o la extracción hecha).

## Dependencias

- I3 e I6 se resuelven dentro de **T17**; I4 es la política de **T16** aplicada a comentarios; I5 se dispara con **F4/T5**.
