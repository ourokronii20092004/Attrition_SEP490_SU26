"use client";

import React, { createContext, useContext, useEffect, useState, useCallback } from "react";
import { generateAccentVariants } from "@/lib/utils";

// ─── Accent Colors ─────────────────────────────────────────

export const ACCENT_COLORS = {
  ember: { label: "Ember", hex: "#e85d3a", description: "Warm, fiery" },
  soul: { label: "Soul", hex: "#6366f1", description: "Mystical, arcane" },
  gold: { label: "Gold", hex: "#d4a053", description: "Noble, classic" },
  verdant: { label: "Verdant", hex: "#10b981", description: "Natural, healing" },
  crimson: { label: "Crimson", hex: "#dc2626", description: "Bold, combat" },
  frost: { label: "Frost", hex: "#06b6d4", description: "Cool, serene" },
} as const;

export type AccentName = keyof typeof ACCENT_COLORS;
export type ThemeMode = "light" | "dark" | "system";

// ─── Storage Keys ──────────────────────────────────────────

const THEME_MODE_KEY = "attrition-theme-mode";
const ACCENT_KEY = "attrition-accent";

function getCookie(name: string): string | null {
  if (typeof window === "undefined") return null;
  const match = document.cookie.match(new RegExp('(^| )' + name + '=([^;]+)'));
  return match ? match[2] : null;
}

function setCookie(name: string, value: string) {
  if (typeof window === "undefined") return;
  const domain = window.location.hostname.includes("hault.io.vn") ? "domain=.hault.io.vn;" : "";
  document.cookie = `${name}=${value}; ${domain} path=/; max-age=31536000; SameSite=Lax`;
}

function removeCookie(name: string) {
  if (typeof window === "undefined") return;
  const domain = window.location.hostname.includes("hault.io.vn") ? "domain=.hault.io.vn;" : "";
  document.cookie = `${name}=; ${domain} path=/; max-age=0; SameSite=Lax`;
}

// ─── Context ───────────────────────────────────────────────

interface ThemeContextValue {
  mode: ThemeMode;
  accent: AccentName;
  resolvedMode: "light" | "dark";
  setMode: (mode: ThemeMode) => void;
  setAccent: (accent: AccentName) => void;
  resetTheme: () => void;
}

const ThemeContext = createContext<ThemeContextValue | undefined>(undefined);

// ─── Apply Theme (runs before React hydrates too) ──────────

function getSystemPreference(): "light" | "dark" {
  if (typeof window === "undefined") return "light";
  return window.matchMedia("(prefers-color-scheme: dark)").matches
    ? "dark"
    : "light";
}

function resolveMode(mode: ThemeMode): "light" | "dark" {
  if (mode === "system") return getSystemPreference();
  return mode;
}

function applyTheme(resolved: "light" | "dark") {
  document.documentElement.setAttribute("data-theme", resolved);
  // Update meta theme-color for mobile browsers
  const meta = document.querySelector('meta[name="theme-color"]');
  if (meta) {
    meta.setAttribute("content", resolved === "dark" ? "#09090b" : "#f7f7f8");
  }
}

function applyAccent(accentName: AccentName) {
  const color = ACCENT_COLORS[accentName];
  if (!color) return;
  const variants = generateAccentVariants(color.hex);
  const root = document.documentElement;
  root.style.setProperty("--accent", variants.base);
  root.style.setProperty("--accent-hover", variants.hover);
  root.style.setProperty("--accent-active", variants.active);
  root.style.setProperty("--accent-subtle", variants.subtle);
  root.style.setProperty("--accent-subtle-hover", variants.subtleHover);
  root.style.setProperty("--accent-contrast", variants.contrast);
}

// ─── Provider ──────────────────────────────────────────────

