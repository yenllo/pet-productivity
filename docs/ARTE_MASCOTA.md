# Arte de la mascota — spec completa + el sentido de la app

> Documento para quien dibuje la mascota (y su estatua de legado). Todos los números están
> **verificados contra el código real** a 2026-07-15: `Controls/RoomDiorama.cs`,
> `Services/PetVisuals.cs`, `Shared/Models/PetEvolution.cs`. Complementa a
> [`GUIA_ARTE.md`](GUIA_ARTE.md) (pieza del cuarto + muebles + estilos) y
> [`ROOM_PIECE_SPEC.md`](ROOM_PIECE_SPEC.md) (geometría del fondo). Aquí manda **la mascota**.
>
> Estado hoy: las 11 imágenes de mascota **ya existen** como placeholder
> (`Resources/Images/pet_*.png`). Este doc es la spec para reemplazarlas por arte definitivo
> sin que se desalinee ni pierda nitidez.

---

## 0. Resumen en 30 segundos

- La mascota se dibuja **dentro** del lienzo isométrico de la sala, **apoyada por su borde
  inferior-centro** sobre la baldosa central (3,3) = punto de diseño **(512, 555)**.
- **Lienzo del sprite: cuadrado, transparente, pixel-art nítido.** Hoy 112×112 (huevo/cristal
  128×128); **recomendado redibujar a 330×330** para que no se reescale con "pixeles" desiguales.
- Se dibuja a **330 unidades de ancho** (~2,2 baldosas) → ~32 % del ancho de la sala.
- **La sombra la pinta el código.** El sprite NO lleva sombra ni piso.
- **La animación la hace el código** (respiración, saltito, día/noche). Entregas **1 frame estático**.
- Set mínimo = **11 imágenes**: huevo + cristal (compartidos) + 3 especies × 3 etapas.

---

## 1. El sentido de la app (por qué la mascota importa)

Antes de dibujar, entender qué representa la criatura — el arte tiene que **hacer sentir esto**:

PetProductivity gamifica la productividad real. El usuario describe en lenguaje natural algo que
hizo ("estudié 2 h de cálculo"); una IA lo juzga y entrega recompensa. La mascota **es el avatar
emocional del esfuerzo del usuario**: no es una mascota cualquiera, es *el reflejo vivo de si la
persona está cumpliendo consigo misma*.

Cuatro ideas que el arte debe transmitir:

1. **Crece contigo.** La mascota evoluciona por etapas (Huevo → Cría → Adulto → Maestro) a medida
   que acumulas esfuerzo real. Cada etapa debe leerse como un **logro**: más grande, más definida,
   más "lograda". Llegar a Maestro toma ~2–4 semanas de constancia — es un evento, debe verse épico.
2. **Vive y siente.** La mascota tiene hambre y salud que decaen si la abandonas; su humor cambia
   (contenta / con hambre / débil). No es estática: respira, parpadea, festeja cuando cumples.
3. **Puede morir y renacer (Fénix).** Si la descuidas se **cristaliza** (queda como piedra, "muerta")
   y solo revive con esfuerzo real. El estado cristal debe dar pena y urgencia — pero **esperanzador**,
   no macabro: es reversible, es una segunda oportunidad, no un castigo cruel.
4. **La habitación es tu diario.** El cuarto acogedor se llena de muebles (comprados con el oro que
   ganas) y, al retirar a un Maestro, de **estatuas de tus mascotas pasadas** (legado / generaciones).
   Cada estatua es un capítulo de tu vida productiva. Todo el conjunto debe sentirse **cálido,
   personal, vivo** — un compañerito acogedor, nunca competitivo ni agresivo.

**Tono visual:** cozy, cálido, redondeado, adorable (estilo "cozy pixel-art", coherente con los
muebles del pack Bongseng que ya viven en la sala). Invita, no intimida. El oro es solo cosmético:
nada de estética de "pagar para ganar" ni de competencia.

---

## 2. El escenario: la habitación isométrica

