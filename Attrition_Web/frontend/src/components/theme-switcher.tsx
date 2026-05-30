"use client";

import { useState } from "react";
import { Palette, Sun, Moon, Check } from "lucide-react";
import { useTheme, ACCENTS } from "@/lib/providers";
import { IconButton } from "@/components/ui/icon-button";

/**
 * Header theme switcher: change mode + accent live from anywhere. Each change is
 * applied to the DOM instantly and saved (server-side when logged in) — no Save button.
 */
export function ThemeSwitcher() {
  const { mode, accent, setTheme } = useTheme();
  const [open, setOpen] = useState(false);

  return (
    <div className="relative">
      <IconButton label="Theme" onClick={() => setOpen((o) => !o)} aria-expanded={open}>
        <Palette size={20} />
      </IconButton>

      {open && (
        <>
          <div className="fixed inset-0 z-[90]" onClick={() => setOpen(false)} aria-hidden />
          <div className="glass absolute right-0 top-full z-[100] mt-2 w-64 origin-top-right animate-fade-in rounded-xl p-4 shadow-[var(--shadow-lg)]">
            <p className="mb-2 text-xs font-medium uppercase tracking-wider text-fg-muted">Mode</p>
            <div className="grid grid-cols-2 gap-2">
              <button
                onClick={() => setTheme({ mode: "dark" })}
                className={`flex items-center justify-center gap-1.5 rounded-lg border px-3 py-2 text-sm font-medium transition-colors ${mode === "dark" ? "border-accent bg-accent-soft text-accent" : "border-border text-fg-muted hover:text-fg"}`}
              >
                <Moon size={15} /> Dark
              </button>
              <button
                onClick={() => setTheme({ mode: "light" })}
                className={`flex items-center justify-center gap-1.5 rounded-lg border px-3 py-2 text-sm font-medium transition-colors ${mode === "light" ? "border-accent bg-accent-soft text-accent" : "border-border text-fg-muted hover:text-fg"}`}
              >
                <Sun size={15} /> Light
              </button>
            </div>

            <p className="mb-2 mt-4 text-xs font-medium uppercase tracking-wider text-fg-muted">Accent</p>
            <div className="grid grid-cols-6 gap-2">
              {ACCENTS.map((a) => (
                <button
                  key={a.name}
                  onClick={() => setTheme({ accent: a.name })}
                  title={a.name}
                  aria-label={a.name}
                  aria-pressed={accent === a.name}
                  style={{ backgroundColor: a.color }}
                  className="flex h-7 w-7 items-center justify-center rounded-full transition-transform duration-150 hover:scale-110"
                >
                  {accent === a.name && <Check size={14} className="text-black/80" strokeWidth={3} />}
                </button>
              ))}
            </div>
          </div>
        </>
      )}
    </div>
  );
}
