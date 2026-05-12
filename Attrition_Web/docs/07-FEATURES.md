# 07 — Feature Specifications

Detailed behavioral specs for all major features.

---

## 1. Authentication & Accounts

### Registration Flow
1. User submits username + password
2. Backend validates: username unique, 3-30 chars, alphanumeric + underscore only
3. Backend validates password: min 8 chars, 1 uppercase, 1 lowercase, 1 digit, 1 special char
4. Hash password with BCrypt (work factor 12)
5. Create user with role "User"
6. Generate JWT access token (15 min) + refresh token (7 days, stored in DB)
7. Return tokens + user info

### Login Flow
1. User submits username + password
2. Find user by username (case-insensitive)
3. If user is banned → return error "Account is suspended"
4. Verify BCrypt hash
5. Generate new token pair
6. If `MustChangePassword` is true → include flag in response, frontend should show password change modal

### Token Refresh
1. Client sends refresh token
2. Find user by stored refresh token, check not expired
3. Issue new access + refresh tokens
4. Old refresh token is replaced (rotation)

### JWT Claims
```json
{
  "sub": "<user-guid>",
  "username": "<username>",
  "role": "User|Admin",
  "iat": 1234567890,
  "exp": 1234568790
}
```

### Admin Seed Account
- Username: `admin123`, Password: `admin123`
- Hashed with BCrypt on first startup
- `MustChangePassword = true`
- Bypasses password strength validation (only during seed)

---

## 2. Wiki System

### Article Lifecycle
```
Admin creates article (Draft or Published)
  → Users can view Published articles
  → Users can "Suggest Edit" (creates WikiContribution, status=Pending)
  → Admin reviews contributions:
      - Approve: content applied to article, revision created, contributor's count incremented
      - Reject: status set to Rejected
  → Admin can also directly edit (creates revision)
```

### Slugs
Auto-generate from title: lowercase, replace spaces with hyphens, remove special chars.
Example: "Flame Sword of the Abyss" → `flame-sword-of-the-abyss`

### Revision History
Every edit (admin or approved contribution) creates a WikiRevision with the **previous** content, editor info, and timestamp. This allows viewing the history and potentially reverting.

### Content Format
Markdown with support for:
- Headings (h1-h6)
- Bold, italic, strikethrough
- Ordered/unordered lists
- Code blocks (inline and fenced)
- Tables (GFM)
- Images (URL-based)
- Links
- Blockquotes

### Caching
- Cache article by slug in Redis (10 min TTL)
- Cache category list (30 min TTL)
- Invalidate on any write (create, update, approve contribution)

---

## 3. Forum System

### Thread + Post Model
- A thread belongs to a category
- The first post in a thread is created alongside the thread (the thread content IS the first post)
- Subsequent posts are replies
- Thread's `ReplyCount` = total posts - 1 (exclude the OP)
- Thread's `LastReplyAt` updates on each new post

### Reactions
- Types: `like`, `dislike`
- Toggle behavior: clicking the same reaction removes it, clicking a different one switches
- One reaction per user per post
- Show counts on each post
- Show current user's reaction (highlighted button) when authenticated

### Moderation (Admin)
- **Pin/Unpin**: Pinned threads appear at the top of the category, marked with a pin icon/badge
- **Lock/Unlock**: Locked threads cannot receive new posts. Show lock icon/badge. Existing posts remain.
- **Delete Thread**: Soft delete or hard delete the thread and all its posts
- **Delete Post**: Admin can delete any post. Author can delete their own.

### Post Editing
- Only the author can edit their own post
- Edited posts show "(edited)" with the edit timestamp
- No revision history for forum posts (unlike wiki)

### Search
- Forum search queries thread titles
- Wiki search queries article titles
- Both use SQL `ILIKE` or `LOWER()` + `LIKE` for case-insensitive search

---

## 4. File Upload (Avatars)

### Constraints
- Max file size: 5 MB
- Allowed types: `image/jpeg`, `image/png`, `image/gif`, `image/webp`
- Storage: `/app/uploads/avatars/` inside the API container (Docker volume)

### Upload Flow
1. User selects file via file input or drag-and-drop
2. Frontend validates file size (< 5MB) and type before upload
3. `POST /api/users/avatar` with `multipart/form-data`
4. Backend validates again (size + type)
5. Save as `{userId}.{extension}` (overwrite previous avatar)
6. Update user's `AvatarPath` in DB
7. Return the new avatar URL

### Serving Avatars
Option A: Serve via ASP.NET Core `UseStaticFiles()` mapped to `/uploads/`
Option B: Dedicated `GET /api/users/{id}/avatar` endpoint that streams the file
Either works. Option A is simpler.

### Default Avatar
If user has no avatar, show a generated fallback: colored circle with user's first initial, using a deterministic color based on username hash.

---

## 5. Music Player

### Behavior
- Audio source: `/audio/friday-night.mp3` (static file in Next.js public dir)
- Volume: fixed at 30% (`0.3`)
- Loop: `true`
- Default state: ON (playing)
- Persists across page navigation (component in root layout, never unmounts)
- Preference persisted in `localStorage` key `attrition-music` (values: `"on"` or `"off"`)

### Autoplay Handling
Browsers block autoplay until user interaction. Solution:
- On first page load, register `click` and `keydown` listeners on `document`
- On first interaction, start playback if preference is ON
- Remove listeners after first interaction

### UI
- Floating circular button in bottom-right corner (above footer, below content)
- Show speaker icon when playing, muted speaker icon when paused
- Hover: scale up slightly, change background to accent color
- Z-index: 900 (below modals at 1000)

---

## 6. Theme Switching (Light/Dark)

### Behavior
- Toggle button in navbar (sun icon for light, moon icon for dark)
- Sets `data-theme="dark"` attribute on `<html>` element
- All CSS transitions use `var(--transition-base)` for smooth switch
- Preference persisted in `localStorage` key `attrition-theme` (values: `"light"` or `"dark"`)
- Default: light mode (no attribute = light)

### Implementation
- `ThemeContext` manages state and provides `toggleTheme`
- On mount, read from localStorage and apply immediately (before paint if possible)
- All color values reference CSS custom properties that change with `[data-theme="dark"]`

---

## 7. API Response Envelope

All API responses follow this format:

```json
// Success
{
  "success": true,
  "data": { ... }
}

// Error
{
  "success": false,
  "error": "Human-readable error message"
}

// Paginated
{
  "success": true,
  "data": {
    "items": [ ... ],
    "totalCount": 100,
    "page": 1,
    "pageSize": 20
  }
}
```

---

## 8. Rate Limiting (Optional but Recommended)

Use Redis-based rate limiting on sensitive endpoints:
- `POST /api/auth/login` — 5 attempts per minute per IP
- `POST /api/auth/register` — 3 attempts per minute per IP
- `POST /api/users/avatar` — 10 uploads per hour per user

Implementation: increment a Redis key `ratelimit:{ip}:{endpoint}` with TTL.

---

## Next Step

Proceed to `08-DEPLOYMENT.md` for Docker Compose production config and deployment.
