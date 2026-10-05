# What To Do — API

ASP.NET Core 8 Web API (EF Core + SQL Server, ASP.NET Identity, JWT) for the What To Do app: todos with a Kanban board, categories, notes, meetings with meeting notes, and offline sync. The web client lives in [What-ToDo-Web](https://github.com/Ikie09/What-ToDo-Web).

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

## Project layout

```
What-To-Do/
├── Program.cs                  Thin startup: wires the extension methods below
├── Extensions/                 Service registration (database, JWT auth, CORS, Swagger)
├── Common/Json/                Shared serialization helpers (UTC date converter)
├── Domain/
│   ├── Entities/               AppUser, Todo, Category, Note, Meeting
│   └── Enums/                  PriorityLevel, TodoStatus
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
    └── Sync/                   Offline pull / push (last-write-wins)
```

## Adding a feature

Add the entity in `Domain/Entities`, its `IEntityTypeConfiguration` in `Data/Configurations`, a `Features/<Name>/` folder with the controller and a `Dtos/` subfolder, then `dotnet ef migrations add <Name>`. To make it available offline, add a DTO to `Features/Sync/Dtos` and handle it in `SyncController`.
