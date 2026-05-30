"use client";

import { createContext, useCallback, useContext, useEffect, useState } from "react";
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

const DEFAULT_MODE: ThemeMode = "dark";
const DEFAULT_ACCENT = "corruption";
const LS_MODE = "attrition:themeMode";
const LS_ACCENT = "attrition:themeAccent";

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

export function ThemeProvider({ children }: { children: React.ReactNode }) {
  const { user } = useAuth();
  const [mode, setMode] = useState<ThemeMode>(DEFAULT_MODE);
  const [accent, setAccent] = useState<string>(DEFAULT_ACCENT);

  // On mount, restore from localStorage so anonymous visitors keep their choice
  // and there's no flash before the user object loads.
  useEffect(() => {
    const m = (localStorage.getItem(LS_MODE) as ThemeMode) || DEFAULT_MODE;
    const a = localStorage.getItem(LS_ACCENT) || DEFAULT_ACCENT;
    setMode(m);
    setAccent(a);
    apply(m, a);
  }, []);

  // When the logged-in user's stored theme arrives, it wins over the local guess.
  useEffect(() => {
    if (!user) return;
    const m = (user.themeMode as ThemeMode) || DEFAULT_MODE;
    const a = user.themeAccent || DEFAULT_ACCENT;
    setMode(m);
    setAccent(a);
    apply(m, a);
    localStorage.setItem(LS_MODE, m);
    localStorage.setItem(LS_ACCENT, a);
  }, [user?.themeMode, user?.themeAccent, user]);

  const setTheme = useCallback(
    (next: { mode?: ThemeMode; accent?: string }) => {
      const m = next.mode ?? mode;
      const a = next.accent ?? accent;
      setMode(m);
      setAccent(a);
      apply(m, a);
      localStorage.setItem(LS_MODE, m);
      localStorage.setItem(LS_ACCENT, a);
      // Persist server-side for logged-in users; fire-and-forget (local state already applied).
      if (user) accountApi.updateTheme({ themeMode: m, themeAccent: a }).catch(() => {});
    },
    [mode, accent, user],
  );

  return <ThemeContext.Provider value={{ mode, accent, setTheme }}>{children}</ThemeContext.Provider>;
}

export function useTheme() {
  const ctx = useContext(ThemeContext);
  if (!ctx) throw new Error("useTheme must be used within ThemeProvider");
  return ctx;
}
