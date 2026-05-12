# 05 — Frontend Core

Root layout, providers, shared components, and the music player.

---

## Root Layout (`web/src/app/layout.tsx`)

```tsx
import './globals.css';
import { Inter, JetBrains_Mono } from 'next/font/google';
import { ThemeProvider } from '@/contexts/ThemeContext';
import { AuthProvider } from '@/contexts/AuthContext';
import Navbar from '@/components/Navbar';
import Footer from '@/components/Footer';
import MusicPlayer from '@/components/MusicPlayer';
import ToastContainer from '@/components/ToastContainer';

const inter = Inter({ subsets: ['latin'], variable: '--font-inter' });
const jetbrains = JetBrains_Mono({ subsets: ['latin'], variable: '--font-mono' });

export const metadata = {
  title: 'Attrition — 2D Roguelike Multiplayer',
  description: 'The official community hub for Attrition. Explore the wiki, join the forum, and connect with other players.',
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" suppressHydrationWarning>
      <body className={`${inter.variable} ${jetbrains.variable}`}>
        <ThemeProvider>
          <AuthProvider>
            <Navbar />
            <main className="page-content">{children}</main>
            <Footer />
            <MusicPlayer />
            <ToastContainer />
          </AuthProvider>
        </ThemeProvider>
      </body>
    </html>
  );
}
```

---

## Theme Context (`web/src/contexts/ThemeContext.tsx`)

```tsx
'use client';
import { createContext, useContext, useEffect, useState, ReactNode } from 'react';

type Theme = 'light' | 'dark';

interface ThemeContextType {
  theme: Theme;
  toggleTheme: () => void;
}

const ThemeContext = createContext<ThemeContextType | undefined>(undefined);

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [theme, setTheme] = useState<Theme>('light');

  useEffect(() => {
    const saved = localStorage.getItem('attrition-theme') as Theme;
    if (saved) {
      setTheme(saved);
      document.documentElement.setAttribute('data-theme', saved);
    }
  }, []);

  const toggleTheme = () => {
    const next = theme === 'light' ? 'dark' : 'light';
    setTheme(next);
    document.documentElement.setAttribute('data-theme', next);
    localStorage.setItem('attrition-theme', next);
  };

  return <ThemeContext.Provider value={{ theme, toggleTheme }}>{children}</ThemeContext.Provider>;
}

export const useTheme = () => {
  const ctx = useContext(ThemeContext);
  if (!ctx) throw new Error('useTheme must be used within ThemeProvider');
  return ctx;
};
```

---

## Auth Context (`web/src/contexts/AuthContext.tsx`)

```tsx
'use client';
import { createContext, useContext, useEffect, useState, ReactNode } from 'react';
import { api } from '@/lib/api';

interface User {
  id: string;
  username: string;
  role: string;
  avatarUrl: string | null;
  bio: string | null;
  mustChangePassword: boolean;
}

interface AuthContextType {
  user: User | null;
  loading: boolean;
  login: (username: string, password: string) => Promise<{ success: boolean; error?: string }>;
  register: (username: string, password: string) => Promise<{ success: boolean; error?: string }>;
  logout: () => void;
  refreshUser: () => Promise<void>;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const token = localStorage.getItem('attrition-token');
    if (token) {
      refreshUser().finally(() => setLoading(false));
    } else {
      setLoading(false);
    }
  }, []);

  async function login(username: string, password: string) {
    const res = await api.post('/api/auth/login', { username, password });
    if (res.success) {
      localStorage.setItem('attrition-token', res.data.accessToken);
      localStorage.setItem('attrition-refresh', res.data.refreshToken);
      setUser(res.data.user);
      return { success: true };
    }
    return { success: false, error: res.error };
  }

  async function register(username: string, password: string) {
    const res = await api.post('/api/auth/register', { username, password });
    if (res.success) {
      localStorage.setItem('attrition-token', res.data.accessToken);
      localStorage.setItem('attrition-refresh', res.data.refreshToken);
      setUser(res.data.user);
      return { success: true };
    }
    return { success: false, error: res.error };
  }

  function logout() {
    localStorage.removeItem('attrition-token');
    localStorage.removeItem('attrition-refresh');
    setUser(null);
  }

  async function refreshUser() {
    const res = await api.get('/api/auth/me');
    if (res.success) setUser(res.data);
    else logout();
  }

  return (
    <AuthContext.Provider value={{ user, loading, login, register, logout, refreshUser }}>
      {children}
    </AuthContext.Provider>
  );
}

export const useAuth = () => {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
};
```

