"use client";

import { useCallback, useEffect, useState } from "react";

const KEY = "attrition:search:recent";
const MAX = 6;

/** Persisted recent search terms (most-recent first), shared via localStorage. */
export function useRecentSearches() {
  const [recent, setRecent] = useState<string[]>([]);

  useEffect(() => {
    try {
      const raw = localStorage.getItem(KEY);
      if (raw) setRecent(JSON.parse(raw));
    } catch { /* ignore */ }
  }, []);

  const persist = useCallback((next: string[]) => {
    setRecent(next);
    try { localStorage.setItem(KEY, JSON.stringify(next)); } catch { /* ignore */ }
  }, []);

  const add = useCallback((term: string) => {
    const t = term.trim();
    if (t.length < 1) return;
    setRecent((prev) => {
      const next = [t, ...prev.filter((x) => x.toLowerCase() !== t.toLowerCase())].slice(0, MAX);
      try { localStorage.setItem(KEY, JSON.stringify(next)); } catch { /* ignore */ }
      return next;
    });
  }, []);

  const remove = useCallback((term: string) => {
    setRecent((prev) => {
      const next = prev.filter((x) => x !== term);
      try { localStorage.setItem(KEY, JSON.stringify(next)); } catch { /* ignore */ }
      return next;
    });
  }, []);

  const clear = useCallback(() => persist([]), [persist]);

  return { recent, add, remove, clear };
}
