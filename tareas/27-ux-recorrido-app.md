# T27 — UX: recorrido de la app en la mano

**Estado:** ✅ sesión guiada con el dueño HECHA (2026-07-04, en teléfono real contra prod) → **~29 hallazgos capturados abajo, pendientes de arreglar por lotes** · **Tipo:** revisión · **Esfuerzo:** M · **Depende de:** T9

## Pre-pase autónomo (2026-07-03) — la parte objetiva del checklist

Recorrido de código + capturas del emulador (la navegación por taps quedó limitada por un ANR de SystemUI del emulador con GPU swiftshader — problema del entorno, no de la app; justo por eso el "feel" real necesita al dueño en teléfono).

**Hallazgo arreglado en esta pasada (criterio 3 — estados de carga/error):**
- `PetDetailPage` (detalle de mascota de grupo) **no tenía spinner ni mostraba errores**: en el cold start de Render (~22 s, T21) quedaba en blanco/vieja y muda, y una carga fallida (`GetDetailAsync` → null) no avisaba nada. **Arreglado:** `IsLoading` + `ActivityIndicator` en la primera carga + toast "No se pudo cargar la familia" al fallar. (Cliente: `PetDetailViewModel`/`PetDetailPage`.)

**Verificado OK por código (no requieren acción):**
- **Bucle central (criterio 2):** registrar tarea = 2 taps + tipear ("Registrar progreso" → escribir → "Reclamar recompensa"); con el quick-log de T7, 2 taps sin tipear. Buen ratio.
- **Loading (criterio 3):** Dashboard, Hub, Task, Shop, Focus ya tienen spinner. StatsPage no lo necesita (lee del usuario ya cargado).
- **Empty states:** History (`EmptyView`) y los chips de quick-log (ocultos si vacío) OK.
- **Errores de red (criterio 3):** el submit de tarea muestra mensaje; T13 añadió el aviso offline.
- **Dark mode (criterio 5):** app forzada a oscuro por defecto, todas las páginas usan brushes de `StaticResource` (sin colores hardcodeados sueltos) → coherente.
- **Navegación (criterio 6):** 5 pestañas + rutas push (TaskPage/Stats/PetDetail); estructura limpia en `AppShell`.

**Queda para la sesión con el dueño (lo que no se puede juzgar sin usarla):** onboarding "¿se entiende sin explicación?" (criterio 1), jerarquía/saturación del Dashboard ya cargado (criterio 4), sensación de fluidez entre pestañas y doble carga (criterio 6), qué momentos piden celebración y no la tienen (criterio 7), accesibilidad/tamaños de toque reales (criterio 8). Método: sesión guiada en teléfono real, grabar pantalla, ordenar fricciones por frecuencia×dolor.

---


## Por qué esta revisión

Todo el juicio de experiencia de los análisis previos salió de leer ViewModels y XAML por encima — nunca se recorrieron las pantallas. La fricción de UX se *siente*, no se lee: cuántos taps cuesta registrar una tarea, si el onboarding se entiende, si los estados de carga/error son visibles, si el dark mode funciona. Esta es la revisión que más se beneficia de hacerse **contigo navegando y yo anotando**, porque tú percibes la fricción en tiempo real.

## Qué revisar (checklist)

