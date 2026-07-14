# PetProductivity

A gamified productivity app. You describe, in plain language, something you actually did —
*"I finally cleaned the kitchen"*, *"studied 2 hours of linear algebra"* — and an AI judge
rates it (difficulty 1–10 + category) and pays you in two currencies:

- **Stats**, which make your pet evolve (Egg → Baby → Adult → Master, branching by dominant stat).
- **Gold**, which is purely cosmetic and decorates the room your pet lives in.

Neglect it and the pet's health decays. Let it hit zero and it **crystallizes and dies** — and
the only way to revive it is to come back with a task the AI rates **difficulty ≥ 9**. That's the
Phoenix mechanic, and it's the whole point: the pet is a hostage to your follow-through.

You also get a **personal pet** of your own, and can join **groups of 2–6 people that share one
pet**. The shared pet is asleep until at least two people join, nobody "owns" it, and it lives or
dies on the group's collective output. When several members are working at the same time, the app
enters **Frenzy** mode and doubles rewards in real time.

> Code and comments are in Spanish. This README is in English.

## Stack

| Layer | Tech |
|---|---|
| Mobile client | .NET 10 MAUI (Android) |
| Server | ASP.NET Core 10 Web API + EF Core |
| Database | PostgreSQL (Supabase) |
| AI judge | Google Gemini (text for task rating, Vision for photo proofs) |
| Real-time | SignalR (`FamilyHub`) |
| Push | Firebase Cloud Messaging |
| Deploy | Docker → Render |

## Things in here that were actually interesting to build

- **The server is the only source of truth.** The client is treated as hostile: the shop catalog,
  prices, rewards and evolution all resolve server-side, and every endpoint and the SignalR hub
  take the user id from the JWT, never from the request body. A cracked client can't mint gold.
- **AI as a game mechanic, not a chatbot.** `GeminiAiService` judges a free-text task and returns
  structured difficulty + category, with a heuristic fallback so the game still works if the model
  is down.
- **Photo-verified focus sessions.** A pomodoro that asks for a photo halfway through and sends it
  to Gemini Vision to check you're actually doing the thing you claimed. Verdicts are stored per
  session; the photos are deleted after 30 days by a hosted service.
- **Isometric room diorama** rendered with SkiaSharp: a swappable isometric background plus
  Sims-style furniture placement on a grid, persisted per user.
- **The shop catalog is a folder on disk.** `Catalog/<Category>/<Item>/info.json` — 205 items.
  Editing a JSON file and restarting the server changes the store. No admin panel, no migration.
- **Real-time group presence** — availability semaphore, synchronized group focus sessions, and
  the Frenzy multiplier, all over SignalR authenticated with the same JWT.

## Assets are not in this repository

The isometric furniture sprites are by **[Bongseng](https://bongseng.itch.io/)**, whose license
allows using them inside a commercial product but **forbids redistributing them** — and publishing
them in an open repo would be exactly that. So they are not here. See [ASSETS.md](ASSETS.md).

Consequence, and it's a small one:

- **The server builds and runs with zero images.** It only reads the `info.json` files. Anyone can
  clone this and deploy the backend as-is.
- **The Android client needs the sprites on disk** to build. Get them from Bongseng's itch.io page
  and place them as described in [ASSETS.md](ASSETS.md).

Pet sprites, egg animation frames and room backgrounds are original to this project and *are*
included.

## Running the server

```bash
dotnet run --project src/PetProductivity.Server --launch-profile http
```

Serves on `http://0.0.0.0:5051` and applies EF migrations on startup. Configure via user-secrets or
environment variables (`appsettings.json` ships with placeholders only):

```
ConnectionStrings__DefaultConnection   # Postgres
Gemini__ApiKey                         # Google Gemini API key
Jwt__Key                               # HS256 signing key
```

## Running the Android client

```bash
dotnet build src/PetProductivity.Client/PetProductivity.Client.csproj -f net10.0-android -t:Run
```

Point it at a local server from **Settings → Server Address** (`http://10.0.2.2:5051` on an
emulator). The default is production.

## History

Started **December 5, 2025**. This repo is a clean republication of a private one whose history
carried rotated credentials and non-redistributable art — the real commit log is preserved in
[HISTORY.md](HISTORY.md).

## License

[AGPL-3.0](LICENSE). Third-party art is **not** covered by it and is not distributed here.
