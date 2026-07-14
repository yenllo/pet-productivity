# T3 — Fénix: la puerta de regreso está invertida

**Estado:** ✅ hecho (2026-07-03) — E + A + F como recomendaba el plan; B (vía social) queda para cuando la capa social tenga más uso; sprites de grieta esperan arte F4 (por ahora contador UI) · **Esfuerzo global:** M-L · **Depende de:** T2 ✅, T5 ✅

> **Implementado:** **E (escudo de ausencia)** — `DecayMath.ApplyPendingDecay(pet, now, lastActivity)`: sin actividad hace >3 días (`AbsenceSleepDays`), la decadencia solo corre los primeros 3 días tras la última actividad y lo dormido SE PERDONA (el reloj salta a ahora); aplicado en los 3 call-sites (materializar usuario, barrido de push, y premiar — ahí con el `LastActivityDate` viejo porque `DailyStreak.Advance` corre después). Consecuencia deliberada: el abandono total ya no cristaliza; el cristal queda para quien está activo y la descuida. **A (vía acumulativa)** — `Pet.RevivalProgress` + `LastRevivalCreditDay` (migración `AddRevivalProgress`): con la mascota cristalizada, un foco completado o una tarea ≥5 (`RevivalCreditDifficulty`) en un día local distinto suma 1 grieta; a las 3 (`RevivalDaysNeeded`) revive igual que la vía épica (20% HP + gracia 24 h, vía `Revive()` compartido que además resetea el progreso — criterio 4). Mensajes del server muestran "N/3 grietas"; el overlay de cristal del Dashboard muestra el contador 💠 y explica las dos puertas. **F** — tarea ≥9 sigue reviviendo al instante (TryRevive intacto, solo refactorizado a `Revive()` común). **C y D descartadas** como decía el plan.
>
> **Verificado:** 7 tests (108 verdes): 3 días distintos reviven con HP/gracia correctos y reset; mismo día no duplica; épica instantánea resetea grietas; ausencia de 10 días aplica solo 36 ticks y perdona el resto; activo/sin-registro = regla normal. En vivo (REST + server local): matar mascota → tarea juzgada 6 por Gemini → "El cristal lleva 1/3 grietas" + `RevivalProgress=1` persistido.

## El quiebre (por qué)

Quien abandonó una semana vuelve y encuentra su mascota cristalizada; revivirla exige UNA tarea de dificultad ≥9, que el propio prompt de la IA define como "logros excepcionales: publicar un libro, correr un maratón, defender la tesis". La persona **menos** motivada de la base de usuarios enfrenta la barrera **más** alta — el diseño castiga exactamente al usuario que había que recuperar. La temática Fénix es buena; la puerta de regreso, no. Es la diferencia entre recuperar usuarios caídos y confirmarles que se vayan.

## Evidencia en el código

- `src/PetProductivity.Shared/Models/Pet.cs:70-88` — `TryRevive`: solo `taskDifficulty >= 9`; revive con 20% HP + escudo de gracia 24 h.
- `src/PetProductivity.Server/Services/AiJudgeService.cs:39` — regla 5 del prompt: dificultad 9-10 = "Exceptional, rare achievements".
- `src/PetProductivity.Server/Services/HealthDecayHostedService.cs` — −5 hambre cada 2 h; con hambre 0, −5 HP cada 2 h → de 100% de hambre a cristal en ~7 días de abandono total.
- `src/PetProductivity.Server/Services/PetService.cs:91-98` — mascota cristalizada: toda tarea se trata como intento de revivir; XP/oro no se acumulan (`Pet.cs:113`: `AddStatXp` retorna si está cristalizada).

## Opciones

### A. Vía acumulativa: el esfuerzo sostenido agrieta el cristal
Nuevo campo `RevivalProgress` en `Pet`. Cada foco completado (o tarea de dificultad ≥5) **en un día distinto** suma 1; a las 3 → revive. Visual: el cristal se agrieta con cada avance (3 estados de sprite).
- **Pros:** re-entrada = "vuelve 3 días seguidos", que es exactamente el hábito que se quiere reconstruir; el progreso visible (grieta) es su propio gancho de retorno; conserva gravedad (no es gratis).
- **Contras:** campo nuevo + migración; necesita 2-3 sprites/overlays de grieta (o un contador UI si no hay arte).
- **Esfuerzo:** M · **Toca:** server + migración + cliente (UI de progreso) + arte opcional.

