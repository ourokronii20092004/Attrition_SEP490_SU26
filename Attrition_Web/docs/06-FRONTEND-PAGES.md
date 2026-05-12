# 06 — Frontend Pages

All page routes, their layouts, data fetching, and behavior.

---

## `/` — Home Page

**File**: `web/src/app/page.tsx`

Sections (top to bottom):
1. **Hero**: Full-width section with background gradient (blue to navy), large heading "Attrition", subtitle "A 2D Roguelike with Built-in Multiplayer", and two CTA buttons: "Explore Wiki" and "Join Forum". Use a random game-style image from Unsplash as a subtle background (e.g. `https://images.unsplash.com/photo-1550745165-9bc0b252726f` — pixel art style).
2. **Features Grid**: 3-column grid of glass cards: "Explore Biomes", "Master Weapons", "Play Together" with icons and short descriptions.
3. **Latest Wiki Articles**: Horizontal scroll of the 4 most recently updated wiki articles as glass cards. Fetch from `GET /api/wiki/articles?pageSize=4`.
4. **Latest Forum Threads**: List of 5 recent threads with author, category, and time. Fetch from `GET /api/forum/threads?pageSize=5`.
5. **CTA Banner**: "Ready to join the community?" with Register button.

---

## `/about` — About Page

**File**: `web/src/app/about/page.tsx`

Content sections in glass cards:
1. **About the Game**: 2-3 paragraphs describing Attrition. Mention it's a 2D roguelike inspired by Dead Cells and Hollow Knight with multiplayer. Write anything reasonable.
2. **Key Features**: Grid of features — Procedural Dungeons, Multiplayer Co-op, Boss Fights, Build Variety, Skill Trees, Environmental Puzzles.
3. **The Team**: Placeholder team section with 3-4 fictional developer names and roles.

---

## `/wiki` — Wiki Index

**File**: `web/src/app/wiki/page.tsx`

- Breadcrumb: Home / Wiki
- Heading: "Attrition Wiki"
- Description paragraph
- **Category Grid**: 2-column grid (3 on desktop) of glass cards, each showing category name, description, icon (use emoji or react-icons), and article count. Clicking navigates to `/wiki/[slug]`.
- Fetch: `GET /api/wiki/categories`

---

## `/wiki/[category]` — Wiki Category Page

**File**: `web/src/app/wiki/[category]/page.tsx`

- Breadcrumb: Home / Wiki / {Category Name}
- Heading: category name
- SearchBar to filter articles by title
- Article list: glass cards with title, last editor, updated date. Click → `/wiki/[category]/[slug]`
- Fetch: `GET /api/wiki/articles?category={slug}&search={query}&page={page}`
- Pagination at bottom

---

## `/wiki/[category]/[slug]` — Wiki Article Page

**File**: `web/src/app/wiki/[category]/[slug]/page.tsx`

- Breadcrumb: Home / Wiki / {Category} / {Article Title}
- Article title as h1
- Metadata bar: Author, Last edited by, Last updated date
- Article content rendered via `MarkdownRenderer` (supports images, tables, code blocks)
- **Sidebar** (desktop): Table of contents auto-generated from headings
- **"Suggest Edit" button** (visible to logged-in users): Opens `/wiki/contribute?article={id}` or an inline editor modal
- **"Edit" button** (visible to admins): Opens `/admin/wiki/edit/[id]`
- **Revision History** collapsible section at bottom

---

## `/wiki/contribute` — Contribute Page

**File**: `web/src/app/wiki/contribute/page.tsx`

- Requires auth (redirect to login if not authenticated)
- Form with: article selector (dropdown of all articles), RichEditor for suggested content, change note text input, submit button
- On submit: `POST /api/wiki/articles/{id}/suggest`
- Success: toast notification "Suggestion submitted for review!"

---

## `/forum` — Forum Index

**File**: `web/src/app/forum/page.tsx`

- Heading: "Community Forum"
- List of forum categories as glass cards: category name, description, thread count, latest activity timestamp
- Click → `/forum/[slug]`
- Fetch: `GET /api/forum/categories`

---

## `/forum/[category]` — Forum Category Page

**File**: `web/src/app/forum/[category]/page.tsx`

- Breadcrumb: Home / Forum / {Category Name}
- "New Thread" button (requires auth)
- **Pinned threads** section at top (highlighted with badge)
- **Thread list**: glass cards with title, author avatar + name, reply count, last reply time
- Locked threads show a lock icon
- SearchBar for searching threads
- Pagination
- Fetch: `GET /api/forum/threads?category={slug}&page={page}&search={query}`

