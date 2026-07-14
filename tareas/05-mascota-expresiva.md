# T5 — Mascota expresiva: las barras no generan cariño

**Estado:** ✅ hecho (2026-07-03) — versión B + C-mínimo + D-celebración; sprites por humor quedan para F4 (arte) · **Esfuerzo global:** M · **Depende de:** diorama F4 (arte) para la versión completa

> **Implementado (B + C-mínimo + primera D, como recomendaba el plan):** `Pet.Condition` calculado (Shared, `[JsonIgnore]`): Crystal > Weak (HP<40) > Hungry (hambre<`Pet.HungryAt`=30, EL MISMO umbral del push T2 — `HealthDecayHostedService.HungerWarnAt` ahora lo referencia) > Happy (hambre>60 y HP>70) > Normal. Cero estado nuevo; `Happiness` queda como columna dormida documentada (borrarla en la próxima migración que pase por ahí). **UI:** la barra "Felicidad" del Dashboard se reemplazó por **Salud** (dato real); burbuja de humor (✨/🥺/💔, tabla única en `PetVisuals.MoodEmoji`) flotando junto a la mascota en Dashboard **y** detalle de grupo (humores consistentes; en el update en vivo de SignalR se deriva solo de Health porque las compartidas no decaen hambre). **D:** al subir `TotalXp` desde la última vista, la mascota da 3 saltitos (~1.2 s) reusando el `FrameTick` del diorama (sin timers nuevos). Dev: `POST api/dev/hunger` para acelerar pruebas.
>
> **Verificado:** 7 tests de `Pet.Condition` (98 verdes) + en emulador contra server local: ✨ con 100/100, 🥺 con hambre 20 (y `{"condition":"Hungry"}` del server), vuelta a Normal al completar una tarea (hambre 20→35), 💔 con HP 30 y barra Salud 30%. La celebración de XP quedó verificada por código (ventana de 1.2 s en `OnFrameTick`); el petting y los sprites por humor (D/C-completo) esperan el arte F4.

## El quiebre (por qué)

El cariño —y por tanto la culpa útil que hace volver— nace de la **expresión emocional**, no de números. Hoy la mascota comunica su estado solo con barras: no se ve triste con hambre, ni débil con poca vida, ni celebra cuando ganas XP. Peor: la barra de `Happiness` que se muestra en el Dashboard está **muerta** — ningún código del servidor la modifica jamás. Una barra que nunca se mueve le enseña al usuario que las barras no importan, lo que socava también a las que sí (hambre, HP).

## Evidencia en el código

- `src/PetProductivity.Shared/Models/Pet.cs:19` — `Happiness` declarada ("Baja si ignoras tareas") pero sin un solo write server-side en todo el repo.
- `src/PetProductivity.Client/Views/DashboardPage.xaml:143-145` — la barra de Happiness se muestra como si fuera real.
- `src/PetProductivity.Client/ViewModels/DashboardViewModel.cs:314,323` — los únicos writes existentes son del **cliente** (0/100 hardcodeados) — cosmética local sin fuente de verdad.
- `src/PetProductivity.Client/Controls/RoomDiorama.cs` + `RoomSprites.cs` — el diorama ya dibuja la mascota por especie/etapa: el punto único donde inyectar estados visuales.
- CLAUDE.md registra `PetMood` como entidad persistida de la capa social (mood de mascotas compartidas) — revisar antes de crear otro concepto de "humor" paralelo.

## Opciones

### A. Cablear `Happiness` de verdad
Server-side: sube al completar tareas/foco, baja en el tick de decadencia (como el hambre pero más lenta), y modula el sprite/animación.
- **Pros:** honra lo que la UI ya promete; una dimensión de estado más rica (puede estar bien alimentada pero triste por abandono).
- **Contras:** duplica en gran parte lo que Hunger+Health ya expresan (las tres suben con tareas y bajan con abandono → tres barras correlacionadas que cuentan la misma historia); más estado que balancear y explicar.
- **Esfuerzo:** M · **Toca:** server + shared + cliente.

