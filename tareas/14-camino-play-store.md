# T14 — Camino a Play Store: deuda no técnica

**Estado:** pendiente · **Esfuerzo global:** M-L (mayormente trámites y decisiones) · **Depende de:** decisión del dueño sobre publicar

## El quiebre (por qué)

Si la meta es publicar, hay obligaciones que no son código y tardan más de lo que parecen: Google exige declarar qué datos se recogen y a dónde van, y la app hoy manda **fotos de los usuarios a Gemini** y las guarda 30 días en la BD sin política de privacidad que lo diga. Además los pagos premium están (correctamente) deshabilitados en producción a la espera de Play Billing, y se mantienen targets (iOS, Windows, Tizen, MacCatalyst) que nadie prueba y que cobran un impuesto mental en cada cambio.

## Evidencia en el código

- `src/PetProductivity.Server/Controllers/FocusController.cs:127-160` — la foto viaja al server y a Gemini Vision; se persiste en `FocusProofs` (bytea).
- `FocusCleanupHostedService.cs:44` — retención 30 días (bien; hay que *declararlo*, no solo hacerlo).
- `src/PetProductivity.Server/Controllers/ShopController.cs:144-159` — `purchase-premium` con stub solo-Development; producción responde "pagos aún no habilitados" (TODO Google Play Billing).
- `PetProductivity.Client.csproj` — TFMs android + windows (y carpetas Platforms/ de iOS/MacCatalyst/Tizen).
- Login con Google ya mediado por servidor y package `yenllo.org.PetProductivity` en Firebase — parte del camino ya está andado.

## Componentes y opciones

### C0. 🔴 Endurecimiento de auth — BLOQUEANTE de lanzamiento (decisión aplazada a propósito)
> El dueño decidió **no** tocar la vida/rotación del token durante el desarrollo (fricción de re-login sin refresh token). Al publicar, la superficie de ataque cambia (usuarios reales, red hostil, teléfonos ajenos) y esto pasa a ser **obligatorio**. Ver detalle y evidencia en [`22-seguridad-INFORME.md`](22-seguridad-INFORME.md).

Checklist antes del primer release:
- **Secreto JWT rotado** (C1 del informe): el `Jwt__Key` de Render debe ser un secreto fuerte (≥32 bytes) y **distinto** del usado en desarrollo. El fail-fast ya rechaza claves cortas; falta hacer la rotación final. *(Paso a paso de Render documentado; es 2 min.)*
- **Token corto + refresh + revocación** (C1 del informe): hoy el token dura 30 días, sin refresh ni revocación (`TokenService.cs:30`), y el cliente **cae a invitado** cuando vence si el login fue por Google (`AuthService.cs:54-61`). Antes de publicar: bajar la vida del access token, añadir refresh token en `SecureStorage`, y una revocación (`SecurityStamp`/versión en BD) para invalidar tokens al hacer logout/cambiar contraseña. Se hace junto a A1.
- **A1 — credenciales en el dispositivo**: mover el token a `SecureStorage`, **dejar de guardar la contraseña en claro** (`SavedPassword`), y `android:allowBackup="false"`. Necesita verificar el login en teléfono real.
- **M4 — cleartext off en release**: `usesCleartextTraffic="false"` (o `network_security_config` que solo permita `10.0.2.2`/localhost de dev).
- **M5 — OAuth con PKCE / App Links verificados** en vez del esquema propio `petproductivity://` interceptable.
- **B10 — Play Billing real** antes de habilitar cualquier compra premium (ver C2).

**Esfuerzo:** M-L (el grueso es el rework de auth del cliente, A1). **Es la razón por la que este bloque no se hizo durante el desarrollo: exige teléfono y toca el flujo de sesión completo.**

### C1. Privacidad y Data Safety (bloqueante para publicar)
- **a — Política de privacidad publicada** (página estática; Render puede servirla o GitHub Pages): qué se recoge (email, tareas descritas, fotos de comprobante, token FCM), con quién se comparte (Google Gemini para el juicio/verificación, Firebase para push), retención (fotos 30 días, veredicto permanente), derecho de borrado.
- **b — Consentimiento en la app** para la foto de comprobante: el flujo ya es opt-in (bonus), añadir la primera vez un aviso "esta foto se analiza con IA y se guarda 30 días".
- **c — Borrado de cuenta** (Google lo exige para apps con cuentas desde 2024): endpoint `DELETE /api/users/me` + página web de solicitud. Hoy no existe — es probablemente el único desarrollo *grande* de esta tarea.
- **Esfuerzo:** a/b S, c M.

### C2. Play Billing para lo premium
- **a — Integrar Google Play Billing** (requiere cuenta de desarrollador del dueño, productos configurados en Play Console, validación server-side del purchase token — el endpoint ya está diseñado para recibirla).
- **b — Lanzar sin premium** (ocultar los ítems Premium del catálogo en producción) y añadir billing después. El catálogo ya soporta esto casi gratis (filtrar por `Currency != "Premium"` en `GetCatalog` o quitar los info.json).
- **Recomendada:** **b** para el primer release — billing es el trámite más lento (revisiones de Play) y lo premium hoy no tiene catálogo que lo justifique.
- **Esfuerzo:** a L, b S.

### C3. Recortar targets fantasma
- **a — Android-only explícito:** quitar `net10.0-windows` del csproj (o moverlo a un perfil local si sirve para depurar UI rápido) y borrar Platforms/iOS/MacCatalyst/Tizen.
- **b — Dejarlo como está** y aceptar el impuesto (cada dependencia/API nueva se piensa ×4 plataformas).
- **Recomendada:** **a**, con la matización de que Windows puede quedarse SI de verdad se usa para iterar UI sin emulador (decisión del dueño); Tizen/iOS/MacCatalyst fuera sin duelo — restaurarlos es un git revert.
- **Esfuerzo:** S.

### C4. Checklist Play Console (trámites, sin código)
Cuenta de desarrollador ($25 una vez), ficha de la tienda (screenshots, descripción), clasificación de contenido, Data Safety form (con C1 hecho es transcribir), firma de app (Play App Signing), y track interno → cerrado → producción. El push MIUI ya está documentado como limitación conocida; no bloquea.
- **Esfuerzo:** M en horas-trámite repartidas (las revisiones de Google tardan días).

## Recomendación

Orden: **C3 (recortar) → C1 (privacidad + borrado de cuenta) → C4 (trámites con track interno) → C2-b (lanzar sin premium)**. Publicar primero en track interno/cerrado con usuarios reales conocidos (la pareja/familia que ya prueba) — eso valida C1/C4 sin exposición. C2-a (billing real) solo cuando exista catálogo premium que lo amerite.

## Criterios de éxito / verificación

1. La app declara en su primer uso de comprobante a dónde va la foto, y la política publicada coincide con lo que el código hace (retenciones incluidas).
2. `DELETE /api/users/me` borra usuario, mascota, tareas, fotos y membresías (y deja los grupos consistentes) — con test.
3. El catálogo de producción no muestra ítems Premium.
4. `dotnet build` del cliente solo compila los TFMs decididos.
5. App instalable desde track interno de Play en el teléfono real.

## Dependencias

- **T9** (working tree limpio) antes de tocar csproj.
- C1-c (borrado) conviene diseñarlo junto a la capa social (¿qué pasa con la mascota compartida si un miembro se borra? — reutilizar la lógica de salida de grupo existente).
