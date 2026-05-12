# Attrition — Web Platform: Master Overview

## What Is This?

This is a set of instruction documents for building the **Attrition** game web platform from scratch. Attrition is a 2D roguelike game (inspired by Dead Cells and Hollow Knight) with built-in multiplayer. This website serves as the game's community hub.

## Document Reading Order

Read and implement in this order:

| # | File | What It Covers |
|---|---|---|
| 01 | `01-PROJECT-SETUP.md` | Scaffolding the project, creating Dockerfiles, docker-compose, env vars |
| 02 | `02-DATABASE.md` | PostgreSQL schema, EF Core migrations, Redis config, seed data |
| 03 | `03-BACKEND-API.md` | ASP.NET Core Web API — every model, controller, service, middleware |
| 04 | `04-DESIGN-SYSTEM.md` | Full CSS design system — glassmorphism, colors, dark/light mode |
| 05 | `05-FRONTEND-CORE.md` | Next.js layout, providers, shared components, music player |
| 06 | `06-FRONTEND-PAGES.md` | Every page in the app with route, layout, and behavior specs |
| 07 | `07-FEATURES.md` | Detailed feature specs — wiki workflow, forum features, auth, uploads |
| 08 | `08-DEPLOYMENT.md` | Docker Compose production config, deploy script, Cloudflare tunnel |

## Tech Stack

| Layer | Technology | Version |
|---|---|---|
| Frontend | Next.js (App Router) | 14 |
| Styling | Vanilla CSS (Glassmorphism) | — |
| Backend API | ASP.NET Core Web API | .NET 8 |
| Database | PostgreSQL | 16 |
| Cache | Redis | 7 |
| Auth | JWT (access + refresh tokens) | — |
| Containers | Docker + Docker Compose | — |
| Target OS | Debian 13 | — |

## Project Directory Structure

```
e:\projects\web-game-test\
├── docs/                          # These instruction files (you are here)
│   ├── 00-OVERVIEW.md
│   ├── 01-PROJECT-SETUP.md
│   ├── 02-DATABASE.md
│   ├── 03-BACKEND-API.md
│   ├── 04-DESIGN-SYSTEM.md
│   ├── 05-FRONTEND-CORE.md
│   ├── 06-FRONTEND-PAGES.md
│   ├── 07-FEATURES.md
│   └── 08-DEPLOYMENT.md
├── Friday Night.mp3               # Theme music file (already present)
│
├── web/                           # Next.js 14 frontend
│   ├── Dockerfile
│   ├── package.json
│   ├── next.config.js
│   ├── tsconfig.json
│   ├── public/
│   │   └── audio/
│   │       └── friday-night.mp3   # Copied from root
│   └── src/
│       ├── app/                   # App Router pages
│       │   ├── globals.css        # Full design system
│       │   ├── layout.tsx         # Root layout (music, theme, nav)
│       │   ├── page.tsx           # Home page
│       │   ├── about/
│       │   ├── wiki/
│       │   ├── forum/
│       │   ├── auth/
│       │   ├── profile/
│       │   ├── admin/
│       │   ├── changelog/
│       │   ├── faq/
│       │   └── contact/
│       ├── components/            # Shared components
│       │   ├── Navbar.tsx
│       │   ├── Footer.tsx
│       │   ├── MusicPlayer.tsx
│       │   ├── ThemeToggle.tsx
│       │   ├── GlassCard.tsx
│       │   ├── Button.tsx
│       │   ├── Input.tsx
│       │   ├── Modal.tsx
│       │   ├── Avatar.tsx
│       │   ├── MarkdownRenderer.tsx
│       │   ├── RichEditor.tsx
│       │   ├── Pagination.tsx
│       │   ├── SearchBar.tsx
│       │   ├── Toast.tsx
│       │   ├── Badge.tsx
│       │   └── Breadcrumb.tsx
│       ├── contexts/
│       │   ├── AuthContext.tsx
│       │   └── ThemeContext.tsx
│       └── lib/
│           └── api.ts             # API client
│
├── api/                           # ASP.NET Core Web API
│   ├── Dockerfile
│   ├── Attrition.API.csproj
│   ├── Program.cs
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   ├── Controllers/
│   │   ├── AuthController.cs
│   │   ├── UsersController.cs
│   │   ├── AdminController.cs
│   │   ├── WikiController.cs
│   │   ├── ForumController.cs
│   │   └── ForumModController.cs
│   ├── Models/
│   │   ├── User.cs
│   │   ├── WikiCategory.cs
│   │   ├── WikiArticle.cs
│   │   ├── WikiRevision.cs
│   │   ├── WikiContribution.cs
│   │   ├── ForumCategory.cs
│   │   ├── ForumThread.cs
│   │   ├── ForumPost.cs
│   │   └── ForumReaction.cs
│   ├── DTOs/                      # Request/Response DTOs
│   ├── Services/
│   │   ├── AuthService.cs
│   │   ├── WikiService.cs
│   │   ├── ForumService.cs
│   │   ├── FileService.cs
│   │   └── CacheService.cs
│   ├── Data/
│   │   ├── AppDbContext.cs
│   │   ├── SeedData.cs
│   │   └── Migrations/
│   ├── Middleware/
│   │   └── ErrorHandlingMiddleware.cs
│   └── Validators/
│       ├── RegisterValidator.cs
│       └── PasswordValidator.cs
│
├── docker-compose.yml
├── .env.example
├── .env                           # Actual env (gitignored)
├── .gitignore
└── deploy.py
```

