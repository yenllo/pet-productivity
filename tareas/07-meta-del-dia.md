# T7 — Meta del día: un buzón de tareas no es un objetivo

**Estado:** ✅ hecho (2026-07-03) — opción A + C como recomendaba el plan; B (misión rotativa) queda como segunda ola si hace falta variedad · **Esfuerzo global:** M · **Depende de:** T8 (reset del día local)

> **Implementado:** **A —** `User.RitualLabels` (CSV con `|`, migración `AddRitualLabels`, endpoint `POST api/users/me/ritual-labels` con validación 9×≤16 chars); el cliente muestra las etiquetas del usuario (o los defaults) y un botón ✏️ que activa modo renombrar (tocar celda → prompt → guarda las 9). **C —** card "Repetir de un toque" en el Dashboard: hasta 4 chips con las últimas descripciones distintas del historial; tap → confirmación → misma IA + recompensa sin tipear. **De paso:** el tablero ahora se sincroniza con el estado real del server al abrir (antes siempre aparecía vacío al reiniciar la app) y el badge ×1.2 sale de `ActiveXpMultiplier` (la verdad del server); parseo defensivo del estado en el cliente (mismo criterio que el server). **D** descartada (rompe la indirección sana del ×1.2), como decía el plan.
>
> **Verificado:** 3 tests de regresión de `ToggleRitualCell` (toggle, línea→×1.2→romperla, reset de día nuevo; fakes de test extraídos a `TestFakes.cs`) — 101 verdes. En emulador: etiquetas propias visibles en el tablero, renombrado por UI persistido en server (Gym→Correr), quick-log end-to-end (tap→confirmar→+XP) y el dedupe intra-día castigando la repetición del mismo día (27→2 XP) sin castigar la de días distintos (ventana 24 h por diseño).

## El quiebre (por qué)

La app registra lo que ya hiciste, pero no te propone nada para *hoy*: no hay una meta diaria concreta que al abrirla por la mañana diga "esto es lo que falta". El ritual 3-en-raya es lo más cercano, pero sus 9 celdas son toggles anónimos sin significado — un minijuego desconectado del sistema de tareas que solo existe para el ×1.2. Sin meta del día, cada apertura de la app depende de que el usuario traiga la motivación de su casa; con meta, la app se la presta.

## Evidencia en el código

- `src/PetProductivity.Server/Services/PetService.cs:326-373` — `ToggleRitualCell`: las celdas son solo `0/1` en el string `RitualGridState`; ninguna tiene nombre ni relación con tareas; el server ni valida qué "significa" togglear.
- `src/PetProductivity.Client/ViewModels/RitualCellViewModel.cs` — celda = índice + estado, nada más.
- El ×1.2 se activa con línea (`PetService.cs:359-366`) y se consume con una tarea ≥7 (`PetService.cs:162-163`): la conexión ritual→tareas existe, pero es invisible y abstracta para el usuario.
- `src/PetProductivity.Server/Services/PetService.cs:146-151` — los rendimientos decrecientes por día ya definen implícitamente un "cupo diario" (5 tareas a valor pleno) que nunca se comunica como meta.
- Historial disponible: `TaskItems` por usuario (`FocusController.cs:180-189` ya lo consulta) — materia prima para sugerir "lo de ayer".

## Opciones

### A. Celdas del ritual nombrables (tus 9 hábitos reales)
El usuario etiqueta cada celda una vez ("gym", "leer", "dormir temprano", "agua"…). Persistencia: un campo `RitualLabels` en `User` (string JSON o CSV de 9, junto a `RitualGridState`). El 3-en-raya pasa de minijuego abstracto a **tablero de hábitos personal**: la línea del día es literalmente "hice 3 de mis hábitos".
- **Pros:** convierte una mecánica ya construida en la meta diaria, sin sistema nuevo; el contenido lo genera el usuario (cero producción); el ×1.2 cobra sentido narrativo ("cumplí mis rituales → hoy rindo más").
- **Contras:** migración menor + UI de edición de etiquetas; sigue siendo auto-reporte trampeable (aceptable: siempre lo fue, y el premio es solo ×1.2).
- **Esfuerzo:** M · **Toca:** server (campo + endpoint) + migración + cliente (editar/mostrar etiquetas).