### B. Vía social: la familia dona calor
Los miembros de los grupos del usuario pueden tocar "dar calor" al cristal (1 vez/día cada uno); N donaciones → revive.
- **Pros:** convierte la muerte en un evento social (te extrañan → vuelves); refuerza la capa de grupos; genera push natural ("Ana le dio calor a Moko").
- **Contras:** solo funciona si el usuario TIENE grupo (la mascota personal de un usuario solitario queda igual de muerta); más superficie: endpoint + UI + push.
- **Esfuerzo:** M-L · **Toca:** server + cliente + T2.

### C. Vía oro: pagar la resurrección
Ítem "Lágrima de Fénix" en el catálogo.
- **Pros:** trivial de implementar (el flujo `buy` + efecto ya existe: la Poción cura 50 en `ShopController.cs:84-85`); sink de oro potente.
- **Contras:** **rompe la regla del proyecto "el oro es solo cosmético"** — pagar por deshacer el castigo vacía la mecánica de muerte y enseña que todo es comprable; además el usuario caído probablemente ya gastó su oro. Documentada por completitud; desaconsejada.
- **Esfuerzo:** S · **Toca:** catálogo + server.

### D. Hibernación en vez de muerte (HP mínimo 1)
La decadencia nunca mata: al llegar a HP bajo la mascota queda "debilitada" (estado visual lamentable, sin bonos) y se recupera cuidándola. La cristalización desaparece o queda solo para eventos especiales.
- **Pros:** elimina el cliff de desinstalación por completo; cero barrera de re-entrada.
- **Contras:** desarma la aversión a la pérdida — sin muerte posible, el cuidado pierde peso y el Fénix (mecánica identitaria del juego, está en el CLAUDE.md) muere de facto. Cambia el diseño, no lo ajusta.
- **Esfuerzo:** S-M · **Toca:** server (+ estados visuales de T5).

### E. Escudo de ausencia: dormirse antes que morir
Si el usuario no tiene actividad hace X días (p. ej. 3), la mascota entra en "sueño profundo": la decadencia se PAUSA (el hosted service salta mascotas cuyo dueño está ausente). Al volver, la mascota despierta hambrienta pero viva. La cristalización queda reservada para el abandono *estando activo* (abres la app pero la ignoras).
- **Pros:** distingue "se fue de vacaciones / abandonó" (no castigar: ya no le duele) de "está activo y la descuida" (castigar: sí le duele); es 1 condición extra en el `Where` del tick usando `LastActivityDate` de T1; precedente real (Pokémon GO pausa rachas, Tamagotchi moderno tiene "hotel").
- **Contras:** necesita `LastActivityDate` (T1); matiz: el castigo por abandono total desaparece — pero ese castigo nunca retuvo a nadie que ya se fue.
- **Esfuerzo:** S · **Toca:** server (HealthDecayHostedService) + depende de T1.

### F. Conservar el sacrificio épico como vía rápida (transversal)
En cualquier variante A/B/E, la tarea ≥9 sigue reviviendo al instante. La épica se mantiene; deja de ser la única puerta.
- **Esfuerzo:** 0 (es no tocar `TryRevive`).

## Recomendación

**E + A + F**: el escudo de ausencia evita que el usuario caído vuelva a un cadáver (ataca la causa), la vía acumulativa da una salida digna cuando la cristalización sí ocurre (usuario activo que descuidó), y el sacrificio épico queda como atajo legendario. **B** después, cuando la capa social tenga más uso — es la versión más bonita pero la que menos usuarios cubre hoy. **C** no (rompe la regla de oro) y **D** no (mata la mecánica identitaria).

Complemento de T2: push *antes* de morir ("Moko se está debilitando") — la mejor resurrección es la muerte que no ocurrió.

## Criterios de éxito / verificación

1. Usuario sin actividad 3+ días → el tick de decadencia lo salta; al volver, mascota viva y hambrienta.
2. Mascota cristalizada: 3 focos en 3 días distintos → revive (20% HP + gracia 24 h, igual que hoy); 3 focos el mismo día → solo 1 de progreso.
3. Tarea juzgada ≥9 sigue reviviendo al instante.
4. `RevivalProgress` se resetea al revivir.
5. Test xUnit de `TryRevive` extendido + del filtro de ausencia del hosted service.

## Dependencias

- **T1** (`LastActivityDate`) para el escudo de ausencia.
- **T2** para avisar antes de la muerte y anunciar el progreso de la grieta.
- **T5/F4 diorama** para los sprites de cristal agrietado (con contador UI basta para lanzar).
