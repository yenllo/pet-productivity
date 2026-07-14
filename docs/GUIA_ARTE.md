# Guía de arte — qué dibujar y dónde soltarlo

Para dibujar la pieza del cuarto, estilos y objetos nuevos **sin tocar código**. Todo lo de aquí
está verificado contra el código real (`RoomDiorama.cs`, `RoomSprites.cs`, `CatalogLoader.cs`,
`GameDataService.cs`) a 2026-07-10.

---

## 1. La PIEZA del cuarto (`room_bg.png`)

Spec completa con coordenadas exactas en [`ROOM_PIECE_SPEC.md`](ROOM_PIECE_SPEC.md). Resumen:

- **1024 × 1024 px, PNG con transparencia.** Solo piso + paredes, SIN muebles ni mascota.
- Iso 2:1, cuarto de 6×6 tiles (tile = 150×75 px), vértice trasero del piso en **(512, 330)**,
  frontal en (512, 780), laterales en (62, 555) y (962, 555). Paredes de 300 px de alto,
  frente abierto.
- Centro (512, 555) despejado — ahí se para la mascota.
- Destino: `src/PetProductivity.Client/Resources/Raw/room_bg.png` (reemplaza el actual).
- **Requiere recompilar el APK** (es un asset empaquetado).

## 2. Fondos por ESTILO (`room_bg_<styleKey>.png`)

Cada estilo comprable de la tienda puede tener su propio fondo: **misma geometría exacta** que la
pieza base, solo cambian colores/texturas/decoración pintada de piso y paredes.

- Nombre de archivo = `room_bg_` + el `styleKey` del info.json del estilo. Los estilos que ya
  existen en la tienda:
  | Estilo | styleKey | Archivo a dibujar |
  |---|---|---|
  | Estilo Bosque | `forest` | `room_bg_forest.png` |
  | Estilo Galaxia | `galaxy` | `room_bg_galaxy.png` |
  | Estilo Baño | `bathroom` | `room_bg_bathroom.png` |
  | Estilo Cocina | `kitchen` | `room_bg_kitchen.png` |
  | Estilo Loft | `loft` | `room_bg_loft.png` |
- Destino: la misma carpeta `Resources/Raw/`. Si el archivo del estilo no existe, el código usa
  `room_bg.png` (fallback automático — puedes entregar los estilos de a uno).
- Un estilo NUEVO = carpeta en `Catalog/Estilos/` con `info.json` (categoría `Estilos`,
  `styleKey` propio en minúsculas sin espacios, `icon` con un emoji, sin PNGs) + su
  `room_bg_<key>.png` en Raw.

## 3. Objetos nuevos de la tienda (`Catalog/`)

El catálogo ES la carpeta `Catalog/` en la raíz del repo. Un objeto = una carpeta:

```
Catalog/<Categoría>/<Nombre_Del_Objeto>/
├── info.json
├── obj_<id>_l.png      ← vista mirando a la izquierda (contra pared derecha)
└── obj_<id>_r.png      ← vista mirando a la derecha (contra pared izquierda)
```

Categorías existentes (el nombre de la carpeta ES la categoría en la tienda):
`Muebles · Decoración · Estructural · Estilos · Cosmético · Consumibles · Eventos · Premium · Vida`

### info.json (campos reales de `CatalogLoader`)

```json
{
  "name": "Mesa de Dibujo",
  "price": 320,
  "description": "",
  "category": "Muebles",
  "rarity": "Common",
  "currency": "Gold",
  "spriteId": "obj_drawing_table_l",
  "styleKey": "",
  "icon": "",
  "availableFrom": null,
  "availableTo": null,
  "source": "",
  "sprites": ["obj_drawing_table_l.png", "obj_drawing_table_r.png"]
}
```

- `spriteId` = el sprite que se muestra en la tienda y se coloca al comprar (con vista incluida).
- `currency`: `Gold` o `Premium`. `rarity`: `Common/Rare/Epic/Legendary`.
- `availableFrom/To` (fechas ISO) = eventos con contador.
- Objetos simétricos (plantas, alfombras, cajas): **una sola vista sin sufijo** (`obj_cactus.png`).

### Specs del sprite (PNG)

- **Fondo transparente**, sin sombra de contacto pintada (el código dibuja la sombra en el piso).
- **Ancla = borde inferior-centro**: el código apoya el sprite por el centro de su borde inferior
  sobre el tile. Deja el "pie" del objeto tocando el borde inferior del lienzo, centrado, sin
  márgenes transparentes abajo.
- **Tamaño fuente**: libre (el pack Bongseng usa ~60–160 px de ancho); el código lo escala al
  ancho del tile preservando proporción. Dibuja a 2× si quieres más nitidez — no hay problema.
- **Perspectiva**: iso 2:1 coherente con el pack Bongseng (mira los PNG existentes en `Catalog/`
  como referencia de ángulo y luz — luz desde arriba-izquierda).

### ⚠️ El nombre del sprite controla la ESCALA y el FOOTPRINT (keywords en inglés)

El código infiere cuánto ocupa el objeto por palabras clave **en inglés** dentro del nombre:

- **Escala en pantalla** (`ModFor`/`KeywordMod` en RoomDiorama.cs): `bed` 1.6× · `closet/cupboard/
  fridge/wardrobe/drawer` 1.5× · `carpet/rug` 1.5× · `tv/shelf/bookshelf/sliding/wall/door/window`
  1.4× · `table/desk/sofa/futon/oven/kitchen` 1.35× · `cup/coffee/book/cushion/teddy/box/cookie/
  knife` 0.75× (objetos chicos) · **cualquier otro nombre → 1.15×** (default razonable).
- **Footprint en la grilla** (`FootprintFor` en GameDataService.cs): `bed` (no `bedside`) = 2×2
  tiles; **todo lo demás = 1×1**.
- Regla práctica: nombra `obj_<qué_es_en_inglés>_<detalle>` (ej. `obj_drawing_table_l`) y la
  escala sale sola. Si un objeto nuevo se ve muy grande/chico, se ajusta con una línea en
  `ModFor` — avísame y lo calibro.

### Pipeline: qué requiere qué

| Cambio | Server (tienda) | Cliente (APK) |
|---|---|---|
| Editar precio/nombre/fechas en `info.json` | reiniciar server (o `git push` → Render) | nada |
| Objeto nuevo (info.json + PNGs) | reiniciar/redeploy | **recompilar APK** (los PNG de `Catalog/**` se empaquetan como MauiAsset) |
| `room_bg*.png` nuevo/cambiado | nada | **recompilar APK** |

O sea: para iterar arte, me pasas los PNG (o los sueltas en las carpetas), y yo recompilo e
instalo en el emulador para que veas cómo queda antes de commitear.

## 4. Checklist al entregar un lote de arte

- [ ] PNGs con transparencia real (RGBA), sin halos blancos en los bordes.
- [ ] Objetos: pie tocando el borde inferior del lienzo, centrado.
- [ ] Dos vistas `_l`/`_r` para muebles direccionales; una sin sufijo para simétricos.
- [ ] Nombres `obj_*` en minúsculas con keyword inglesa del tipo de objeto.
- [ ] Pieza/estilos: geometría exacta del spec (los 4 vértices del rombo clavados).
- [ ] `info.json` por objeto nuevo (copia uno existente de la misma categoría y edita).