## Architecture Diagram

```
Browser
  │
  ▼
Next.js 14 (Port 3000)
  │  - Server-side rendering
  │  - Static assets (music, images)
  │  - API route proxying (optional)
  │
  ▼  HTTP/JSON
ASP.NET Core API (Port 5000)
  │  - JWT Authentication
  │  - Business logic
  │  - File uploads
  │
  ├──▶ PostgreSQL 16 (Port 5432)
  │       - All persistent data
  │
  └──▶ Redis 7 (Port 6379)
          - Caching
          - Rate limiting
          - Session tracking
```

## Network Topology

```
Internet
  │
  ▼
attrition.hault.io.vn (Cloudflare DNS)
  │
  ▼
Cloudflare Tunnel
  │
  ▼
cloudflared (LAN machine)
  │
  ▼
192.168.1.110:3000 (Debian 13 server)
  │
  └── Docker containers (web, api, db, redis)
```

## Key Design Decisions

1. **Glassmorphism UI** — frosted glass cards, backdrop blur, semi-transparent backgrounds. Blue/navy color palette. Both light and dark mode.
2. **Normal, clean design** — not overly dramatic or dark. Professional game website feel.
3. **Music player** — Friday Night.mp3 plays at 30% volume on page load, toggleable, persists across navigation (lives in root layout).
4. **Admin/User separation** — Role-based. Admin has full CRUD on wiki, moderation on forum. Users can contribute to wiki and post on forum.
5. **Admin seed** — `admin123/admin123` seeded on first startup, bypasses password strength rules, flagged for password change.
6. **JWT auth** — Access token (short-lived, ~15 min) + Refresh token (long-lived, ~7 days). Stored in httpOnly cookies or localStorage.
7. **Docker everything** — All 4 services containerized, single `docker-compose up` to run.

## Important Notes for the Implementing Agent

- The music file `Friday Night.mp3` already exists at the project root. Copy it to `web/public/audio/friday-night.mp3` during setup.
- Do NOT install nginx or any reverse proxy — the user handles that via Cloudflare Tunnel.
- Do NOT set up SSL — Cloudflare handles HTTPS termination.
- The remote server is Debian 13 at `root@192.168.1.110` (password: `12345`). The deploy script should handle SSH + docker compose.
- Use random images from the web (e.g., Unsplash, placeholder services) for any game art, category icons, hero images, etc. There are no existing game assets.
- Password hashing: BCrypt. Password requirements: min 8 chars, 1 uppercase, 1 lowercase, 1 digit, 1 special char.
- All API responses should follow a consistent envelope: `{ success: bool, data: T?, error: string? }`