### B. Micro-misión diaria rotativa
Una misión del día generada **determinísticamente por fecha** (hash de fecha → elegir de una lista fija de ~20 plantillas: "completa 1 foco de 25 min", "una tarea de Hogar", "haz línea en el ritual") con bonus de oro al cumplirla. Sin tablas nuevas: la misión se computa, y el cumplimiento se verifica contra datos que ya se registran (`TaskItems`, `FocusSessions` completadas, `RitualGridState`).
- **Pros:** contenido "fresco" diario sin producir contenido ni tocar BD; empuja a probar partes de la app que el usuario no usa (foco, categorías descuidadas); barata.
- **Contras:** las plantillas se agotan/repiten (con 20 y rotación por fecha se nota al mes); verificar cumplimiento server-side exige mapear cada plantilla a una query — mantener eso simple o se vuelve un motor de quests (no construir un motor de quests).
- **Esfuerzo:** M · **Toca:** server (generador + verificador + endpoint) + cliente (card de misión).

### C. Quick-log: repetir lo de ayer con 1 tap
En el Dashboard, chips con las últimas tareas distintas ("🏋️ gym", "📖 leer 30 min"): un tap = re-enviarla hoy. Reutiliza el historial `TaskItems` y el endpoint de tareas tal cual (el dedupe de 24 h no castiga la repetición legítima en días distintos, `PetService.cs:132-144`).
- **Pros:** ataca la fricción #1 del bucle central (tipear la descripción cada vez); los hábitos reales SON repetitivos; solo cliente + 1 query.
- **Contras:** facilita también el spam de baja calidad (mitigado: dedupe intra-día + rendimientos decrecientes ya existen); no es una "meta", es un facilitador — complementa A/B, no las sustituye.
- **Esfuerzo:** S · **Toca:** cliente (+ endpoint trivial de "últimas distintas" o filtrar el history existente).

### D. Bonus por celdas además de la línea
Premiar progresión parcial del ritual: 3/6/9 celdas → gotas de oro pequeñas, tablero completo → bonus mayor.
- **Pros:** suaviza el "todo o nada" de la línea; da razón de volver a tocar el tablero durante el día.
- **Contras:** premia toggles auto-reportados con oro (hoy la línea solo da ×1.2, que exige una tarea real para cobrarse — esa indirección es elegante y esto la rompe); riesgo de inflación de oro por cero esfuerzo. Solo considerarla con montos simbólicos.
- **Esfuerzo:** S · **Toca:** server.

## Recomendación

**A + C**: celdas nombradas convierten el ritual en LA meta diaria (mecánica ya construida, contenido del propio usuario) y el quick-log elimina la fricción de registrar los hábitos que esas celdas representan — juntas cierran el círculo "veo mis hábitos → los hago → los registro en un tap". **B** como segunda ola si hace falta variedad (y entonces con lista corta y verificaciones simples). **D** descartada salvo montos simbólicos: rompe la indirección sana del ×1.2.

## Criterios de éxito / verificación

1. El usuario puede nombrar sus 9 celdas; los nombres persisten en el server y se ven en el tablero.
2. El estado del tablero sigue reseteando a diario y el ×1.2 por línea funciona igual que hoy (test de regresión de `ToggleRitualCell`).
3. En el Dashboard aparecen chips de tareas recientes; un tap registra la tarea completa (IA + recompensa) sin tipear.
4. El quick-log de una tarea repetida ayer NO sufre el castigo de dedupe; repetirla dos veces hoy SÍ.
5. (Si B) La misión del día es la misma en dos requests del mismo día y cambia al día siguiente.

## Dependencias

- **T8** para que el reset del tablero y "hoy" coincidan con el día real del usuario.
- Sinergia con **T1** (completar la meta del día = mantener la racha) y **T2** (aviso vespertino "te falta 1 celda para la línea").
