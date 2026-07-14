# T28 — Documentación vs. realidad

**Estado:** ✅ hecho (2026-07-02) · **Tipo:** revisión · **Esfuerzo:** S · **Depende de:** —

## Por qué esta revisión

`ROADMAP.md` y la carpeta `docs/` (untracked, nueva) nunca se abrieron en los análisis. Con 20+ planes nuevos en `tareas/`, hay que verificar que no contradigan el roadmap oficial ni dupliquen algo ya planeado ahí, y que el `CLAUDE.md` (que "manda sobre suposiciones") siga siendo verdad tras el diorama y la migración a .NET 10.

## Qué revisar (checklist)

1. **`ROADMAP.md` vs. `tareas/`:** ¿alguna tarea T1-T20 ya estaba planeada/hecha en el roadmap? ¿Alguna fase futura del roadmap contradice una recomendación de `tareas/`? Reconciliar (el roadmap es el plan por fases oficial según CLAUDE.md).
2. **`docs/` (nueva, untracked):** qué contiene, si debe commitearse (T9), si duplica o complementa `tareas/`.
3. **`CLAUDE.md` desactualizado:** dice ".NET 8 MAUI" en la tabla de stack, pero el proyecto migró a .NET 10 (working tree). El estado "Fases 0-5 CERRADAS" no menciona el diorama (F1-F3) ni el foco grupal. Actualizar para que el próximo agente/sesión no parta con datos viejos.
4. **`DESIGN.md`:** ya marcado como histórico en CLAUDE.md; confirmar que nada lo trate como vigente.
5. **Memoria del proyecto** (`.claude/.../memory/`): ya se actualizó con los planes; verificar que `diorama-vivo-plan.md` y `pendientes-futuros.md` no contradigan el estado real tras T9.

## Cómo

- Leer `ROADMAP.md`, `docs/*`, `DESIGN.md` completos y cotejar con `tareas/00-indice.md` y el código actual.
- Editar `CLAUDE.md` con el stack real (.NET 10), el diorama y lo que se cierre en T9.

## Salida esperada

`CLAUDE.md` y `ROADMAP.md` reflejando la realidad post-diorama/.NET 10; una nota en `tareas/00-indice.md` de cualquier solape con el roadmap; decisión sobre `docs/`.

## Dependencias

- Independiente, pero conviene **después de T9** (para documentar el estado ya commiteado) y como cierre tras avanzar varias tareas (mantener CLAUDE.md vivo).

## Resultado (2026-07-02)

1. **ROADMAP vs. tareas/:** sin contradicciones — las fases 0–5 del roadmap están cerradas y `tareas/` es el backlog posterior. Se añadió al ROADMAP la sección "Post-Fase 5" (migración .NET 10, foco con comprobante, diorama+tienda, seguridad T22, backlog). Único solape real: PLAN-diorama F5.4 (billing) ⊂ T14.
2. **`docs/`:** ya estaba trackeada (T9). Contiene el plan de handoff del diorama + 2 specs de arte — **complementa** a `tareas/`, se queda. Se le puso addendum de estado (commiteado/desplegado/verificado-en-emulador).
3. **CLAUDE.md:** actualizado — stack .NET 10, sección Post-Fase 5 (foco/diorama/catálogo/seguridad), pendientes 🟡 depurados (2 eran obsoletos: `TaskItem` SÍ se persiste; nombre de solicitante arreglado), comandos (`net10.0-android`, package `yenllo.org.PetProductivity`, tip `AndroidSdkDirectory`).
4. **DESIGN.md:** ya tenía el banner de histórico — sin cambios.
5. **Memoria del agente:** `diorama-vivo-plan` y `pendientes-futuros` actualizadas (commit+deploy 2026-07-02; revisión visual del dueño pendiente post-exámenes).

De paso se cerró el resto de T9: **push a Render hecho (2026-07-02)** — main `504de95..6fa7f88` (14 commits: diorama+catálogo+backlog+seguridad), fast-forward desde `feature/modular-room`; ramas `feature/modular-room` (local+origin) y `refactor-security-phase-zero` (worktree Antigravity, 0 commits únicos) **borradas**.