---

## API Client (`web/src/lib/api.ts`)

```typescript
const BASE_URL = process.env.NEXT_PUBLIC_API_URL || '';

async function request(method: string, path: string, body?: any) {
  const token = typeof window !== 'undefined' ? localStorage.getItem('attrition-token') : null;

  const headers: Record<string, string> = { 'Content-Type': 'application/json' };
  if (token) headers['Authorization'] = `Bearer ${token}`;

  const res = await fetch(`${BASE_URL}${path}`, {
    method,
    headers,
    body: body ? JSON.stringify(body) : undefined,
  });

  // Handle 401 — try refresh
  if (res.status === 401 && typeof window !== 'undefined') {
    const refreshToken = localStorage.getItem('attrition-refresh');
    if (refreshToken) {
      const refreshRes = await fetch(`${BASE_URL}/api/auth/refresh`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken }),
      });
      if (refreshRes.ok) {
        const data = await refreshRes.json();
        localStorage.setItem('attrition-token', data.data.accessToken);
        localStorage.setItem('attrition-refresh', data.data.refreshToken);
        // Retry original request
        headers['Authorization'] = `Bearer ${data.data.accessToken}`;
        const retry = await fetch(`${BASE_URL}${path}`, { method, headers, body: body ? JSON.stringify(body) : undefined });
        return retry.json();
      }
    }
  }

  return res.json();
}

async function uploadFile(path: string, file: File, fieldName = 'file') {
  const token = typeof window !== 'undefined' ? localStorage.getItem('attrition-token') : null;
  const formData = new FormData();
  formData.append(fieldName, file);

  const headers: Record<string, string> = {};
  if (token) headers['Authorization'] = `Bearer ${token}`;

  const res = await fetch(`${BASE_URL}${path}`, { method: 'POST', headers, body: formData });
  return res.json();
}

export const api = {
  get: (path: string) => request('GET', path),
  post: (path: string, body?: any) => request('POST', path, body),
  put: (path: string, body?: any) => request('PUT', path, body),
  delete: (path: string) => request('DELETE', path),
  upload: uploadFile,
};
```

---

## Music Player (`web/src/components/MusicPlayer.tsx`)

This is the **most critical UI component** — it must live in the root layout so it never unmounts.

```tsx
'use client';
import { useEffect, useRef, useState } from 'react';
import { FiVolume2, FiVolumeX } from 'react-icons/fi';

export default function MusicPlayer() {
  const audioRef = useRef<HTMLAudioElement>(null);
  const [isPlaying, setIsPlaying] = useState(true);
  const [hasInteracted, setHasInteracted] = useState(false);

  useEffect(() => {
    // Load saved preference
    const saved = localStorage.getItem('attrition-music');
    if (saved === 'off') setIsPlaying(false);

    // Autoplay requires user interaction on most browsers.
    // Start playing on first user interaction with the page.
    const handleInteraction = () => {
      if (!hasInteracted) {
        setHasInteracted(true);
        const audio = audioRef.current;
        if (audio && isPlaying) {
          audio.volume = 0.3;
          audio.play().catch(() => {});
        }
        document.removeEventListener('click', handleInteraction);
        document.removeEventListener('keydown', handleInteraction);
      }
    };

    document.addEventListener('click', handleInteraction);
    document.addEventListener('keydown', handleInteraction);

    return () => {
      document.removeEventListener('click', handleInteraction);
      document.removeEventListener('keydown', handleInteraction);
    };
  }, []);

  useEffect(() => {
    const audio = audioRef.current;
    if (!audio) return;
    audio.volume = 0.3;
    if (isPlaying && hasInteracted) {
      audio.play().catch(() => {});
    } else {
      audio.pause();
    }
  }, [isPlaying, hasInteracted]);

  const toggle = () => {
    const next = !isPlaying;
    setIsPlaying(next);
    setHasInteracted(true);
    localStorage.setItem('attrition-music', next ? 'on' : 'off');
  };

  return (
    <>
      <audio ref={audioRef} src="/audio/friday-night.mp3" loop preload="auto" />
      <button
        onClick={toggle}
        className="music-toggle"
        aria-label={isPlaying ? 'Mute music' : 'Unmute music'}
        title={isPlaying ? 'Mute music' : 'Unmute music'}
      >
        {isPlaying ? <FiVolume2 size={20} /> : <FiVolumeX size={20} />}
      </button>
    </>
  );
}
```

