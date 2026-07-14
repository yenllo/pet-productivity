# Assets

## Why the furniture sprites aren't here

The isometric furniture, walls and floors come from two asset packs by
**[Bongseng](https://bongseng.itch.io/)**:

- *Semi realist room generator — sprites appartment*
- *Christmas update* (expansion of the same pack)

Their license permits using and modifying them in commercial and non-commercial projects, but
explicitly forbids redistribution: *"you can't redistribute or resell this asset pack in total or
partly"*. A public Git repository is redistribution, so the 316 sprite files are excluded via
`.gitignore`. Credit to Bongseng — the room would be a grey box without them.

## What *is* here

Original artwork for this project, included and covered by the repo license:

- Pet sprites for the three species (Sprout / Ember / Aqua) at Baby, Adult and Master stages.
- Egg-cracking animation frames and the crystallized (dead) pet.
- Room backgrounds (`room_bg*.png`): default, bathroom, kitchen, loft, forest, galaxy.
- The catalog objects that were designed for this project rather than taken from the packs.

## Building the client

The **server needs no images at all** — it only reads `Catalog/**/info.json`. Deploy it as-is.

The **Android client** bundles the sprites as `MauiAsset`, so to build it you need the art on disk:

1. Get the two packs from [bongseng.itch.io](https://bongseng.itch.io/).
2. Drop each sprite into its catalog folder as `Catalog/<Category>/<Item>/obj_<name>_l.png` (and
   `_r.png` for objects with a left/right facing variant). The folder names and the expected file
   names are already in the repo — each `info.json` sits next to where its PNG belongs, so the
   layout tells you exactly what goes where.
3. Furniture the client renders directly also lives in
   `src/PetProductivity.Client/Resources/Raw/`.

Without the art the client will build but render missing objects; the server, the API, the AI
judge and every game rule work normally.
