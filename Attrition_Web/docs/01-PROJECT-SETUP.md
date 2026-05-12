# 01 — Project Setup

This document covers initial project scaffolding, Docker configuration, and environment variables.

---

## Step 1: Initialize Next.js 14 Frontend

```bash
cd e:\projects\web-game-test
npx -y create-next-app@14 ./web --typescript --app --eslint --src-dir --no-tailwind --import-alias "@/*"
```

Install additional dependencies:

```bash
cd web
npm install react-markdown remark-gfm rehype-raw rehype-sanitize @uiw/react-md-editor react-icons
```

### `next.config.js`

```javascript
/** @type {import('next').NextConfig} */
const nextConfig = {
  output: 'standalone',
  images: {
    remotePatterns: [
      { protocol: 'https', hostname: 'images.unsplash.com' },
      { protocol: 'http', hostname: 'api', port: '5000' },
    ],
  },
  async rewrites() {
    return [
      {
        source: '/api/:path*',
        destination: `${process.env.API_URL || 'http://localhost:5000'}/api/:path*`,
      },
    ];
  },
};
module.exports = nextConfig;
```

This rewrites `/api/*` requests to the backend so frontend and API share the same origin.

---

## Step 2: Initialize ASP.NET Core Backend

```bash
cd e:\projects\web-game-test
dotnet new webapi -n Attrition.API -o api --framework net8.0 --no-https
```

Install NuGet packages:

```bash
cd api
dotnet add package Microsoft.EntityFrameworkCore --version 8.0.*
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL --version 8.0.*
dotnet add package Microsoft.EntityFrameworkCore.Design --version 8.0.*
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer --version 8.0.*
dotnet add package StackExchange.Redis --version 2.*
dotnet add package BCrypt.Net-Next --version 4.*
dotnet add package FluentValidation.AspNetCore --version 11.*
```

Delete auto-generated `WeatherForecast.cs` and `Controllers/WeatherForecastController.cs`.

---

## Step 3: Copy Music File

```powershell
New-Item -ItemType Directory -Force -Path web\public\audio
Copy-Item "Friday Night.mp3" -Destination "web\public\audio\friday-night.mp3"
```

---

## Step 4: `.env.example`

```env
POSTGRES_USER=attrition_user
POSTGRES_PASSWORD=changeme_strong_password_here
POSTGRES_DB=attrition
REDIS_URL=redis:6379
JWT_SECRET=your-super-secret-jwt-key-at-least-32-chars-long-change-this
JWT_ISSUER=attrition-api
JWT_AUDIENCE=attrition-web
JWT_ACCESS_TOKEN_EXPIRY_MINUTES=15
JWT_REFRESH_TOKEN_EXPIRY_DAYS=7
API_URL=http://api:5000
MAX_AVATAR_SIZE_MB=5
UPLOAD_PATH=/app/uploads
```

Copy to `.env` and fill in real values for deployment.

---

## Step 5: `.gitignore`

```gitignore
.env
*.env.local
web/node_modules/
web/.next/
web/out/
api/bin/
api/obj/
docker-compose.override.yml
.DS_Store
Thumbs.db
uploads/
.vs/
.vscode/
```

---

## Step 6: Dockerfiles

### `web/Dockerfile`

```dockerfile
FROM node:20-alpine AS deps
WORKDIR /app
COPY package.json package-lock.json* ./
RUN npm ci

FROM node:20-alpine AS builder
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY . .
ENV NEXT_TELEMETRY_DISABLED=1
RUN npm run build

FROM node:20-alpine AS runner
WORKDIR /app
ENV NODE_ENV=production
ENV NEXT_TELEMETRY_DISABLED=1
RUN addgroup --system --gid 1001 nodejs
RUN adduser --system --uid 1001 nextjs
COPY --from=builder /app/public ./public
COPY --from=builder --chown=nextjs:nodejs /app/.next/standalone ./
COPY --from=builder --chown=nextjs:nodejs /app/.next/static ./.next/static
USER nextjs
EXPOSE 3000
ENV PORT=3000
ENV HOSTNAME="0.0.0.0"
CMD ["node", "server.js"]
```

### `api/Dockerfile`

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY *.csproj ./
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /app/uploads/avatars
ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000
ENTRYPOINT ["dotnet", "Attrition.API.dll"]
```

---

## Step 7: `docker-compose.yml`

```yaml
version: '3.8'

services:
  web:
    build: { context: ./web, dockerfile: Dockerfile }
    container_name: attrition-web
    ports: ["3000:3000"]
    environment:
      - API_URL=http://api:5000
    depends_on:
      api: { condition: service_healthy }
    restart: unless-stopped
    networks: [attrition-net]

  api:
    build: { context: ./api, dockerfile: Dockerfile }
    container_name: attrition-api
    ports: ["5000:5000"]
    environment:
      - ConnectionStrings__DefaultConnection=Host=db;Port=5432;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}
      - Redis__ConnectionString=${REDIS_URL}
      - Jwt__Secret=${JWT_SECRET}
      - Jwt__Issuer=${JWT_ISSUER}
      - Jwt__Audience=${JWT_AUDIENCE}
      - Jwt__AccessTokenExpiryMinutes=${JWT_ACCESS_TOKEN_EXPIRY_MINUTES}
      - Jwt__RefreshTokenExpiryDays=${JWT_REFRESH_TOKEN_EXPIRY_DAYS}
      - FileUpload__MaxAvatarSizeMB=${MAX_AVATAR_SIZE_MB}
      - FileUpload__UploadPath=${UPLOAD_PATH}
    depends_on:
      db: { condition: service_healthy }
      redis: { condition: service_started }
    volumes: [uploads:/app/uploads]
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:5000/health"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks: [attrition-net]

  db:
    image: postgres:16-alpine
    container_name: attrition-db
    environment:
      - POSTGRES_DB=${POSTGRES_DB}
      - POSTGRES_USER=${POSTGRES_USER}
      - POSTGRES_PASSWORD=${POSTGRES_PASSWORD}
    volumes: [pgdata:/var/lib/postgresql/data]
    ports: ["5432:5432"]
    restart: unless-stopped
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER} -d ${POSTGRES_DB}"]
      interval: 5s
      timeout: 5s
      retries: 5
    networks: [attrition-net]

  redis:
    image: redis:7-alpine
    container_name: attrition-redis
    command: redis-server --appendonly yes
    volumes: [redisdata:/data]
    ports: ["6379:6379"]
    restart: unless-stopped
    networks: [attrition-net]

volumes:
  pgdata:
  redisdata:
  uploads:

networks:
  attrition-net:
    driver: bridge
```

---

## Next Step

Proceed to `02-DATABASE.md` to set up the database schema and seed data.