---

## `/forum/[category]/[threadId]` — Thread View

**File**: `web/src/app/forum/[category]/[threadId]/page.tsx`

- Breadcrumb: Home / Forum / {Category} / {Thread Title}
- Thread title as h1, with pinned/locked badges if applicable
- **Post list**: each post is a glass card containing:
  - Left: author avatar, username, role badge, post count
  - Right: post content (rendered as markdown), timestamp, edit/delete buttons (own posts only)
  - Bottom: reaction buttons (👍 like count, 👎 dislike count), highlight user's current reaction
- **Reply form** at bottom (if thread not locked and user is authenticated): RichEditor + submit
- Admin controls: Pin/Unpin, Lock/Unlock, Delete Thread buttons
- Pagination for posts

---

## `/forum/new` — New Thread

**File**: `web/src/app/forum/new/page.tsx`

- Requires auth
- Form: category dropdown, title input, content RichEditor, submit button
- On submit: `POST /api/forum/threads`
- Redirect to new thread on success

---

## `/auth/login` — Login Page

**File**: `web/src/app/auth/login/page.tsx`

- Centered glass card
- Heading: "Welcome Back"
- Username input, password input, submit button
- Link: "Don't have an account? Register"
- On success: redirect to home (or previous page)
- On error: show error message inline
- If user `mustChangePassword`, redirect to a password change modal/page after login

---

## `/auth/register` — Register Page

**File**: `web/src/app/auth/register/page.tsx`

- Centered glass card
- Heading: "Create Account"
- Username input, password input (with strength indicator bar), confirm password input, submit button
- Link: "Already have an account? Login"
- **Password strength indicator**: Visual bar that shows red (weak) → yellow (fair) → green (strong) based on requirements met (8+ chars, uppercase, lowercase, digit, special char)
- Show real-time validation messages below password field

---

## `/profile/[username]` — Public Profile

**File**: `web/src/app/profile/[username]/page.tsx`

- Large avatar, username, role badge, bio
- Stats row: Join date, Post count, Wiki contributions
- Recent forum posts (latest 5)
- Fetch: `GET /api/users/{username}/profile`

---

## `/profile/settings` — Profile Settings

**File**: `web/src/app/profile/settings/page.tsx`

- Requires auth
- **Avatar section**: Current avatar preview, file upload button (max 5MB, jpg/png/gif/webp), drag-and-drop zone
- **Bio section**: Textarea, save button
- **Change Password section**: Current password, new password (with strength indicator), confirm, save button
- Each section is a separate glass card

---

## `/admin` — Admin Dashboard

**File**: `web/src/app/admin/page.tsx`

- Requires admin role (redirect non-admins)
- Stats overview: total users, total wiki articles, total forum threads, pending wiki contributions
- Quick links: Manage Users, Manage Wiki, View Contributions, Forum Overview

---

## `/admin/wiki/new` — Create Wiki Article

**File**: `web/src/app/admin/wiki/new/page.tsx`

- Admin only
- Form: title, category dropdown, RichEditor for content, status (Draft/Published), submit
- `POST /api/wiki/articles`

---

## `/admin/wiki/edit/[id]` — Edit Wiki Article

**File**: `web/src/app/admin/wiki/edit/[id]/page.tsx`

- Admin only
- Pre-filled form with existing article data
- Change note input
- `PUT /api/wiki/articles/{id}`
- Show pending contributions for this article with approve/reject buttons

---

## `/admin/users` — User Management

**File**: `web/src/app/admin/users/page.tsx`

- Admin only
- Table: Username, Role, Join Date, Post Count, Banned status, Actions
- Actions: Change role dropdown, Ban/Unban toggle, Reset Password button (opens modal)
- Pagination
- `GET /api/admin/users`

---

## `/changelog` — Changelog

**File**: `web/src/app/changelog/page.tsx`

- Static/placeholder content
- Timeline-style layout with version numbers, dates, and bullet points of changes
- Write 3-4 placeholder entries (e.g. "v0.3.0 — Added multiplayer lobby system")

---

## `/faq` — FAQ

**File**: `web/src/app/faq/page.tsx`

- Accordion-style FAQ items (click to expand/collapse)
- Write 6-8 placeholder Q&As about the game and website

---

## `/contact` — Contact

**File**: `web/src/app/contact/page.tsx`

- Glass card with contact info (placeholder email, Discord link, etc.)
- Optional: simple contact form (name, email, message) — can be non-functional placeholder

---

## Next Step

Proceed to `07-FEATURES.md` for detailed feature specifications.