1. **Onboarding / primer uso:** ceremonia de nacimiento → nombrar → primera tarea. ¿Se entiende sin explicación? ¿El banner de invitado invita o molesta? ¿Cuándo aparece el muro de login?
2. **Registrar una tarea (el bucle central):** ¿cuántos taps desde abrir la app hasta ver la recompensa? ¿Se puede reducir? (cruza con T7-C quick-log).
3. **Estados de carga y error:** ¿hay spinner en las esperas (cold start de Render puede ser 30-60 s)? ¿Los errores de red se muestran o la pantalla queda muda? (cruza con T13, T17-logging).
4. **Legibilidad y jerarquía:** ¿la info importante (racha, estado de la mascota, recompensa) resalta, o compite con todo lo demás? El Dashboard ya está cargado (diorama + barras + ritual) — ¿satura?
5. **Dark mode / temas:** `ThemePreference` existe. ¿Funciona en todas las pantallas o hay contrastes rotos?
6. **Navegación entre las 5 pestañas:** ¿fluida? ¿Doble carga al volver? ¿El back button in-page (CommunityToolkit) se comporta?
7. **Feedback emocional y celebraciones:** hoy las evoluciones son silenciosas (T4-E), el feedback llega en inglés (T12). ¿Qué momentos piden celebración y no la tienen?
8. **Accesibilidad básica:** tamaños de toque, contraste, ¿texto escalable?

## Cómo

- Sesión guiada en emulador o teléfono real: el dueño navega un flujo, se anota la fricción de cada paso.
- Grabar la pantalla para revisar taps/tiempos después.
- Priorizar por frecuencia: el bucle de tarea se usa 100×/semana; el onboarding, 1×.

## Salida esperada

Lista de fricciones ordenadas por (frecuencia × dolor), cada una con una propuesta concreta. Las que sean grandes se convierten en su propio plan; las pequeñas entran como checklist de pulido.

## Dependencias

- **T9** primero. Solapa con T7 (quick-log), T13 (estados offline/error), T12 (idioma feedback), T4-E (celebraciones). Idealmente **después** de T12 y T2 para juzgar el estado ya mejorado.

---

## Sesión guiada con el dueño (2026-07-04) — hallazgos en teléfono real contra producción

Instalación limpia, recorrido como usuario nuevo (invitado → luego login Google). ~29 hallazgos. Ordenados por tipo; el número entre () es prioridad tentativa (frecuencia×dolor).

### 🔴 Bugs funcionales
1. **"Flash" pervasivo (alta prioridad):** al abrir la app se ve la pantalla principal un instante → flash de microsegundo → vuelve a la principal. Se repite al moverse por el menú principal, al entrar a "Registrar progreso", y durante el foco. Sospecha: re-navegación/rehidratación que reconstruye la página.
2. **Foco — se puede salir del foco activo y perder el acceso:** con un foco en curso puedes salir al menú principal y navegar; al volver al menú de foco pide "escribe algo antes de iniciar" aunque el foco sigue activo (notificación "modo foco activo" presente, restricción de apps funcionando). No hay forma de volver al foco en curso desde la UI → hay que cancelarlo por la notificación.
3. **Tocar la notificación "modo foco activo" NO devuelve al menú de foco** (debería deep-linkear a la sesión activa).
4. **La foto de comprobante del minuto 5 NUNCA llegó:** se esperó pasado el minuto 5 sin ninguna notificación pidiendo la foto. El comprobante a mitad de sesión no dispara.
5. **Permisos de foco no se detectan al concederse:** tras dar un permiso y volver, sigue mostrando "falta X". Falta re-chequear el estado (OnResume).
6. **"Apps permitidas" mostró 2/3 con "documentos y gemini" de la versión ANTERIOR** — en instalación limpia. La selección sobrevivió al reinstall (¿dónde se guarda que no se limpió? investigar — ¿shared prefs del servicio Android, o servidor?).
7. **Menú de selección de apps del foco se traba al scrollear:** cada icono recarga al subir/bajar; deberían cachearse mientras el menú está abierto.
8. **Navegación por pestañas conserva el sub-stack:** Perfil → "ver historial" → ir a Mascota → volver a Perfil → apareces DENTRO de "ver historial", no en el menú Perfil. Falta resetear el stack por pestaña.
9. **Familias y Modo Foco NO están bloqueados para invitados** — deberían requerir login (como sí lo pide el resto).
10. **Quick-log (repetir de un toque):** mostró una notificación que desapareció demasiado rápido para leerse.

