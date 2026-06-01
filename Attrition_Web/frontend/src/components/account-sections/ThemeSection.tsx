"use client";

import { Sun, Moon } from "lucide-react";
import { useTheme, ACCENTS } from "@/lib/providers";
import { SettingsCard } from "./SettingsCard";

export function ThemeSection() {
  const { mode, accent, setTheme } = useTheme();

  return (
    <SettingsCard title="Theme">
      <p className="-mt-2 mb-4 text-sm text-fg-muted">Changes apply and save instantly. You can also switch from the palette icon in the header.</p>
      <div className="space-y-5">
        <div>
          <p className="mb-2 text-sm font-medium text-fg-muted">Mode</p>
          <div className="grid max-w-xs grid-cols-2 gap-2">
            <button
              onClick={() => setTheme({ mode: "dark" })}
              className={`flex items-center justify-center gap-2 rounded-lg border px-4 py-2.5 text-sm font-medium transition-colors ${mode === "dark" ? "border-accent bg-accent-soft text-accent" : "border-border text-fg-muted hover:text-fg"}`}
            >
              <Moon size={16} /> Dark
            </button>
            <button
              onClick={() => setTheme({ mode: "light" })}
              className={`flex items-center justify-center gap-2 rounded-lg border px-4 py-2.5 text-sm font-medium transition-colors ${mode === "light" ? "border-accent bg-accent-soft text-accent" : "border-border text-fg-muted hover:text-fg"}`}
            >
              <Sun size={16} /> Light
            </button>
          </div>
        </div>
        <div>
          <p className="mb-2 text-sm font-medium text-fg-muted">Accent color</p>
          <div className="flex flex-wrap gap-2.5">
            {ACCENTS.map((a) => (
              <button
                key={a.name}
                onClick={() => setTheme({ accent: a.name })}
                title={a.name}
                aria-label={a.name}
                aria-pressed={accent === a.name}
                style={{ backgroundColor: a.color }}
                className={`h-9 w-9 rounded-full transition-transform duration-150 hover:scale-110 ${accent === a.name ? "ring-2 ring-fg ring-offset-2 ring-offset-surface" : ""}`}
              />
            ))}
          </div>
        </div>
      </div>
    </SettingsCard>
  );
}