La mascota se pinta encima del fondo `room_bg.png`. Para que "pise" bien el suelo hay que respetar
su geometría (detalle completo en `ROOM_PIECE_SPEC.md`; aquí lo esencial):

- **Lienzo de diseño: 1024 × 1024 px.** Todo se posiciona en este sistema y el código lo escala al
  tamaño real de pantalla (fit-contain centrado).
- **Proyección isométrica 2:1**, cuarto de **6×6 baldosas**. Baldosa = **150 × 75 px** (`T_W=150`,
  `T_H=75`).
- Fórmula exacta de pantalla de un nodo de grilla `(i,j)` en coords de diseño:

  ```
  x = 512 + (i − j) · 75
  y = 330 + (i + j) · 37.5
  ```

  (`O_X=512`, `O_Y=330`, medias baldosas 75 y 37.5.)

- Vértices del piso (rombo):

  | Nodo | Coord diseño | Qué es |
  |---|---|---|
  | (0,0) | **(512, 330)** | esquina trasera (fondo) |
  | (6,0) | (962, 555) | esquina derecha |
  | (0,6) | (62, 555) | esquina izquierda |
  | (6,6) | (512, 780) | esquina frontal |
  | **(3,3)** | **(512, 555)** | **centro — aquí se para la mascota** |

- Paredes de ~300 px de alto (van de y≈30 a y≈330), frente abierto. **Luz desde arriba-izquierda**
  (coherente con los muebles Bongseng).

---

## 3. La mascota dentro de la habitación  ← el corazón de este doc

### 3.1 Matriz de sprites (especie × etapa) — la lista mínima

El código elige el archivo así (`PetVisuals.SpriteFor`):

```
Huevo (cualquier especie) ........ pet_egg.png
Cristalizada (cualquier especie) . pet_crystal.png
Viva, post-huevo ................. pet_<especie>_<etapa>.png
```

- **Especies** (`Species`, minúsculas): `sprout` (planta), `ember` (fuego), `aqua` (agua).
- **Etapas** (sufijo): `baby` (Cría), `adult` (Adulto), `master` (Maestro). *(El Huevo no usa
  especie; usa `pet_egg.png`.)*

**Set mínimo = 11 archivos:**

| Archivo | Cuándo se ve |
|---|---|
| `pet_egg.png` | recién nacida (Huevo, 0–50 XP), y ceremonia de nacimiento |
| `pet_crystal.png` | mascota muerta/cristalizada (Fénix), cualquier especie/etapa |
| `pet_sprout_baby.png` · `pet_sprout_adult.png` · `pet_sprout_master.png` | especie planta |
| `pet_ember_baby.png` · `pet_ember_adult.png` · `pet_ember_master.png` | especie fuego |
| `pet_aqua_baby.png` · `pet_aqua_adult.png` · `pet_aqua_master.png` | especie agua |

> Nombres **exactos, en minúsculas** — el código los arma por string. Un typo = sprite en blanco.
> Destino: `src/PetProductivity.Client/Resources/Images/`. Son assets empaquetados → **recompilar el
> APK** al cambiarlos.

### 3.2 Specs de píxeles

| Propiedad | Valor | Por qué |
|---|---|---|
| **Formato** | PNG, **RGBA con transparencia real** | sin halos blancos en el borde |
| **Lienzo** | **cuadrado.** Hoy 112×112 (huevo/cristal 128×128). **Recomendado: 330×330** para todos | ver nota de nitidez abajo |
| **Muestreo en pantalla** | **Nearest-neighbor** (lo fija el código) | mantiene el pixel-art nítido; **no** cuentes con suavizado/anti-alias del motor |
| **Ancla** | **borde inferior-centro** | el código apoya ese punto en la baldosa (3,3) |
| **Sombra** | **NO incluir** | el código pinta una elipse de sombra de contacto bajo la mascota |
| **Piso/base** | **NO incluir** | la criatura "flota" sobre su ancla; el suelo lo pone la sala |

**El ancla es lo más importante.** El código toma el **borde inferior-centro del lienzo** y lo clava
en el punto (512, 555). Por lo tanto:

