# What To Do — API

ASP.NET Core 8 Web API (EF Core + SQL Server, ASP.NET Identity, JWT) for the What To Do app: todos with a Kanban board, categories, notes, meetings with meeting notes, budgeting, offline sync, Web Push reminders and local speech-to-text. The web client lives in [What-ToDo-Web](https://github.com/Ikie09/What-ToDo-Web).

## Run it

1. Set the JWT signing key once (kept out of source control):
   ```bash
   cd What-To-Do
   dotnet user-secrets set "Jwt:Key" "<a random string of at least 32 characters>"
   ```
2. Check the `DefaultConnection` string in `What-To-Do/appsettings.Development.json`, then create the database:
   ```bash
   dotnet ef database update
   ```
3. `dotnet run --launch-profile WhatToDo` → https://localhost:7242 (Swagger at `/swagger`).

Push and transcription are optional extras with their own setup below.

## Web Push reminders (also when the app is closed)

The API sends due-todo and upcoming-meeting reminders to subscribed browsers through the standard Web Push protocol (VAPID + `aes128gcm`, so Chrome, Edge, Firefox, Safari and installed iPhone/iPad apps all work).

1. Generate keys and store them in user-secrets (the command prints the exact lines):
   ```bash
   dotnet run -- --generate-vapid-keys
   dotnet user-secrets set "Push:VapidPublicKey" "<public key>"
   dotnet user-secrets set "Push:VapidPrivateKey" "<private key>"
   dotnet user-secrets set "Push:Subject" "mailto:you@example.com"
   ```
   Never commit these. Changing them later invalidates existing subscriptions (devices simply turn push on again).
2. In the web app: Settings → Reminders → *Push notifications*. Browsers only register service workers in a production build, so test with `npm run build && npm run preview` (http://localhost:4173, already allowed by CORS), not `npm run dev`.

What gets sent, once per device:

- **Due today** at 08:00 in the device's time zone (also later in the day if push was enabled after 08:00), and one **Overdue** reminder the next morning. Older overdue items stay silent so turning push on never floods you.
- **Meeting in N min** — `LeadMinutes` before the start (default 10, changeable in Settings; each device keeps its own).

Notes: the reminders run inside the API process, so they only fire while the API is running (fine once deployed; locally only while `dotnet run` is up). Subscription endpoints must belong to a real push service (`Push:AllowedHostSuffixes`) — the server will not POST to arbitrary URLs. Other options: `Push:ReminderIntervalSeconds` (60), `Push:MorningHour` (8).

## Speech-to-text (Whisper, runs on your machine)

`POST /api/transcriptions` transcribes recordings locally with [Whisper](https://github.com/ggerganov/whisper.cpp) through the open-source `Whisper.net` package: no API key, no per-minute cost, and audio never leaves your server.

- The browser sends **16 kHz mono 16-bit WAV** (the web app converts it), so no ffmpeg is needed. Recordings are limited to 15 minutes; one transcription per user at a time; results are kept in memory for 30 minutes and the audio is deleted as soon as it has been processed. Nothing is stored in the database.
- The first start downloads the model once (`Base`, about 142 MB) into a `models/` folder next to the app (git-ignored). Needs internet that one time; set `Transcription:AutoDownload` to `false` and place `ggml-base.bin` there yourself to avoid it.
- It is CPU work. On a 4-core laptop the `Base` model transcribes about real-time or faster. Options in `appsettings.json` → `Transcription`: `Model` (`Tiny` fastest, `Base` default, `Small` most accurate), `Language` (`auto` or a code like `en`/`sw`), `MaxAudioSeconds`, `Enabled` (turn the feature off; the web app then hides the button).
- Endpoints: `GET /api/transcriptions/status`, `POST /api/transcriptions` (multipart field `file`) → `202 {id}`, `GET /api/transcriptions/{id}`, `DELETE /api/transcriptions/{id}`.
- In Docker, mount the `models` folder as a volume so the model is not downloaded on every container start.

## Project layout

```
What-To-Do/
├── Program.cs                  Thin startup: wires the extension methods below
├── Extensions/                 Service registration (database, JWT auth, push, transcription, CORS, Swagger)
├── Common/Json/                Shared serialization helpers (UTC date converter)
├── Domain/
│   ├── Entities/               AppUser, Todo, Category, Note, Meeting, Transaction, Budget, PushSubscription
│   └── Enums/                  PriorityLevel, TodoStatus, TransactionType
├── Data/
│   ├── AppDbContext.cs
│   ├── Configurations/         One IEntityTypeConfiguration per entity
│   └── Migrations/
└── Features/                   One folder per feature: controller + its DTOs
    ├── Auth/                   register / login, token creation
    ├── Todos/                  CRUD, toggle, Kanban reorder
    ├── Categories/
    ├── Notes/
    ├── Meetings/
    ├── Transactions/ Budgets/  Budgeting
    ├── Sync/                   Offline pull / push (last-write-wins)
    ├── Push/                   Web Push: subscriptions, reminder planner + background service
    └── Transcriptions/         Whisper job queue, model manager, upload endpoint
```

## Adding a feature

Add the entity in `Domain/Entities`, its `IEntityTypeConfiguration` in `Data/Configurations`, a `Features/<Name>/` folder with the controller and a `Dtos/` subfolder, then `dotnet ef migrations add <Name>`. To make it available offline, add a DTO to `Features/Sync/Dtos` and handle it in `SyncController`.
