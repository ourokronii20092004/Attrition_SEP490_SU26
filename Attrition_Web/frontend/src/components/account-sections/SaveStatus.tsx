"use client";

import { Check, Loader2, TriangleAlert } from "lucide-react";
import type { SaveState } from "@/lib/hooks/use-auto-save";

/**
 * Inline status for a save-on-change section, replacing the reassurance a Save button used to give.
 *
 * Renders nothing when idle so the settings page stays quiet, and announces politely so a screen
 * reader hears the outcome without being interrupted mid-sentence.
 */
export function SaveStatus({ state, className }: { state: SaveState; className?: string }) {
  return (
    <span
      aria-live="polite"
      className={`inline-flex items-center gap-1.5 text-xs ${className ?? ""}`}
    >
      {state === "saving" && (
        <>
          <Loader2 size={13} className="animate-spin text-fg-subtle" aria-hidden />
          <span className="text-fg-subtle">Saving…</span>
        </>
      )}
      {state === "saved" && (
        <>
          <Check size={13} className="text-success" aria-hidden />
          <span className="text-success">Saved</span>
        </>
      )}
      {state === "error" && (
        <>
          <TriangleAlert size={13} className="text-danger" aria-hidden />
          <span className="text-danger">Not saved</span>
        </>
      )}
    </span>
  );
}
