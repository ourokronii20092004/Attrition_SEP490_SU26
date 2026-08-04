"use client";

import { useCallback, useRef, useState } from "react";

export type SaveState = "idle" | "saving" | "saved" | "error";

/**
 * Save-on-change for settings, replacing a Save button people forget to press.
 *
 * Each call applies the change optimistically, persists it, and reports status so the UI can say
 * "Saved" instead of leaving the user guessing. A failed save rolls the control back to the value
 * the server still holds, so what's on screen never disagrees with what's stored.
 *
 * Overlapping saves are sequenced by generation, not cancelled: settings writes are tiny and a
 * dropped one would silently lose a change. Only the newest response is allowed to set the visible
 * state, so a slow earlier reply can't overwrite a faster later one.
 */
export function useAutoSave<T>({ onSave, onError }: {
  onSave: (patch: T) => Promise<void>;
  onError?: (message: string) => void;
}) {
  const [state, setState] = useState<SaveState>("idle");
  const generation = useRef(0);
  const savedTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  const save = useCallback(async (patch: T, rollback?: () => void) => {
    const mine = ++generation.current;
    if (savedTimer.current) clearTimeout(savedTimer.current);
    setState("saving");
    try {
      await onSave(patch);
      if (generation.current !== mine) return; // a newer save owns the UI now
      setState("saved");
      // Let "Saved" linger long enough to be read, then fall quiet.
      savedTimer.current = setTimeout(() => setState("idle"), 2000);
    } catch {
      if (generation.current !== mine) return;
      setState("error");
      // Put the control back to the stored value so the screen can't lie about what's saved.
      rollback?.();
      onError?.("Couldn't save that change. Please try again.");
    }
  }, [onSave, onError]);

  return { save, state };
}