- Los **pies / punto de contacto** de la criatura deben quedar **pegados al borde inferior del
  lienzo, centrados horizontalmente**. Nada de margen transparente abajo (si dejas 20 px de aire
  abajo, la mascota "levita" 20 px sobre el suelo — fue un bug real ya corregido; no lo reintroduzcas).
- Horizontalmente **centrada** en el lienzo (su eje de simetría en la columna del medio).
- Arriba puede sobrar aire: la criatura crece hacia arriba (hacia la pared), eso está bien.

**Tamaño en pantalla y nitidez.** El código dibuja la mascota a **330 unidades de diseño de ancho**
(constante `PET_DESIGN = 330`), preservando la proporción del lienzo. Como el lienzo es cuadrado,
ocupa 330×330 de diseño: en la sala va de y≈225 (cabeza, sube hasta la pared) a y=555 (pies), y de
x≈347 a x≈677 — **~2,2 baldosas de ancho, centrada**.
→ Un sprite nativo de 112 px reescalado a ~330 en pantalla se agranda ~3× con nearest **no entero**,
lo que hace que unos "pixeles" salgan de 3 px y otros de 4 (se ve irregular). **Por eso conviene
dibujar nativo a 330×330** (o al menos ≥256): así el reescalado queda cerca de 1× y el pixel-art se
ve parejo. Si prefieres el look chunky, trabaja el personaje en una grilla lógica gruesa (p. ej.
48–64 "art-pixels") y escálala **por múltiplo entero** hasta 330.

### 3.3 Ángulo, luz, sombra, silueta

- **Perspectiva:** iso 2:1 coherente con la sala y los muebles (vista de 3/4 desde arriba, ~30°).
  La mascota mira hacia el frente/abajo (hacia el usuario), de pie sobre la baldosa.
- **Luz:** desde **arriba-izquierda** (igual que los muebles Bongseng). Sombra propia de la criatura
  hacia abajo-derecha, sutil, integrada en el sprite (NO la sombra de contacto en el piso, esa la
  pone el código).
- **Silueta legible:** un mueble que esté delante en la grilla **puede tapar parcialmente** a la
  mascota (el código la ordena por profundidad en la baldosa 3,3). Diseña una silueta que se
  reconozca aunque le tapen los pies. Contorno/outline oscuro suave ayuda a que resalte sobre
  paredes y muebles de cualquier color.
- **Contraste con el fondo:** la sala por defecto es morada-cozy con luz cálida; los estilos pueden
  ser verdes (bosque), morados (galaxia), etc. La mascota debe destacar sobre fondos variados →
  colores saturados + outline.

### 3.4 El arco de evolución — qué comunica cada etapa

Debe **leerse de un vistazo** que la mascota progresó. Umbrales reales de XP
(`PetEvolution.cs`; meta del dueño Huevo→Maestro ≈ 2–4 semanas de constancia):

| Etapa | XP | Qué debe transmitir | Idea visual |
|---|---|---|---|
| **Huevo** | 0–50 | potencial, ternura, misterio | huevo con patrón/color neutro; NO revela la especie todavía |
| **Cría** (`baby`) | 50–600 | bebé recién eclosionado, torpe, adorable | pequeña, cabezona, ojos grandes; apenas insinúa el elemento |
| **Adulto** (`adult`) | 600–2500 | crecida, capaz, con personalidad | más grande y definida; el elemento (hoja/llama/agua) ya es claro |
| **Maestro** (`master`) | 2500+ | logro máximo, aspiracional, épico | la más imponente: aura, corona, alas, elemento en su esplendor — se ganó semanas de esfuerzo |

La transición **debe verse como crecimiento continuo de la MISMA criatura** (misma alma, mismo
personaje), no tres animales distintos. Mantén rasgos de identidad (color base, forma de ojos,
marca característica) a través de las 3 etapas de cada especie.

> Nota técnica: la evolución hoy salta de sprite (Cría→Adulto→Maestro) con una ceremonia ✨ que
> pinta el código. También existe un `MasterVisualChangeThreshold` para un "sub-cambio" dentro de
> Maestro (aura/corona por stat dominante) que **hoy no tiene arte** — es opcional, no del set mínimo.