### 🟠 Layout / sobreposiciones (lo que MÁS le molestó)
11. **El rectángulo de evolución (abajo) choca/sobrepone con las 2 barras (hambre/salud) de arriba.**
12. **Cómo aparecen el menú de estado (offline/working/…) y el editor de texto del ritual:** sobreposiciones feas. El dueño quiere que el input "aparezca de la nada" pero con el **estilo visual del input de registrar tarea / registro manual de cuenta**, no el actual. Mismo reclamo para el ingresador de código de grupo (Familias).
13. **Grid del ritual sin borde/separador inferior** — parece que sigue hacia abajo. LO MISMO le pasa a la caja del historial de tareas que aparece bajo el ritual tras la primera tarea.
14. **Indicador offline aparece cortado (arriba).**
15. **Botón "Editar cuarto" inconsistente:** como invitado se ve cortado / más corto que el rectángulo del cuarto y desplazado a la izquierda; tras login con Google, SOBREPASA el rectángulo del diorama.
16. **Texto de ayuda del ritual cortado:** "toca una celda para ???? ese hábito" — no se lee completo.

### 🟡 Visual / arte / pulido
17. **La mascota flota "en la nada" frente a ti, no dentro del cuarto** (sprite/posicionamiento; tema de arte/diorama F4).
18. **Chip 🔥 de racha con punto naranja:** no se entiende qué es; el punto naranja parece badge de notificación/tappable. Necesita label o dejar de parecer botón.
19. **Botón "Registrar progreso" tiene un icono de calavera a la derecha — quitar.**
20. **Feedback de recompensa "¡Hecho! Dificultad 4/10 XP +48 Oro +20" se siente plano/poco motivante** (y el historial también). Celebración débil.
21. **Tienda desordenada:** se ve bien y gusta el amarillo (comprable/no), pero hay que ordenarla/categorizar mejor.
22. **Tienda: la barra de categorías es desplazable horizontal pero no hay pista visual** de que se puede scrollear a la derecha.
23. **Tienda: algunas imágenes de objetos cargan lento.**
24. **Perfil: "Invitado <id>" y "@<id_con_guion>" se ven feos.**
25. **Modo oscuro solo cambia algunas cosas** (barra de menús, algunas líneas), no el entorno completo.

### 🔵 Estructura / config
26. **Agregar "Idiomas" en Ajustes**, adaptado al idioma del sistema; empezar con **inglés + español** (conecta con T16).
27. **"ver historial" → "ver historial de tareas".**
28. **Quitar de Ajustes toda la sección "Servidor"** (ya no se usa) → moverla a un menú aparte de **"Desarrollador"**, accesible por un botón como el de "Créditos" en esa misma sección.
29. **"Recompensas por foto" debe venir ACTIVADO por defecto** (estaba desactivado).

### Nota de infra
- UptimeRobot marcó 2 incidentes de "down" (falsos: prod verificado 200 en 0.2s y la app se usó entera). Causa: cold start del free tier que supera el timeout de 30s de UptimeRobot en algún chequeo. Mitigación: monitor a IPv4-only. No es caída real.

## Plan de arreglo por lotes (próxima sesión)

Cada cambio de cliente = recompilar + reinstalar (~4 min), así que se arregla por lotes: un lote entero → un rebuild → el dueño re-revisa en el teléfono.

