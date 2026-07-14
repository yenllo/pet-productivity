# T9 — Commitear el working tree (proceso, no código)

**Estado:** pendiente · **Esfuerzo global:** S · **Depende de:** — (bloquea todo lo demás: es el riesgo #1)

## El quiebre (por qué)

Semanas de trabajo viven sin commit sobre `fa643a3` (rama `feature/modular-room`): el diorama F1-F3, los estilos de sala en tienda (compra + equipar), el sistema de muebles con ~40 sprites nuevos, la migración a .NET 10 de los 3 proyectos, y **una migración de EF sin aplicar en producción**. Un `git checkout --` torpe, un `reset` equivocado o un disco que falla borra todo eso sin recuperación. Ningún plan de mejora tiene sentido mientras el trabajo ya hecho esté en riesgo.

## Evidencia

- `git status`: ~15 archivos modificados (cliente, server, shared, csproj) + docs/ + `RoomGrid.cs` + ~40 PNG `obj_*` sin trackear.
- Memoria del proyecto: F1-F3 del diorama "HECHAS en working tree (sin commit; migración sin aplicar)"; migración .NET 10 también en working tree.
- Último commit `fa643a3` es anterior a todo esto.

## Opciones

### A. Un commit único "wip: todo"
- **Pros:** 2 minutos; el riesgo de pérdida desaparece ya.
- **Contras:** historial ilegible; imposible revertir una pieza (¿y si .NET 10 da problemas en Render pero el diorama está bien?); mezcla migración de plataforma con features.
- **Esfuerzo:** S.

### B. Commits por fase temática (recomendada)
Trocear con `git add -p` / por archivo en 3-4 commits coherentes, en este orden:
1. `chore: migrar a .NET 10` — los 3 `.csproj` + breaks de API asociados (OpenAPI nativo, SkiaSharp 4, Plugin.Firebase 4).
2. `feat: grilla y muebles del diorama` — `RoomGrid.cs`, `RoomDiorama.cs`, `RoomSprites.cs`, PNGs `obj_*`, `PlacedFurniture`.
3. `feat: estilos de sala en tienda (compra+equipa)` — `ShopController`, `ShopViewModel`, `ShopPage`, `User.ActiveRoomStyle`, migración EF.
4. `docs:` — la carpeta `docs/` y `tareas/`.
- **Pros:** cada pieza es revertible/desplegable por separado; el deploy a Render (que se dispara con `git push`) se puede hacer commit a commit verificando.
- **Contras:** requiere 30-60 min de cuidado para no partir un cambio que cruza archivos (p. ej. csproj toca tanto .NET 10 como recursos nuevos).
- **Esfuerzo:** S-M.

### C. B + aplicar/desplegar la migración pendiente en el mismo esfuerzo
Tras commitear: `git push` por etapas → Render redeploya → `Migrate()` al arranque aplica la migración → verificar estilos+muebles en vivo (el pendiente que ya está anotado en la memoria del proyecto).
- **Pros:** cierra el ciclo completo (el trabajo no solo está a salvo: está en producción); la migración EF es aditiva, riesgo bajo.
- **Contras:** desplegar .NET 10 a Render puede requerir tocar el Dockerfile (imagen base net10) — verificar antes de pushear; hacerlo con tiempo para mirar logs.
- **Esfuerzo:** M.

## Recomendación

**B ya, C enseguida.** Si el troceo fino se complica, degradar a 2 commits (plataforma / features) antes que posponer — cualquier commit es mejor que ninguno. Regla a futuro: ninguna fase "HECHA" queda sin commit al cerrar la sesión en que se hizo.

## Criterios de éxito / verificación

1. `git status` limpio (o solo basura conocida del repo).
2. Cada commit compila por sí solo (`dotnet build` del server al menos en los commits que lo tocan).
3. (C) Render desplegado, migración aplicada, estilos/muebles verificados en teléfono real contra producción.

## Dependencias

- Ninguna entrante. **Salientes:** todas — T10-T16 editan archivos que hoy tienen cambios sin commit; hacerlas antes de T9 mezclaría trabajo nuevo con el pendiente.