### 3.5 Las 3 especies (personalidad visual)

Cosméticas, asignadas al azar al nacer (el usuario no elige). Deben ser **claramente distintas** de
un vistazo y con personalidad propia, pero del mismo "mundo" (misma familia de diseño, "starters"
tipo Moko):

| Especie | Clave | Elemento | Paleta sugerida | Vibra |
|---|---|---|---|---|
| **Sprout** | `sprout` | planta / naturaleza | verdes + brotes/hojas | tierna, terrosa, tranquila |
| **Ember** | `ember` | fuego / energía | rojos-naranjas + brasa | enérgica, chispeante, decidida |
| **Aqua** | `aqua` | agua / calma | azules + gotas/aletas | serena, fluida, curiosa |

### 3.6 Estados especiales

- **Huevo (`pet_egg.png`, 128×128 hoy):** neutro, no revela especie. Se usa en el Dashboard recién
  nacida y en la **ceremonia de nacimiento** (grieta → flash → revela la criatura → nombrar). Un
  huevo con un patrón bonito y un brillo de "algo por venir".
- **Cristal (`pet_crystal.png`, 128×128 hoy):** la mascota **muerta/cristalizada** (mecánica Fénix).
  Una versión pétrea/congelada, azulada, apagada — **da pena pero es esperanzadora** (se puede
  revivir con esfuerzo real). El código además tira una escarcha azul sobre toda la sala y un
  contador de "grietas 💠 /3". No la hagas macabra: es una crisálida, una pausa, no una tumba.

### 3.7 Animación = trabajo del código (entrega 1 frame)

**No entregues spritesheets ni frames.** El motor anima el sprite estático:

- **Respiración:** escala vertical suave ±3 % en ciclo de ~3,2 s.
- **Saltito de celebración:** al ganar XP, brinca hasta ~18 px y la sombra encoge (venta el contacto).
- **Día/noche:** el código tiñe toda la escena según la hora local (no toques color por hora).
- **Opacidad:** el código puede atenuarla (p. ej. invitado). No la bakees.

Diseña un frame que se vea bien **quieto** y que tolere ese estiramiento sutil sin romperse (evita
detalles que se deformen feo al escalar en Y).

---

## 4. La estatua del legado (T4-A generaciones)

Cuando el usuario **retira a un Maestro**, este pasa a ser un **ancestro** y (a futuro) una
**estatua colocable en el diorama**. La estatua **NO es un sprite de mascota**: es un **mueble**, así
que sigue enteramente la spec de objetos de [`GUIA_ARTE.md`](GUIA_ARTE.md) §3:

- Va en `Catalog/` como objeto con su `info.json`, o como sprite `obj_statue_*` colocable.
- **Ancla = borde inferior-centro** (igual que todo mueble), footprint **1×1** baldosa, sin sombra
  propia (la pone el código), iso 2:1, luz arriba-izquierda, PNG transparente.
- **Dos vistas** `obj_statue_l.png` / `obj_statue_r.png` (o una sin sufijo si es simétrica).
- Idea: la silueta del Maestro tallada en piedra/bronce sobre un pedestal, con placa. Idealmente
  **una por especie** (`obj_statue_sprout_l`…) para que se reconozca a quién honra; mínimo aceptable:
  **una genérica de trofeo**.
- El nombre controla la escala (`ModFor`): "statue" cae en el default **1,15×**; si se ve chica se
  sube una línea en `RoomDiorama.ModFor` — avísame y la calibro.

