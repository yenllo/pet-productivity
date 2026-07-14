# Especificación: Tienda, Objetos y Monetización

Objetivo: convertir la tienda actual (lista plana de ítems con emoji y precio) en una **tienda real**
que aproveche los **cientos de sprites** (con múltiples vistas) como **objetos colocables tipo Sims**,
y abrir la vía de **ingresos** (objetos pagados con dinero real: únicos, de eventos, especiales,
colaboraciones), además de los objetos comunes que se compran con **oro** ganado jugando.

## 1. Modelo de Objeto (cada sprite = un objeto)
Cada objeto del catálogo:
```
RoomObject {
  Id            : string   // "bongseng_bed_blue", "event_xmas_tree_2026"
  Name          : string
  Category      : enum { Floor, Wall, Furniture, Decor, RoomStyle, Consumable }
  Slot          : enum { FloorTile, WallLeft, WallRight, FloorObject, Ceiling }  // dónde se ancla
  Footprint     : (w, d)   // celdas de grilla que ocupa (ej 2x2 cama)
  Views         : [string] // claves de sprite por orientación: "ne","nw","se","sw" (aprovecha las vistas)
  DefaultView   : string
  // --- economía ---
  Currency      : enum { Gold, Premium }   // Gold = ganado jugando ; Premium = dinero real
  Price         : int                       // en oro, o en la moneda premium / centavos
  Rarity        : enum { Common, Rare, Unique, Event, Collab }
  Availability  : { From?: date, To?: date } // null = siempre; con fechas = ventana de evento
  Source        : string?  // "Colab: <marca/artista>" para colaboraciones
  Tradable      : bool
}
```
- Los **sprites con varias vistas** se guardan como `Views` del MISMO objeto (no objetos separados):
  al colocar/rotar el mueble, el código elige la vista. Esto es clave para "aprovechar los puntos de
  vista de los sprites".
- **RoomStyle** sigue el contrato de `ROOM_PIECE_SPEC.md` (un `room_bg_<estilo>.png`).

## 2. Monetización (dónde ganas)
| Tier | Moneda | Ejemplos | Disponibilidad |
|---|---|---|---|
| **Common/Rare** | **Oro** (ganado) | muebles base, estilos básicos | siempre |
| **Unique** | **Premium** ($) | objeto exclusivo, edición limitada | siempre o stock limitado |
| **Event** | **Premium** o evento | árbol navideño 2026, objeto de Halloween | **ventana de fechas** (`Availability`) |
| **Special** | **Premium** | bundles, objeto "founder" | lanzamientos puntuales |
| **Collab** | **Premium** | objeto de marca/artista invitado | ventana + crédito `Source` |
- **Regla de oro del proyecto:** lo premium es **100% cosmético** (no acelera evolución; eso solo sube
  con XP de tareas). Mantener el balance: el oro nunca compra objetos Premium.
- **Validación server-side obligatoria:** precio, propiedad y ventana de disponibilidad se verifican en
  el servidor (no confiar en el cliente). El pago real se confirma con el proveedor antes de otorgar.
- Pago real: integrar **Google Play Billing** (Android) — NO meter claves en el repo; el servidor valida
  el recibo. (Fase posterior; primero el catálogo/UX.)

## 3. UX de tienda (más "tienda")
- **Pestañas/filtros por categoría** (hoy los chips son estáticos → cablearlos): Muebles, Decoración,
  Estilos, Paredes/Pisos, Eventos, Consumibles.
- **Sección "Eventos / Limitado"** destacada arriba con contador de tiempo (usa `Availability.To`).
- **Tarjeta de objeto** con: sprite real (no emoji), nombre, rareza (badge), precio con icono de moneda
  (oro 🪙 o premium 💎/$), botón **Comprar** → luego **Equipar/Colocar**.
- **Vista previa**: al tocar, mostrar el objeto en el cuarto (o un mini-render) antes de comprar.
- Estado por objeto: No poseído → "Comprar"; Poseído → "Colocar"/"Equipar"; Colocado → "Quitar".

## 4. Colocación tipo Sims (encima del fondo)
- Reusar `RoomGrid` + `FurniturePlacement` (ya existen en `Controls/RoomGrid.cs`).
- El usuario entra a **modo edición** desde el cuarto: arrastrar para mover, tocar para rotar (cambia
  `Views`), botón quitar. Persistir en `User.PlacedFurniture` (slot/celda → objectId + vista).
- Colisiones: `RoomGrid.IsAreaFree` ya valida solape y límites.

## 5. Cómo mapea sobre el código actual
- `Shared/Models/ShopItem.cs` → evolucionar a `RoomObject` (mantener compat: los campos actuales
  Name/Icon/Price quedan; añadir Category/Slot/Footprint/Views/Currency/Rarity/Availability/Source).
- `Shared/Models/User.cs` → ya tiene `Inventory` (poseídos), `ActiveRoomStyle`, `PlacedFurniture`.
  Añadir `PremiumEntitlements` (compras reales validadas) si hace falta separar de Inventory.
- `Server/Controllers/ShopController.cs` → catálogo ampliado + endpoints: `catalog` (con filtros),
  `buy` (oro), `purchase-premium` (valida recibo), `equip`, `place`/`unplace`.
- Cliente: `ShopViewModel`/`ShopPage` (filtros + tarjetas con sprite + comprar/colocar);
  `RoomDiorama` ya dibuja muebles desde grilla.

## 6. Plan por fases (para no romper nada ni gastar de más)
1. **F-Tienda-1 (datos+UX):** modelo `RoomObject`, catálogo seed (decenas de objetos de los packs ya
   bajados, con sus vistas), filtros por categoría, tarjetas con sprite, comprar con **oro** + equipar.
   *Sin dinero real todavía.* Migración EF aditiva.
2. **F-Tienda-2 (colocación Sims):** modo edición (mover/rotar/quitar) persistido en `PlacedFurniture`.
3. **F-Tienda-3 (eventos):** `Availability` por fechas + sección "Limitado" con contador.
4. **F-Tienda-4 (dinero real):** Google Play Billing + validación de recibo server-side para Unique/
   Event/Special/Collab. (Requiere cuenta de Play Console del dueño.)

## 7. Importar sprites como objetos (evitar trabajo pixel a pixel)
- Convención de carpetas en `IsometricThings/seleccionados/...` → script que genera el seed del catálogo:
  por cada carpeta de mueble con archivos `*left/right/...`, crear un `RoomObject` con sus `Views`.
- Copiar solo los PNG usados a `Resources/Raw/obj_<id>_<view>.png` (no meter el demo de Unity ni packs
  crudos al repo; `IsometricThings/` debe ir en `.gitignore`).
