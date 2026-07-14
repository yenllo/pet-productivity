# T30 — Log de la revisión continua (2026-07-14)

Registro operativo, no un plan: un resumen corto de cada tarea que se hizo hoy (sesión completa,
incluidas las de antes del `/loop`) — qué se encontró, cómo se verificó, y el commit. Las ideas
sin implementar viven en `tareas/29-ideas-futuras.md`, no aquí.

## Antes del loop (probando en teléfono/emulador con el dueño)

| # | Qué | Cómo se encontró / verificó | Commit |
|---|-----|------------------------------|--------|
| 1 | Tienda: 4,9 s → 11 ms | Instrumentado con cronómetro real (no adivinado); el costo era `Categories.Clear()+N Add()` sobre un `CollectionView`, no la red | `800aecc` |
| 2 | Diorama: picos de GC de 25-30 ms → 11-18 ms | Medido antes/después; la media de pintado (8 ms) nunca fue el problema, eran objetos `SKPaint`/blur creados por frame | `ee2b592` |
| 3 | Mascota borrosa/levitando/con emoji | La mascota vivía fuera del lienzo (`<Image>` XAML); movida dentro de `RoomDiorama`, anclada a su sombra, orden de profundidad isométrico | `7068a9b` |
| 4 | `/health` no aceptaba `HEAD` → 10 días de falsa alerta de caída | UptimeRobot manda HEAD; el endpoint era `MapGet`. Verificado con `curl -I` antes y después | `b444207` |
| 5 | Sentry en el cliente (crashes invisibles antes) | Smoke test real: `SentrySdk.CaptureMessage` de prueba, confirmado en el dashboard, luego retirado | `f55a894` |
| 6 | Economía del foco pagaba menos que mentir (foco 5 min: 10 XP; "leer" a mano: 20 XP) | Reproducido con los números exactos que reportó el dueño; decisión suya sobre el fix (piso ×1.4 + juez más estricto) | `24fd990` |
| 7 | Permisos de foco no se refrescaban / foco "se olvidaba" activo / notificación no reabría la pantalla / hueco fantasma de la mascota en la grilla | 4 bugs de una sola sesión de prueba en teléfono real; cada uno con su causa raíz (resume de Activity vs. navegación de Shell; falta de chequeo `IsActive`; falta el marcador `ActionOpenFocus`; regla de la baldosa (3,3) sin aviso) | `e12a279` |
| 8 | T20-I1b: `ToggleRitualCell` con string mágico "User not found" | Ya estaba en el toll-list de `tareas/20-consistencia-menores.md`, solo había que ejecutarlo | (incluido en la ronda de arriba) |

## Iteraciones del `/loop` (autónomo, dueño ausente)

| # | Área elegida | Qué se encontró | Cómo se verificó | Resultado |
|---|---|---|---|---|
| 1 | `GroupsController`/`GroupService` | `UserId` fantasma en 5 endpoints (create/join/mine/approve/leave) — el server siempre usa el token, el campo/parámetro viajaba y se ignoraba. Mismo patrón que T18 ya mató en otros controllers | Grep de usos en cliente y server antes de tocar; build limpio cliente+servidor | Arreglado, `4e03ad1` |
| 2 | Foco grupal (`FocusController.JoinInternal`) | Camino real (no hipotético) para cobrar recompensa de foco grupal sin esperar: una `FocusSession` SOLA abandonada sobre la mascota compartida (alcanzable desde "Registrar tarea" → Foco sin pasar por "Foco grupal") se reutilizaba sin realinear su reloj | Test de regresión nuevo (`GroupFocusJoinTests`), verificado **en ambas direcciones**: revertido el fix a mano → el test falla; restaurado → pasa | Arreglado, `4b3ff59` |
| 3 | `AuthController`/`SessionController` (refresh tokens) | Un comentario prometía "cascada de revocación" al reusar un token ya revocado — **nunca se implementó**. No es un bug mecánico, es una capa de seguridad real que falta | Grep exhaustivo: 0 tests, 0 código, solo el comentario | Comentario corregido (`1c8fb22`); diseño completo de la implementación real dejado en T29 para que el dueño lo revise antes de construirla — no se implementó solo por tocar auth |
| 4 | `TasksController`/`PetService.ProcessTaskCompletion`/`ApproveTaskAsync` | Nada — chequeo de membresía, compuerta de relevancia, mayoría de votos, auto-aprobación y reenvío del cliente, todo correcto | Lectura completa de las 3 capas (controller, servicio, cliente) | Sin hallazgos |
| 5 | `FamilyHub`/`PresenceService`/`RealtimeService` (SignalR, presencia, Frenesí) | Nada — locks consistentes, umbral de Frenesí (≥2) igual en todos lados, multi-conexión bien manejada, reconexión resincroniza (T25), `PetUpdate` verificado extremo a extremo (no es un evento fantasma) | Lectura completa server+cliente, grep de `"PetUpdate"` para confirmar que el emisor existe | Sin hallazgos |

## Convención de este log

Una fila por tarea/iteración, no por commit — si una iteración no encontró nada, se anota igual
("sin hallazgos" es información real). Se actualiza al cerrar cada iteración del loop.
