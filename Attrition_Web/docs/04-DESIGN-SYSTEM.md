# 04 — Design System (CSS)

Glassmorphism design with blue/navy palette. Light and dark mode via `data-theme` attribute on `<html>`.

---

## `globals.css` — Full Design System

Place in `web/src/app/globals.css`. This is the single source of truth for all visual tokens.

```css
@import url('https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700;800&family=JetBrains+Mono:wght@400;500&display=swap');

/* ========== CSS CUSTOM PROPERTIES ========== */
:root {
  /* Colors — Light Mode */
  --bg-primary: #f0f4f8;
  --bg-secondary: #ffffff;
  --bg-tertiary: #e2e8f0;
  --glass-bg: rgba(255, 255, 255, 0.25);
  --glass-border: rgba(255, 255, 255, 0.35);
  --glass-shadow: 0 8px 32px rgba(0, 0, 0, 0.08);
  --accent: #2563eb;
  --accent-hover: #1d4ed8;
  --accent-light: #dbeafe;
  --accent-secondary: #1e3a5f;
  --text-primary: #1e293b;
  --text-secondary: #64748b;
  --text-muted: #94a3b8;
  --border: rgba(0, 0, 0, 0.08);
  --danger: #dc2626;
  --danger-hover: #b91c1c;
  --success: #16a34a;
  --warning: #d97706;

  /* Glassmorphism */
  --glass-blur: 16px;
  --glass-radius: 16px;
  --glass-radius-sm: 10px;
  --glass-radius-lg: 24px;

  /* Typography */
  --font-sans: 'Inter', -apple-system, BlinkMacSystemFont, sans-serif;
  --font-mono: 'JetBrains Mono', monospace;

  /* Spacing */
  --space-xs: 4px;
  --space-sm: 8px;
  --space-md: 16px;
  --space-lg: 24px;
  --space-xl: 32px;
  --space-2xl: 48px;
  --space-3xl: 64px;

  /* Transitions */
  --transition-fast: 150ms ease;
  --transition-base: 250ms ease;
  --transition-slow: 400ms ease;

  /* Layout */
  --max-width: 1200px;
  --nav-height: 64px;
}

/* ========== DARK MODE ========== */
[data-theme="dark"] {
  --bg-primary: #0a0f1a;
  --bg-secondary: #111827;
  --bg-tertiary: #1e293b;
  --glass-bg: rgba(17, 24, 39, 0.6);
  --glass-border: rgba(255, 255, 255, 0.08);
  --glass-shadow: 0 8px 32px rgba(0, 0, 0, 0.3);
  --accent: #3b82f6;
  --accent-hover: #60a5fa;
  --accent-light: rgba(59, 130, 246, 0.15);
  --accent-secondary: #1e40af;
  --text-primary: #f1f5f9;
  --text-secondary: #94a3b8;
  --text-muted: #64748b;
  --border: rgba(255, 255, 255, 0.06);
  --danger: #ef4444;
  --success: #22c55e;
  --warning: #f59e0b;
}

/* ========== RESET & BASE ========== */
*, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }

html { scroll-behavior: smooth; }

body {
  font-family: var(--font-sans);
  background: var(--bg-primary);
  color: var(--text-primary);
  line-height: 1.6;
  min-height: 100vh;
  transition: background var(--transition-base), color var(--transition-base);
}

a { color: var(--accent); text-decoration: none; transition: color var(--transition-fast); }
a:hover { color: var(--accent-hover); }

/* ========== GLASSMORPHISM CARD ========== */
.glass-card {
  background: var(--glass-bg);
  backdrop-filter: blur(var(--glass-blur));
  -webkit-backdrop-filter: blur(var(--glass-blur));
  border: 1px solid var(--glass-border);
  border-radius: var(--glass-radius);
  box-shadow: var(--glass-shadow);
  padding: var(--space-lg);
  transition: transform var(--transition-fast), box-shadow var(--transition-fast);
}

.glass-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 12px 40px rgba(0, 0, 0, 0.12);
}

[data-theme="dark"] .glass-card:hover {
  box-shadow: 0 12px 40px rgba(0, 0, 0, 0.4);
}

/* ========== BUTTONS ========== */
.btn {
  display: inline-flex; align-items: center; justify-content: center; gap: var(--space-sm);
  padding: 10px 20px; border-radius: var(--glass-radius-sm); border: none;
  font-family: var(--font-sans); font-weight: 500; font-size: 14px;
  cursor: pointer; transition: all var(--transition-fast);
  text-decoration: none;
}

.btn-primary { background: var(--accent); color: #fff; }
.btn-primary:hover { background: var(--accent-hover); transform: translateY(-1px); }

.btn-secondary { background: var(--glass-bg); color: var(--text-primary); border: 1px solid var(--glass-border); backdrop-filter: blur(8px); }
.btn-secondary:hover { background: var(--accent-light); }

.btn-danger { background: var(--danger); color: #fff; }
.btn-danger:hover { background: var(--danger-hover); }

.btn-ghost { background: transparent; color: var(--text-secondary); }
.btn-ghost:hover { background: var(--accent-light); color: var(--accent); }

.btn-sm { padding: 6px 12px; font-size: 12px; }
.btn-lg { padding: 14px 28px; font-size: 16px; }

/* ========== INPUTS ========== */
.input-group { display: flex; flex-direction: column; gap: var(--space-xs); }
.input-group label { font-size: 14px; font-weight: 500; color: var(--text-secondary); }

.input {
  padding: 10px 14px; border-radius: var(--glass-radius-sm);
  border: 1px solid var(--border); background: var(--glass-bg);
  backdrop-filter: blur(8px); color: var(--text-primary);
  font-family: var(--font-sans); font-size: 14px;
  transition: border-color var(--transition-fast), box-shadow var(--transition-fast);
}

.input:focus { outline: none; border-color: var(--accent); box-shadow: 0 0 0 3px var(--accent-light); }
.input-error { border-color: var(--danger); }

textarea.input { min-height: 120px; resize: vertical; }

/* ========== BADGES ========== */
.badge {
  display: inline-flex; align-items: center; padding: 2px 10px;
  border-radius: 100px; font-size: 12px; font-weight: 500;
}
.badge-admin { background: var(--accent-light); color: var(--accent); }
.badge-user { background: var(--bg-tertiary); color: var(--text-secondary); }
.badge-pinned { background: rgba(234, 179, 8, 0.15); color: var(--warning); }
.badge-locked { background: rgba(239, 68, 68, 0.15); color: var(--danger); }

/* ========== AVATAR ========== */
.avatar {
  width: 40px; height: 40px; border-radius: 50%;
  object-fit: cover; border: 2px solid var(--glass-border);
}
.avatar-sm { width: 28px; height: 28px; }
.avatar-lg { width: 64px; height: 64px; }
.avatar-xl { width: 96px; height: 96px; }

/* ========== LAYOUT UTILITIES ========== */
.container { max-width: var(--max-width); margin: 0 auto; padding: 0 var(--space-lg); }
.page-content { padding-top: calc(var(--nav-height) + var(--space-xl)); padding-bottom: var(--space-3xl); }

/* ========== MODAL ========== */
.modal-overlay {
  position: fixed; inset: 0; background: rgba(0, 0, 0, 0.5);
  backdrop-filter: blur(4px); display: flex; align-items: center;
  justify-content: center; z-index: 1000;
}
.modal-content {
  background: var(--bg-secondary); border-radius: var(--glass-radius-lg);
  border: 1px solid var(--glass-border); padding: var(--space-xl);
  max-width: 500px; width: 90%; max-height: 80vh; overflow-y: auto;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.2);
}

/* ========== TOAST ========== */
.toast-container { position: fixed; bottom: var(--space-lg); right: var(--space-lg); z-index: 1100; display: flex; flex-direction: column; gap: var(--space-sm); }
.toast { padding: 12px 20px; border-radius: var(--glass-radius-sm); font-size: 14px; color: #fff; animation: slideIn 0.3s ease; }
.toast-success { background: var(--success); }
.toast-error { background: var(--danger); }
.toast-info { background: var(--accent); }

@keyframes slideIn { from { transform: translateX(100%); opacity: 0; } to { transform: translateX(0); opacity: 1; } }

/* ========== PAGINATION ========== */
.pagination { display: flex; gap: var(--space-xs); align-items: center; justify-content: center; }
.pagination button { min-width: 36px; height: 36px; border-radius: var(--glass-radius-sm); border: 1px solid var(--border); background: var(--glass-bg); color: var(--text-primary); cursor: pointer; }
.pagination button.active { background: var(--accent); color: #fff; border-color: var(--accent); }

/* ========== BREADCRUMB ========== */
.breadcrumb { display: flex; align-items: center; gap: var(--space-sm); font-size: 14px; color: var(--text-muted); margin-bottom: var(--space-lg); }
.breadcrumb a { color: var(--text-secondary); }
.breadcrumb a:hover { color: var(--accent); }
.breadcrumb span.sep { color: var(--text-muted); }

/* ========== RESPONSIVE ========== */
@media (max-width: 768px) {
  :root { --space-lg: 16px; --space-xl: 24px; --space-2xl: 32px; }
  .glass-card { padding: var(--space-md); }
}
```

---

## Usage Notes

- Apply `.glass-card` to any container that should have the frosted-glass effect.
- Pair glass cards with a subtle gradient background on the parent container for best effect (the body or section backgrounds).
- The navbar should also use glassmorphism: `background: var(--glass-bg); backdrop-filter: blur(var(--glass-blur));`
- Use `var(--transition-base)` on all theme-dependent properties for smooth light/dark transitions.
- All components should use these CSS tokens, NOT ad-hoc hardcoded values.

---

## Next Step

Proceed to `05-FRONTEND-CORE.md` for layout, providers, and shared components.
