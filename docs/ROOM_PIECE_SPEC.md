# Especificación de la PIEZA del cuarto (fondo del diorama)

El cuarto donde vive la mascota se dibuja como **UNA sola imagen de fondo** (piso + paredes,
sin muebles ni mascota). Encima, el código compone los **muebles** (movibles, tipo Sims) y la
**mascota**. Por eso esta pieza debe respetar un sistema de coordenadas fijo: si lo cumples, los
muebles y la mascota caen exactamente en su lugar sin tocar código.

> Archivo destino: `src/PetProductivity.Client/Resources/Raw/room_bg.png`
> (reemplaza el `room_bg.png` inicial que ya generé con estas mismas specs).

## Formato
- **PNG con transparencia** (RGBA). Todo lo que no sea cuarto = transparente.
- **Lienzo: 1024 × 1024 px** (cuadrado, obligatorio — el código asume 1024).
- Sin sombras proyectadas fuera del cuarto (las sombras de contacto las pone el código).
- Estilo libre (pixel, vector, lo que quieras) pero **paleta neutra/sobria**: los muebles van
  encima y deben resaltar. Evita texturas muy ruidosas en el piso.

## Proyección isométrica (2:1)
- Un "tile" lógico mide **150 px de ancho × 75 px de alto** en pantalla (relación 2:1).
- El cuarto es de **6 × 6 tiles** (cuadrado → se ve como rombo/diamante, NO triángulo).
- **Vértice trasero del piso** (esquina del fondo, tile 0,0) en: **X=512, Y=330**.
- Fórmula de un punto del piso para tile (i, j), con i,j ∈ [0..6]:
  ```
  X = 512 + (i - j) * 75
  Y = 330 + (i + j) * 37.5
  ```

## Geometría exacta (coordenadas en el lienzo 1024×1024)
**Piso** (rombo), 4 vértices:
| Punto | (X, Y) | tile |
|---|---|---|
| Trasero (arriba) | (512, 330) | (0,0) |
| Derecho | (962, 555) | (6,0) |
| Frontal (abajo) | (512, 780) | (6,6) |
| Izquierdo | (62, 555) | (0,6) |

**Paredes** (2 paredes traseras, suben **300 px** desde los bordes traseros del piso):
- Pared **derecha**: cuadrilátero (512,330) → (962,555) → (962,255) → (512,30).
- Pared **izquierda**: cuadrilátero (512,330) → (62,555) → (62,255) → (512,30).
- **Esquina superior trasera** (donde se juntan ambas paredes arriba): (512, 30).
- **Frente ABIERTO**: no hay paredes frontales (si no, taparían a la mascota/muebles).
- Sugerencia: pared izquierda un pelín más clara que la derecha (luz desde la izquierda).
- Zócalo opcional de ~18 px en la base de cada pared.

## Zonas reservadas (no dibujar cosas "sueltas" ahí; el código las usa)
- **Centro del piso** = tile (3,3) ≈ (512, 555): ahí se para la **mascota**. Déjalo despejado.
- Los **muebles** se anclan por su **borde inferior-centro** sobre el punto del tile que ocupan
  (el código los coloca; tú no los dibujas en la pieza).
- Una ventana/cuadro PINTADOS en la pared están OK (decoración fija), pero recuerda que también
  habrá muebles de pared comprables encima.

## Cómo lo encuadra el código (para que lo tengas en mente)
- La imagen se escala con **fit-contain** y se **centra** en la tarjeta del dashboard
  (~1010×730 px en pantalla). El centro geométrico del lienzo (512,512) cae en el centro de la
  tarjeta. Por eso el piso y las paredes deben estar **centrados** en el lienzo (ya lo están con
  estas coordenadas). Márgenes transparentes arriba/abajo/lados son normales.
- Mantén las paredes dentro de Y ≥ 30 y el piso dentro de Y ≤ 790 para que no se recorten.

## Variantes / estilos (tienda)
- Cada **estilo de cuarto** comprable = un `room_bg_<estilo>.png` con **las mismas coordenadas**.
  Ej: `room_bg_default.png`, `room_bg_bosque.png`, `room_bg_costa.png`, `room_bg_galaxia.png`.
- Solo cambian colores/texturas de piso y pared; la geometría es idéntica (así los muebles encajan
  en todos los estilos sin reconfigurar).

## Checklist rápido
- [ ] 1024×1024 PNG transparente.
- [ ] Piso rombo con los 4 vértices exactos de la tabla.
- [ ] 2 paredes traseras de 300 px, frente abierto.
- [ ] Centro (512,555) despejado para la mascota.
- [ ] Paleta neutra; sin sombras externas.