- **Lote 1 — Bugs funcionales** (empezar aquí): investigar y arreglar el **flash** (#1) y el **cluster de Foco** (#2 salir/volver, #3 notificación deep-link, #4 foto del min 5 no dispara, #5 permisos no detectados, #6 apps persisten tras reinstall, #7 lag de scroll); gate de login en **Familias y Foco** (#9); **reset del stack de navegación** por pestaña (#8); notificación del quick-log ilegible (#10).
- **Lote 2 — Layout + quick wins:** sobreposiciones (#11 evolución vs barras, #12 menús, #13 bordes inferiores, #14 offline cortado, #15 botón editar cuarto, #16 texto ritual cortado); quitar calavera (#19); "ver historial" → "ver historial de tareas" (#27); recompensa-por-foto ON por defecto (#29); sección Servidor → menú Desarrollador (#28); claridad del chip de racha (#18); perfil invitado feo (#24).
- **Lote 3 — Decisiones de diseño (preguntar al dueño):** rediseño del input del ritual/estado/código de grupo con el estilo del input de registrar tarea (#12); ordenar la tienda + pista de scroll horizontal (#21, #22); celebración de recompensa más satisfactoria (#20); dark mode completo (#25); **Idioma ES/EN adaptado al sistema** (#26, conecta con T16); imágenes de tienda lentas (#23).
- **Arte (F4):** mascota flotando fuera del cuarto (#17).

**Estado al dormir (2026-07-04):** APK nuevo instalado limpio en el teléfono del dueño (probado invitado→login Google). Prod sana (health 200 ~0.2s). UptimeRobot marca falsos "down" por cold start del free tier → cambiar el monitor a IPv4-only. Nada de esto bloquea; son mejoras de pulido sobre una app que funciona.

## Idea del dueño (2026-07-04) — editor de cuarto inmersivo estilo Game Boy

Reemplaza el "mover objetos" actual (aún sin programar; hoy es difícil hacer click preciso a cada objeto). Diseño propuesto:

1. **Entrar a modo edición = inmersivo:** se hace **zoom a la habitación** y todo lo demás de la UI **desaparece** (foco total en el cuarto).
2. **Lista de todos los objetos del cuarto** en pantalla → seleccionar cuál editar (evita tener que atinarle con el dedo a un sprite chico).
3. **Control tipo Game Boy (D-pad):** mueve el objeto seleccionado **bloque a bloque** por la grilla.
4. **Botones A y B:** rotar / confirmar.
5. **Botón externo "cancelar última edición"** (deshacer el último movimiento).

Encaja con: hallazgo #15 (botón "Editar cuarto" con layout roto) y el reclamo de que seleccionar objetos por tap es impreciso. Es un rediseño de feature (más que un fix de pulido) → candidato a plan propio o parte del Lote 3. Reutiliza la grilla lógica y `PlacedFurniture` ya existentes (`RoomGrid`/`RoomDiorama`, colocación persistida server-side).

## Progreso de arreglos (2026-07-04, commits 2580ae9 + da3676b)

**Lote 1 — HECHO** (compila; verificado en emulador que arranca): #1 flash, #2 volver-al-foco (banner Dashboard), #4 foto min5 (notificación + re-oferta), #6 allowBackup=false (también resuelve #29 y el A1 de seguridad), #10 toast Long.

**Lote 2 — parcial:** ✅ #19 calavera fuera del CTA · ✅ #11 sobreposición de tarjetas (Spacing 12→18 + sombra contenida; **VERIFICADO en emulador: la sobreposición desapareció**) · ✅ #27 rename historial · ✅ #29 (vía #6).
Pendientes de Lote 2: #13 (borde inferior del ritual/historial), #14 (chip "Offline" cortado — va con el rediseño de inputs #12), #15 (botón editar cuarto), #16 (texto del ritual cortado), #18 (chip de racha), #24 (perfil invitado feo), #28 (Servidor → menú Desarrollador).

**Lote 3 — pendiente** (decisiones/diseño): #12 rediseño de inputs (ritual/estado/código), #20 celebración, #21/#22 tienda, #23 imágenes lentas, #25 dark mode completo, #26 Idioma ES/EN.

## Progreso (2026-07-08, sesión de verificación + Lote 3)

**Lote 2 — VERIFICADO EN EMULADOR (cuenta real contra prod):** ✅ #28 (URL/ping/modelo IA dentro de "Opciones de desarrollador"; sección Servidor fuera de Ajustes) · ✅ #12 código de grupo ("Unirse por código" = overlay estilizado) · ✅ #12 ritual ("Nombra este hábito" = overlay estilizado) · ✅ #14 selector "Tu estado" = overlay (el chip actualiza en vivo) · ✅ #16 texto de ayuda del ritual completo ("Toca una celda para nombrar ese hábito.") · ✅ #18 chip 🔥 muestra toast explicativo · ✅ #11/#15/#19 ya verificados antes.

**Bug encontrado y arreglado (al verificar #24):** tras "Cerrar sesión" la UI seguía mostrando al usuario ANTERIOR (nombre, mascota, stats) aunque la sesión nueva (invitado) ya estaba activa — `GameDataService.CurrentUser` no se limpiaba y `InitializeAsync` retorna temprano si hay caché. Fix: `SetUser(null)` en el logout (`ProfileViewModel`). Afectaba también a registrar tareas post-logout (iban a la cuenta nueva mientras la UI mostraba la vieja).

**Lote 3 — hecho y VERIFICADO EN EMULADOR (2026-07-08):**
- ✅ #24 perfil invitado: "Invitado" limpio sin id/handle + CTA "Iniciar sesión o registrarse" (verificado con invitado real).
- ✅ #21 tienda ordenada: chips en orden fijo (Todo·Muebles·Decoración·Vida·…) e ítems agrupados por categoría y barato→caro (verificado: pág. 1 = muebles 200→210).
- ✅ #22 chevron "›" fijo a la derecha de la barra de categorías (pista de scroll; verificado).
- ✅ #23 sprites del catálogo copiados una vez a CacheDirectory y servidos con `FromFile` (Glide cachea; antes `FromStream` re-leía el asset en cada página). Verificado: página 2 pinta 8/9 imágenes en ~1.5 s.
- ✅ #20 celebración: overlay con pop (escala+fade), estrellas de dificultad, +XP/+Oro grandes, feedback emocional de la IA y háptica (permiso VIBRATE añadido). Verificado end-to-end con IA real (tarjeta opaca, sin transparentar lo de atrás).
- ✅ #25 modo claro "suave" (decisión del dueño 2026-07-08: superficies/tarjetas/tinta claras, arte intacto, aplicado AL INSTANTE): `ThemeService` pone overrides como entradas directas de `Application.Resources` (prioridad sobre Colors.xaml); los estilos del design system usan `DynamicResource` para las claves que cambian; el toggle reconstruye el AppShell y vuelve a Ajustes. VERIFICADO en emulador: Ajustes/Tienda/Mascota legibles en claro.
- ✅ #26 idioma ES/EN (decisión del dueño: Sistema+selector, IA en el idioma del usuario): localizador `L` (clave = string en español; fallback visible a ES), `{loc:T '…'}` en XAML (~180 strings), `LocFmt` para StringFormats, mensajes C# con `L.T/L.F`, selector "Idioma" en Ajustes que cicla Sistema→Español→English (rebuild del Shell). El cliente manda `Language` en `/api/tasks` y el Tasador de Gemini responde el feedback en ese idioma (server: `SubmitTaskRequest.Language` → `PetService` → `AiJudgeService`). VERIFICADO: emulador en-US arranca la app en inglés; ciclar a Español cambia toda la UI al instante.
- **Bug arreglado al implementar #25:** `LoadSettings()` asignaba `IsDarkTheme` al construir el VM → con el rebuild nuevo, cada apertura de Ajustes reconstruía el Shell en bucle (ANR + NRE de `ShellToolbarTracker`). Fix: bandera `_initializing` para que el partial method solo reaccione a cambios del usuario.
- **Gaps conocidos (menores):** nombres/descripciones de ítems del catálogo siguen en ES (son datos de `Catalog/`); el fallback heurístico del server responde en ES; arquetipos del crear-familia en ES (viajan al server).

**Nota emulador:** el invitado no cargó datos del server en el emulador (bars/stats en 0, "Offline") — red del emulador, no del código (en teléfono real carga). La verificación visual fina del resto (con datos) conviene hacerla en el teléfono del dueño.