Hoy, sin ese arte, el ancestro se muestra como **lista de texto en el Perfil** ("🏆 Gen 1: Moko —
Maestro Aqua · 2.850 XP"). La estatua es el upgrade visual pendiente.

---

## 5. Humor (opcional — hoy es burbuja emoji)

El humor de la mascota (contenta / con hambre / débil) **hoy NO usa sprites**: el código dibuja una
burbuja con emoji (`✨` / `🥺` / `💔`) encima de la criatura (`PetVisuals.MoodEmoji`). Así que
**sprites de humor son polish opcional**, no parte del set mínimo. Si algún día se quieren variantes
de cara por humor, sería un set nuevo (no rompas el mínimo por esto ahora).

---

## 6. Lista mínima de entregables + checklist

**Mínimo para que la mascota se vea perfecta en la sala (11 imágenes):**

- [ ] `pet_egg.png` · `pet_crystal.png`
- [ ] `pet_sprout_baby/adult/master.png`
- [ ] `pet_ember_baby/adult/master.png`
- [ ] `pet_aqua_baby/adult/master.png`

**Deseable después:** estatua(s) de legado (`obj_statue_*`), fondos de estilo faltantes, sub-cambio
visual de Maestro por stat.

**Checklist por sprite de mascota:**

- [ ] PNG RGBA, transparente, **sin halos** blancos en el borde.
- [ ] Lienzo cuadrado; **recomendado 330×330** (mínimo 256) para nitidez con nearest.
- [ ] **Pies pegados al borde inferior, centrados horizontalmente.** Cero aire abajo.
- [ ] **Sin sombra ni piso** dibujados (los pone el código).
- [ ] Iso 2:1, luz arriba-izquierda, mira al frente.
- [ ] Silueta legible aunque un mueble le tape los pies (outline oscuro suave).
- [ ] Un frame estático (nada de animación bakeada).
- [ ] Nombre exacto en minúsculas (`pet_ember_master.png`, etc.).
- [ ] Las 3 etapas de una especie = **la misma criatura creciendo**, no 3 animales distintos.
- [ ] Maestro se ve claramente épico frente a Cría.

---

## 7. Pipeline: dónde van y qué recompilar

| Cambio | Dónde | Requiere |
|---|---|---|
| Sprite de mascota (`pet_*.png`) | `src/PetProductivity.Client/Resources/Images/` | **recompilar APK** |
| Estatua / objeto (`obj_*` + `info.json`) | `Catalog/<Categoría>/<Nombre>/` | reiniciar/redeploy server **+ recompilar APK** |
| Fondo de sala/estilo (`room_bg*.png`) | `src/PetProductivity.Client/Resources/Raw/` | **recompilar APK** |

Flujo de iteración: me pasas los PNG (o los sueltas en las carpetas), **yo recompilo e instalo en el
emulador** y te muestro cómo queda **antes** de commitear. Nada de arte se sube al repo sin verlo en
la sala real (además, ojo con las licencias: el arte del pack Bongseng no es redistribuible; el arte
propio sí — ver la allowlist de PNGs del repo).

---

## 8. Referencia rápida de números (para tener a mano)

```
LIENZO DE LA SALA ....... 1024 × 1024 (diseño; el código lo escala a pantalla)
BALDOSA ................. 150 × 75  (iso 2:1)
GRILLA .................. 6 × 6 baldosas
ORIGEN (nodo 0,0) ....... (512, 330)
FÓRMULA ISO ............. x = 512 + (i−j)·75  ;  y = 330 + (i+j)·37.5
BALDOSA DE LA MASCOTA ... (3,3) → punto de apoyo (512, 555)

MASCOTA
  lienzo sprite ......... cuadrado; hoy 112×112 (huevo/cristal 128×128); RECOMENDADO 330×330
  ancho en pantalla ..... 330 unidades de diseño (~2,2 baldosas, ~32% del ancho)
  ancla ................. borde inferior-centro (pies al borde de abajo, centrado)
  muestreo .............. nearest-neighbor (pixel-art nítido)
  sombra ................ la pinta el código (no incluir)
  animación ............. la hace el código (entregar 1 frame)

ETAPAS (XP) ............. Huevo 0–50 · Cría 50–600 · Adulto 600–2500 · Maestro 2500+
ESPECIES ............... sprout (planta) · ember (fuego) · aqua (agua)
ESTADOS ................ egg (huevo) · crystal (cristalizada/Fénix)
SET MÍNIMO ............. 11 imágenes (2 compartidas + 3 especies × 3 etapas)
```
