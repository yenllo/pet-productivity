# T29 — Banco de ideas futuras (candidatas, NO implementar sin decisión del dueño)

**Estado:** vivo, se alimenta de revisiones continuas de código/emulador · **Depende de:** —

Respeta la política de consolidación (`00-indice.md`): mientras T27/T4/T14 sigan abiertos, ninguna
idea de aquí se construye. Este archivo es solo el banco de candidatas — cada entrada nueva se
agrega, nunca se implementa de paso. Si una idea choca con un plan ya cerrado (T1-T28), gana el
plan cerrado; anotarlo y no reabrir la discusión.

## De diseño/interacción (ya detectadas probando, aparcadas hoy)

- **Drag de muebles.** Hoy: seleccionar → botón "Mover" → D-pad. El dueño lo sintió tedioso.
  Alternativa: arrastre directo con el dedo sobre el lienzo (`RoomDiorama` ya sabe convertir
  pantalla→grilla para el tap; falta trackear `PanGestureRecognizer` y resolver la celda bajo el
  dedo en tiempo real). Riesgo: en una grilla iso, "arriba" en pantalla no es un eje de la grilla —
  hay que decidir si el arrastre sigue los ejes de pantalla o los de la grilla (el D-pad actual ya
  optó por los ejes de grilla; un arrastre libre podría sentirse "raro" si no se le pone un imán a
  la celda más cercana). Necesita una sesión de prueba, no solo código.
- **Estilo nativo de `DisplayAlert`/`DisplayActionSheet`** en el flujo de comprobante de foto
  (consentimiento + elegir selfie/subir/ahora no). Desentona con el look Neo-Retro del resto de la
  app. Requiere un popup custom (`Border`+`VerticalStackLayout` sobre un `Grid` a pantalla
  completa, patrón que la app ya usa en otros overlays) — no es difícil, es trabajo de UI puro.

## Contenido y arte

- **Segunda fuente de arte propia.** Los muebles de Bongseng no son redistribuibles (ver
  `ASSETS.md`) y ya cubrieron lo esencial; si el catálogo crece mucho más, vale la pena tener una
  fuente 100% propia (o una licencia comercial-plena) para no quedar atado a un solo proveedor.
- **`RoomDiorama.cs` pasó las ~750 líneas** con los cambios de hoy (mascota en el lienzo, caché de
  paints). El propio T20-I5 ya preveía este umbral ("si F4/T5 lo empujan sobre ~1000 líneas,
  extraer la capa de animación a su propio archivo"). Todavía no toca, pero está más cerca.

## Monetización y lanzamiento (más allá de T14)

- **Presupuesto/alerta de costo de Gemini en producción**, no solo el rate-limit por usuario. Con
  tráfico público real (Play Store + la demo pública `/demo.html`), vale la pena un tope de gasto en
  Google Cloud con alerta, no solo confiar en el rate-limit de código.
- **La demo pública (`/demo.html`) une un CTA de conversión.** Hoy termina en "Google Play — coming
  soon". Cuando exista la ficha real, medir cuánta gente prueba el juez vs. cuánta más tarde
  instala — sin esa métrica, no se sabe si la demo cumple su propósito.

## Engagement (con cuidado — no reabrir la Serie A ya cerrada)

- **Comparación entre amigos/familias** (leaderboard ligero). Choca potencialmente con "el oro es
  solo cosmético, nunca competitivo" — **decisión del dueño antes de anotar esto como plan**, no
  solo idea de código.
- **Vista de "quién aportó qué" en la mascota de grupo.** Ya existe afecto por miembro
  (`PetDetailPage`); una vista de contribución (tareas/foco por persona esta semana) podría ser una
  extensión barata de datos que ya se calculan, sin agregar mecánica nueva.

## Calidad técnica para cuando haya más usuarios

- **`ShopController.SavePlacements` no valida propiedad del sprite contra el inventario** (el
  comentario de la línea 120 dice que sí valida; el código de la 132 dice explícitamente que no
  — son contradictorios). Impacto real: bajo (colocar es 100% cosmético, sin oro/XP, y el usuario
  solo se afecta a sí mismo), pero el comentario engañoso podría hacer que una futura auditoría de
  seguridad lo dé por cubierto sin serlo. Corregir el comentario es gratis; añadir el check de
  inventario es opcional y de bajo valor dado que no mueve la economía.
- **Historial de tareas capado en 50** (`FocusController.History`) — bien para hoy; si en el futuro
  se pide ver más atrás, hace falta paginación real, no solo subir el número.

## Proceso

- Este archivo lo alimenta el loop de revisión continua (ver sesión 2026-07-14). Cuando el dueño
  retome T27/T4/T14, esta lista es el primer lugar para mirar antes de diseñar desde cero.
