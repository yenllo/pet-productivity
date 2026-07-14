# Historial de desarrollo

PetProductivity se empezó el **5 de diciembre de 2025** y se desarrolló en un repositorio
privado. Este repositorio público es una republicación limpia de ese trabajo: el historial
original no se puede publicar porque sus commits contienen (a) credenciales antiguas —ya
rotadas— de Supabase y Gemini, y (b) sprites con licencia que prohíbe su redistribución.

Abajo está el registro real de commits, para que la cronología del proyecto sea verificable.

| Fecha | Commit |
|---|---|
| 2025-12-05 | feat: implement Shop, Stats, and TaskPage improvements |
| 2025-12-12 | feat: implement settings, theming, and notifications infrastructure |
| 2025-12-13 | Initial commit to Render: Supabase & Gemini |
| 2025-12-13 | Initial commit: Supabase and Gemini migration |
| 2025-12-13 | Fix connection string format for Npgsql |
| 2025-12-13 | Implement Backend Auth: User model, Controller, Migration |
| 2025-12-13 | Fix Android Crash: Register ViewModels, Fix HttpClient Scope, Add Crash Reporter |
| 2025-12-13 | Fix Supabase Hostname Typo & Port (IPv4 Support) |
| 2025-12-13 | feat: Implement Cloud AI integration and fix crash issues |
| 2026-06-16 | Refactor: Completar Fase 0 y Fase 1 (Sincronizacion, IA y Seguridad) |
| 2026-06-16 | feat: Shop api integration - Migrar tienda al backend y completar nube |
| 2026-06-19 | feat: rediseno navegacion (5 tabs) + Google Sign-In + cierre Fases 3/4 |
| 2026-06-19 | feat(fase5): N1 secrets/config + N2 auth de sesion JWT (compila, sin verificar) |
| 2026-06-19 | feat(fase5): N3 rate-limit del endpoint de IA (10/min por usuario) |
| 2026-06-19 | feat(fase5): N4 push FCM - lado servidor |
| 2026-06-19 | test(fase5): N5 primeros tests (xUnit) - 12 verdes |
| 2026-06-19 | feat(fase5): N4 push FCM - cliente + fix disparo de Frenesi |
| 2026-06-19 | fix(fase5): presencia en vivo al crear/unirse a familia tras conectar |
| 2026-06-19 | feat(fase5): push FCM con prioridad alta (mejor entrega en MIUI/Xiaomi) |
| 2026-06-19 | docs: cerrar Fase 5 + metodos de simulacion/pruebas reproducibles |
| 2026-06-21 | feat: rediseño Neo-Retro + nacimiento de grupo + remediación R1-R5 |
| 2026-06-21 | feat(anti-trampa): juez endurecido, decrecientes, modo foco y validación social |
| 2026-06-22 | feat(foco): modo foco v2 — duración elegible + bloqueo suave de apps + apartado con anillo |
| 2026-06-22 | feat(foco): íconos de apps en el selector del modo foco |
| 2026-06-22 | perf(carga): primer render no bloqueante + dedupe de fetch de usuario + endpoint health |
| 2026-06-23 | feat(mascota): sprites pixel-art Moko por especie y etapa (starters) |
| 2026-06-23 | feat(foco): extras (notificación, vibración, racha/min, meta diaria, escapes) + arreglos sueltos |
| 2026-06-24 | fix(foco): sin ANR + permisos detallados + picker async + lista blanca del sistema; sprites centrados + mascota tamaño tile; grupo reusa iniciales |
| 2026-06-24 | fix(ui): ritual sin scroll anidado + foco con overlay (no rebote brusco) + barras redondeadas |
| 2026-06-24 | fix(ui): solape Evolución/vitales (altura fija de grilla) + el foco encierra al usuario |
| 2026-06-24 | feat(foco): atrás muestra diálogo de cancelar + restaurar foco al reabrir tras cerrar desde recientes |
| 2026-06-24 | feat(foco): F0-F2 — comprobante con Gemini Vision + historial laboral + fix de solapamiento |
| 2026-06-25 | feat(foco): F3-F4 — foco grupal sincronizado + comprobante multi-persona |
| 2026-06-25 | feat(foco): comprobante como bonus opt-in (x2) en vez de penalización + switch en Ajustes |
| 2026-06-25 | fix(foco): cámara no cuenta como escape + indicador "en revisión" + registro como feed con fotos |
| 2026-06-25 | chore(foco): log del veredicto de Gemini Vision en /proof (verificable en producción) |
| 2026-06-25 | chore: migrar server/shared/tests/docker a .NET 10 |
| 2026-06-25 | chore: migrar cliente MAUI a .NET 10 |
| 2026-06-25 | feat: back button in-page (CommunityToolkit.Maui) + auditoría de código |
| 2026-06-26 | feat: room diorama with Bongseng sprites, shop room styles, furniture system |
| 2026-07-02 | fix(gitignore): trackear Catalog (el patrón *log lo excluía) |
| 2026-07-02 | feat(tienda): catálogo como fuente de verdad + estilos de sala + muebles (server/shared/BD) |
| 2026-07-02 | feat(cliente): diorama con muebles colocables + UI de tienda de estilos/objetos |
| 2026-07-02 | docs: specs de diorama y tienda |
| 2026-07-02 | docs(tareas): backlog de mejoras (engagement, salud técnica, código, revisiones) |
| 2026-07-02 | docs(tareas): marcar T9 hecho (commit) — pendiente el push a Render |
| 2026-07-02 | docs(seguridad): informe de auditoría T22 + marcar hecho en índice |
| 2026-07-02 | fix(seguridad): rate-limit en login/register + piso de entropía del secreto JWT |
| 2026-07-02 | fix(seguridad): fijar Microsoft.OpenApi 2.9.0 parcheada (A3, GHSA-v5pm-xwqc-g5wc) |
| 2026-07-02 | fix(seguridad): validar pertenencia en detalle y solicitudes de grupo (M1, IDOR) |
| 2026-07-02 | fix(seguridad): endurecer /proof y el juez de IA; ocultar el hash de contraseña |
| 2026-07-02 | docs(seguridad): estado de remediación T22 (lote server-side hecho) |
| 2026-07-02 | docs(seguridad): C0 — endurecer auth como bloqueante de lanzamiento (T14) |
| 2026-07-02 | docs(seguridad): confirmar rotación del secreto JWT (T22/C1) |
| 2026-07-02 | docs(T28): CLAUDE.md y ROADMAP al día (.NET 10, diorama+foco, comandos); cerrar T9 con el deploy |
| 2026-07-02 | fix(bucle): T11 — transacción en foco, lock por aprobación, TaskId real, sin fallback de texto plano |
| 2026-07-02 | feat(ia): T12 — juicio y feedback emocional en UNA llamada a Gemini, en español |
| 2026-07-02 | chore(T18): purga de código muerto |
| 2026-07-02 | fix(deploy): copiar Catalog/ a la imagen Docker — la tienda de producción estaba VACÍA |
| 2026-07-02 | docs(tareas): cerrar T18 con smoke prod y anotar el bug incidental del catálogo en T21 |
| 2026-07-02 | feat(economia): T15+T19 — RewardMath con tabla de tests, CI, Sentry gated y efectos declarativos |
| 2026-07-02 | docs(tareas): cerrar T15+T19; nombrar DominantStatMargin en CheckEvolution |
| 2026-07-02 | docs(T26): informe de balance — veredicto por métrica contra metas del dueño (2-4 semanas) |
| 2026-07-02 | feat(balance): aplicar T26 — umbrales de evolución 50/600/2500, inanición -3, foco exento de rendimientos decrecientes, Cristal a 1500 |
| 2026-07-02 | fix(grupos): T24 — bordes de salida del grupo y auth interna |
| 2026-07-02 | fix(android): T25 — re-sync de estado al reconectar SignalR; ResizeJpeg fuera del hilo UI |
| 2026-07-02 | perf(bd): T23 — 9 índices de caminos calientes (migración AddHotPathIndexes) |
| 2026-07-03 | feat(dia-local): T8 — el 'hoy' del usuario corta a SU medianoche, no a la UTC |
| 2026-07-03 | feat(racha): T1 — racha diaria real + congelador + visible en Dashboard |
| 2026-07-03 | feat(push): T2 — la app avisa fuera de la app (hambre, debilidad, cristal y racha nocturna) |
| 2026-07-03 | feat(grupos): T6 — el silencio de la familia otorga (auto-aprobación a 48h) + push + countdown |
| 2026-07-03 | feat(decadencia): T10 — lazy con LastDecayAt; el juego ya no depende del uptime de Render |
| 2026-07-03 | fix(bd): FromJson tolerante a columnas JSON vacías — el login estaba caído en prod |
| 2026-07-03 | refactor(cliente): T17 — contrato compartido + helper HTTP único |
| 2026-07-03 | feat(offline): T13 — el texto del usuario ya no se pierde sin señal |
| 2026-07-03 | feat(mascota): T5 — la mascota se expresa; muere la barra de Felicidad fake |
| 2026-07-03 | feat(ritual): T7 — el ritual se vuelve TU meta del dia + quick-log de 1 tap |
| 2026-07-03 | chore(idioma): T16 — una sola voz en español + politica de consolidacion |
| 2026-07-03 | feat(fenix): T3 — la puerta de regreso ya no esta invertida |
| 2026-07-03 | feat(engagement): T4-E ceremonia de evolucion + T27 fix de cold-start |
| 2026-07-03 | feat(balance): T26 — Frenesí de ×2 a ×1.5 (decisión del dueño) |
| 2026-07-03 | feat(grupos): T24#4 — decadencia colectiva de la mascota de grupo |
| 2026-07-03 | docs(T15): Sentry + UptimeRobot montados por el dueño (cold start eliminado) |
| 2026-07-04 | chore(TEMP): endpoint /debug/boom para verificar Sentry (se quita enseguida) |
| 2026-07-04 | chore: quitar /debug/boom — Sentry verificado (capturó el 500 de prueba) |
| 2026-07-04 | docs(T15): Sentry verificado end-to-end (evento de prueba capturado) |
| 2026-07-04 | docs(T27): sesión de UX con el dueño en teléfono real — 29 hallazgos capturados |
| 2026-07-04 | docs(T27): plan de arreglo por lotes (1 funcionales, 2 layout, 3 diseño) |
| 2026-07-04 | docs(T27): idea del dueño — editor de cuarto inmersivo estilo Game Boy |
| 2026-07-04 | fix(ux-L1): T27 Lote 1 — flash, volver-al-foco, foto min5, backup, toast |
| 2026-07-04 | fix(ux-L2): T27 Lote 2 — quitar calavera del CTA, separar tarjetas, renombrar historial |
| 2026-07-04 | docs(T27): progreso Lote 1 (hecho) + Lote 2 (parcial, #11/#19 verificados en emulador) |
| 2026-07-04 | fix(ux-L2): T27 #11 real — Vitales a una sola Card + colapsar nombre vacío |
| 2026-07-04 | fix(ux-L2): T27 #15 — Editar cuarto a lápiz arriba-derecha del diorama |
| 2026-07-04 | fix(ux-L2): T27 #14 + #12(ritual) — selector de estado y renombrar hábito como overlays del app |
| 2026-07-08 | fix(ux-L2): T27 resto del Lote 2 — código de grupo, perfil invitado, chip racha, texto ritual, Servidor→Desarrollador |
| 2026-07-08 | fix(ux-L3): T27 Lote 3 + fix de logout — tienda ordenada, celebración de recompensa, caché de sprites |
| 2026-07-08 | feat(ux-L3): T27 #25 modo claro suave + #26 idioma ES/EN con IA localizada |
| 2026-07-08 | chore: mover ThemeService.cs a Services/ (quedo en ruta anidada por error) |
| 2026-07-08 | fix(ux-L3): #26 BackButton '< Atras' localizado (quedaba en ES en modo EN) |
| 2026-07-08 | fix(ux): chips sin relleno solido — estado y categoria seleccionada translucidos |
| 2026-07-09 | feat(seguridad): T14-C0 — access token 60 min + refresh rotatorio revocable, SecureStorage, sin cleartext |
| 2026-07-09 | fix(seguridad): migracion one-shot de credenciales legadas a refresh token |
| 2026-07-09 | docs: T14-C0 hecho en el indice de tareas |
| 2026-07-10 | feat(privacidad): T14-C1 — política publicada, borrado de cuenta y consentimiento de foto |
| 2026-07-10 | docs: T14-C1 hecho en el indice de tareas |
| 2026-07-10 | feat(diorama): fondo del cuarto por estilo equipado + guia de arte |
| 2026-07-11 | feat(diorama): 13 objetos nuevos de catalogo + 6 fondos de habitacion (ronda4) |
| 2026-07-11 | feat(tienda): quitar Estilo Costa (nunca llegó su arte; equip valida catálogo y el fondo cae al base) |
| 2026-07-11 | feat(cuarto): panel Guardados en modo edición + arreglos UX del editor |
| 2026-07-11 | feat(cuarto): sandbox inmersivo de edición con pad tipo Gameboy |
| 2026-07-11 | fix(cuarto): sandbox respeta el tema (AppBgBrush, no color fijo) + i18n EN de sus textos |
| 2026-07-11 | fix(robustez): feedback de error en onboarding + topes de input server-side |
| 2026-07-12 | chore(pulido): purga de refresh tokens muertos + timeout de Gemini + fuera el ajuste no-op de modelo IA |