export function ThemeProvider({ children }: { children: React.ReactNode }) {
  const [mode, setModeState] = useState<ThemeMode>("system");
  const [accent, setAccentState] = useState<AccentName>("ember");
  const [resolvedMode, setResolvedMode] = useState<"light" | "dark">("light");

  // Initialize from storage/cookies on mount
  useEffect(() => {
    const savedMode = getCookie(THEME_MODE_KEY) || localStorage.getItem(THEME_MODE_KEY) as ThemeMode | null;
    const savedAccent = getCookie(ACCENT_KEY) || localStorage.getItem(ACCENT_KEY) as AccentName | null;

    const initialMode = (savedMode as ThemeMode) || "system";
    const initialAccent = savedAccent && ACCENT_COLORS[savedAccent as AccentName] ? (savedAccent as AccentName) : "ember";

    setModeState(initialMode);
    setAccentState(initialAccent);

    const resolved = resolveMode(initialMode);
    setResolvedMode(resolved);
    applyTheme(resolved);
    applyAccent(initialAccent);
  }, []);

  // Listen for system preference changes
  useEffect(() => {
    const mql = window.matchMedia("(prefers-color-scheme: dark)");
    const handler = () => {
      if (mode === "system") {
        const resolved = getSystemPreference();
        setResolvedMode(resolved);
        applyTheme(resolved);
      }
    };
    mql.addEventListener("change", handler);
    return () => mql.removeEventListener("change", handler);
  }, [mode]);

  // Poll for cookie changes if cross-domain, or use storage event
  useEffect(() => {
    const handler = (e: StorageEvent) => {
      if (e.key === THEME_MODE_KEY && e.newValue) {
        const newMode = e.newValue as ThemeMode;
        setModeState(newMode);
        const resolved = resolveMode(newMode);
        setResolvedMode(resolved);
        applyTheme(resolved);
      }
      if (e.key === ACCENT_KEY && e.newValue) {
        const newAccent = e.newValue as AccentName;
        if (ACCENT_COLORS[newAccent]) {
          setAccentState(newAccent);
          applyAccent(newAccent);
        }
      }
    };
    window.addEventListener("storage", handler);
    
    // Cookie polling for cross-subdomain syncing where storage events don't fire
    const interval = setInterval(() => {
      const currentMode = getCookie(THEME_MODE_KEY) as ThemeMode | null;
      const currentAccent = getCookie(ACCENT_KEY) as AccentName | null;
      
      setModeState(prev => {
        if (currentMode && currentMode !== prev) {
          const resolved = resolveMode(currentMode);
          setResolvedMode(resolved);
          applyTheme(resolved);
          return currentMode;
        }
        return prev;
      });

      setAccentState(prev => {
        if (currentAccent && currentAccent !== prev && ACCENT_COLORS[currentAccent]) {
          applyAccent(currentAccent);
          return currentAccent;
        }
        return prev;
      });
    }, 2000);

    return () => {
      window.removeEventListener("storage", handler);
      clearInterval(interval);
    };
  }, []);

  const setMode = useCallback((newMode: ThemeMode) => {
    setModeState(newMode);
    localStorage.setItem(THEME_MODE_KEY, newMode);
    setCookie(THEME_MODE_KEY, newMode);
    const resolved = resolveMode(newMode);
    setResolvedMode(resolved);
    applyTheme(resolved);
  }, []);

  const setAccent = useCallback((newAccent: AccentName) => {
    if (!ACCENT_COLORS[newAccent]) return;
    setAccentState(newAccent);
    localStorage.setItem(ACCENT_KEY, newAccent);
    setCookie(ACCENT_KEY, newAccent);
    applyAccent(newAccent);
  }, []);

  const resetTheme = useCallback(() => {
    // Clear stored preferences
    localStorage.removeItem(THEME_MODE_KEY);
    localStorage.removeItem(ACCENT_KEY);
    removeCookie(THEME_MODE_KEY);
    removeCookie(ACCENT_KEY);

    // Reset to defaults
    const defaultMode: ThemeMode = "system";
    const defaultAccent: AccentName = "ember";
    setModeState(defaultMode);
    setAccentState(defaultAccent);
    const resolved = resolveMode(defaultMode);
    setResolvedMode(resolved);
    applyTheme(resolved);
    applyAccent(defaultAccent);

    // Clear inline accent CSS vars
    const root = document.documentElement;
    root.style.removeProperty("--accent");
    root.style.removeProperty("--accent-hover");
    root.style.removeProperty("--accent-active");
    root.style.removeProperty("--accent-subtle");
    root.style.removeProperty("--accent-subtle-hover");
    root.style.removeProperty("--accent-contrast");
  }, []);

  return (
    <ThemeContext.Provider value={{ mode, accent, resolvedMode, setMode, setAccent, resetTheme }}>
      {children}
    </ThemeContext.Provider>
  );
}

// ─── Hook ──────────────────────────────────────────────────

export function useTheme() {
  const ctx = useContext(ThemeContext);
  if (!ctx) throw new Error("useTheme must be used within ThemeProvider");
  return ctx;
}
