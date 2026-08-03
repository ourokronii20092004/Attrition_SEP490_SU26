"use client";

import { createContext, useCallback, useContext, useEffect, useRef, useState } from "react";
import { useAuth } from "./auth-provider";
import { accountApi } from "@/lib/api/account";

export type ThemeMode = "dark" | "light";

export const ACCENTS: { name: string; color: string }[] = [
  { name: "corruption", color: "#38e8a0" },
  { name: "crimson", color: "#ff4365" },
  { name: "ember", color: "#ff7a45" },
  { name: "gold", color: "#e7b549" },
  { name: "azure", color: "#4d9bff" },
  { name: "violet", color: "#a274ff" },
  { name: "rose", color: "#ff5fa8" },
  { name: "cyan", color: "#2fd6e8" },
  { name: "amber", color: "#ffb02e" },
  { name: "sky", color: "#34b3f1" },
  { name: "bone", color: "#d8d2c0" },
];

/**
 * Must match the <html> attributes and the pre-paint script in app/layout.tsx. If they drift, the
 * page paints one theme and then snaps to another.
 */
export const DEFAULT_MODE: ThemeMode = "light";
export const DEFAULT_ACCENT = "ember";

const LS_MODE = "attrition:themeMode";
const LS_ACCENT = "attrition:themeAccent";
/** When the stored choice was made (epoch ms). Lets the newest write win across tabs. */
const LS_AT = "attrition:themeAt";

interface ThemeContextValue {
  mode: ThemeMode;
  accent: string;
  setTheme: (next: { mode?: ThemeMode; accent?: string }) => void;
}

const ThemeContext = createContext<ThemeContextValue | null>(null);

function apply(mode: ThemeMode, accent: string) {
  const root = document.documentElement;
  root.setAttribute("data-theme", mode);
  root.setAttribute("data-accent", accent);
}

function readStored(): { mode: ThemeMode; accent: string; at: number } {
  try {
    const at = Number(localStorage.getItem(LS_AT));
    return {
      mode: (localStorage.getItem(LS_MODE) as ThemeMode) || DEFAULT_MODE,
      accent: localStorage.getItem(LS_ACCENT) || DEFAULT_ACCENT,
      at: Number.isFinite(at) ? at : 0,
    };
  } catch {
    // Private browsing / storage disabled.
    return { mode: DEFAULT_MODE, accent: DEFAULT_ACCENT, at: 0 };
  }
}

export function ThemeProvider({ children }: { children: React.ReactNode }) {
  const { user } = useAuth();
  const [mode, setMode] = useState<ThemeMode>(DEFAULT_MODE);
  const [accent, setAccent] = useState<string>(DEFAULT_ACCENT);

  /**
   * True once the user has picked a theme in this tab. A pick is newer than whatever the server
   * has on file, so the stored theme must not overwrite it — signing in used to replace a
   * just-made choice with the saved one, which is backwards.
   */
  const pickedHere = useRef(false);
  /** Account whose stored theme has already been applied, so re-renders don't re-apply it. */
  const appliedFor = useRef<string | null>(null);

  const write = useCallback((m: ThemeMode, a: string, stamp: boolean) => {
    setMode(m);
    setAccent(a);
    apply(m, a);
    try {
      localStorage.setItem(LS_MODE, m);
      localStorage.setItem(LS_ACCENT, a);
      // Only a deliberate pick advances the clock. Applying a stored theme must not, or it would
      // look newer than a choice another tab just made.
      if (stamp) localStorage.setItem(LS_AT, String(Date.now()));
    } catch {
      // Theme still applies for this page; it just won't persist.
    }
  }, []);

  // Sync React state to what the pre-paint script already put on <html>.
  useEffect(() => {
    const { mode: m, accent: a } = readStored();
    setMode(m);
    setAccent(a);
    apply(m, a);
  }, []);

  // Apply the signed-in user's saved theme: once per account, and never over a pick made here.
  // Signing out and into another account re-runs this because the account id changes.
  useEffect(() => {
    if (!user) {
      // Logged out: forget which account we applied, and let the next sign-in load its own theme.
      appliedFor.current = null;
      pickedHere.current = false;
      return;
    }
    if (appliedFor.current === user.id) return;
    appliedFor.current = user.id;

    if (pickedHere.current) {
      // The user chose a theme, then signed in. Their click is newer than the record, so keep it
      // and save it to this account instead of stomping it.
      accountApi.updateTheme({ themeMode: mode, themeAccent: accent }).catch(() => {});
      return;
    }
    write((user.themeMode as ThemeMode) || DEFAULT_MODE, user.themeAccent || DEFAULT_ACCENT, false);
  }, [user, mode, accent, write]);

  // Cross-tab sync. Two tabs open, change the theme in one: the other follows. The timestamp
  // guards against an out-of-order event applying an older choice over a newer one.
  useEffect(() => {
    const onStorage = (e: StorageEvent) => {
      if (e.key !== LS_MODE && e.key !== LS_ACCENT && e.key !== LS_AT) return;
      const { mode: m, accent: a } = readStored();
      setMode(m);
      setAccent(a);
      apply(m, a);
    };
    window.addEventListener("storage", onStorage);
    return () => window.removeEventListener("storage", onStorage);
  }, []);

  const setTheme = useCallback(
    (next: { mode?: ThemeMode; accent?: string }) => {
      const m = next.mode ?? mode;
      const a = next.accent ?? accent;
      pickedHere.current = true;
      write(m, a, true);
      // Persist for signed-in users; fire-and-forget, the UI already reflects the change.
      if (user) accountApi.updateTheme({ themeMode: m, themeAccent: a }).catch(() => {});
    },
    [mode, accent, user, write],
  );

  return <ThemeContext.Provider value={{ mode, accent, setTheme }}>{children}</ThemeContext.Provider>;
}

export function useTheme() {
  const ctx = useContext(ThemeContext);
  if (!ctx) throw new Error("useTheme must be used within ThemeProvider");
  return ctx;
}