### B. Eliminar `Happiness` y derivar el humor de `Hunger`/`Health` existentes
Borrar la barra del Dashboard (y el campo, o dejarlo dormido) y computar un **humor** de solo lectura: `Feliz` (hambre>60 y HP>70), `Normal`, `Hambrienta` (hambre<30), `Débil` (HP<40), `Cristal`. Una propiedad calculada en `Pet` o en el cliente — sin migración, sin balanceo nuevo.
- **Pros:** cero estado nuevo, cero mentira, el humor siempre es coherente con las mecánicas reales; es la opción mínima que arregla el quiebre completo.
- **Contras:** se pierde la dimensión "feliz aunque hambrienta" (¿importa? probablemente no); hay que decidir qué hacer con la columna en BD (dejarla es gratis).
- **Esfuerzo:** S (lógica) · **Toca:** shared o cliente + XAML.

### C. Estados visuales por humor (la cara del sistema)
Sea A o B la fuente, el humor debe **verse**: sprite alterno o overlay por estado (orejas caídas + gotita = hambrienta; tumbada = débil; brillitos = feliz). Mínimo viable sin arte nuevo: tint/postura/emoji-burbuja sobre el sprite actual en `RoomDiorama`; versión completa: 3-4 variantes de sprite por especie/etapa (F4).
- **Pros:** es donde el quiebre realmente se cierra — el usuario ve sufrimiento/alegría, no porcentajes; la burbuja de emoji es implementable hoy con SkiaSharp sin esperar arte.
- **Contras:** la versión con sprites multiplica arte (3 especies × 3 etapas × 4 humores); por eso el fallback burbuja/tint importa.
- **Esfuerzo:** S (burbuja/tint) → L (sprites completos) · **Toca:** cliente (RoomDiorama) + arte.

### D. Micro-reacciones a eventos
Animaciones puntuales: saltito + partículas al ganar XP, mirar/usar un mueble recién comprado, dormir de noche (según reloj del dispositivo), reacción al toque (petting). Encaja con el plan F4 del diorama (Lottie/arte).
- **Pros:** transforma el diorama de "fondo bonito" a "criatura viva"; el petting da algo que hacer entre tareas (hoy no hay nada); refuerza cada recompensa en el momento exacto.
- **Contras:** puro cliente/arte, cadencia de producción; ninguna mecánica nueva (correcto: no debe haberla).
- **Esfuerzo:** M-L (incremental, animación a animación) · **Toca:** cliente + arte.

## Recomendación

**B + C-mínimo primero**: derivar el humor de Hunger/Health (sin estado nuevo) y mostrarlo YA con burbuja/postura/tint en el diorama — eso elimina la barra mentirosa y le da cara al estado real en un esfuerzo S. Después **D** al ritmo del arte de F4 (empezando por la celebración de XP, que refuerza el momento de recompensa del bucle central). **A** solo si tras B se echa de menos una dimensión emocional independiente — dudoso.

Coordinar con `PetMood` de la capa social para no tener dos nociones de humor con nombres distintos.

## Criterios de éxito / verificación

1. La barra de Happiness ya no existe en el Dashboard (o muestra un dato real si se eligió A).
2. Con hambre < 30 (acelerable vía `DevController`) la mascota se ve visiblemente hambrienta en el diorama; al completar una tarea que suba el hambre, vuelve a normal.
3. Con HP < 40 se ve débil; cristalizada se ve cristal (ya existe).
4. Al ganar XP hay una reacción visible en < 1 s.
5. Los humores son consistentes entre Dashboard y detalle de mascota de grupo.

## Dependencias

- **Diorama F4** para sprites/Lottie completos (la versión burbuja/tint no lo necesita).
- Sinergia con **T3** (estados visuales del cristal agrietado) y **T2** (el push de hambre y la cara hambrienta deben contar la misma historia).