Add to `globals.css`:
```css
.music-toggle {
  position: fixed; bottom: 24px; right: 24px; z-index: 900;
  width: 48px; height: 48px; border-radius: 50%;
  background: var(--glass-bg); backdrop-filter: blur(12px);
  border: 1px solid var(--glass-border); color: var(--text-primary);
  cursor: pointer; display: flex; align-items: center; justify-content: center;
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.1);
  transition: all var(--transition-fast);
}
.music-toggle:hover { transform: scale(1.1); background: var(--accent); color: #fff; }
```

---

## Navbar (`web/src/components/Navbar.tsx`)

Glassmorphism sticky navbar with links, auth buttons, theme toggle, responsive hamburger.

Key elements:
- Logo/name "Attrition" on the left
- Nav links: Home, Wiki, Forum, About
- Right side: ThemeToggle button, Login/Register (or username + logout if authed)
- Admin link visible only to admin role
- Mobile: hamburger icon → slide-out menu
- CSS: `position: fixed; top: 0; width: 100%; background: var(--glass-bg); backdrop-filter: blur(var(--glass-blur)); border-bottom: 1px solid var(--glass-border); z-index: 1000; height: var(--nav-height);`

---

## Footer (`web/src/components/Footer.tsx`)

Simple footer with:
- "© 2025 Attrition. All rights reserved."
- Links: Home, Wiki, Forum, About, FAQ, Contact, Changelog
- CSS: subtle border-top, muted text color

---

## Shared Components Summary

Create each in `web/src/components/`. Use the CSS classes from `04-DESIGN-SYSTEM.md`.

| Component | Purpose |
|---|---|
| `GlassCard.tsx` | Wrapper `<div className="glass-card">`. Accept `className` and `children` props. |
| `Button.tsx` | `<button className="btn btn-{variant}">`. Props: `variant`, `size`, `onClick`, `children`, `disabled`, `type`. |
| `Input.tsx` | `<div className="input-group">` with label + input. Props: `label`, `type`, `value`, `onChange`, `error`, `placeholder`. |
| `Modal.tsx` | Portal-based modal with overlay. Props: `isOpen`, `onClose`, `title`, `children`. |
| `Avatar.tsx` | `<img className="avatar">` with fallback to initials. Props: `src`, `username`, `size`. |
| `MarkdownRenderer.tsx` | Uses `react-markdown` with `remark-gfm` and `rehype-sanitize`. Styled with `font-mono` for code blocks. |
| `RichEditor.tsx` | Wrapper around `@uiw/react-md-editor` for wiki/forum content editing. |
| `Pagination.tsx` | Page buttons. Props: `currentPage`, `totalPages`, `onPageChange`. |
| `SearchBar.tsx` | Input with debounced onChange (300ms). Props: `value`, `onChange`, `placeholder`. |
| `ToastContainer.tsx` | Fixed container for toast notifications. Use a simple context/state manager. |
| `Badge.tsx` | `<span className="badge badge-{variant}">`. Props: `variant`, `children`. |
| `Breadcrumb.tsx` | `<nav className="breadcrumb">` with links separated by `/`. Props: `items: {label, href}[]`. |

---

## Next Step

Proceed to `06-FRONTEND-PAGES.md` for all page implementations.
