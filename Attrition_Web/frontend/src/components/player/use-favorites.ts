"use client";

import { useCallback, useEffect, useState } from "react";
import { musicApi } from "@/lib/api/music";
import { useAuth } from "@/lib/providers";

/**
 * Tracks which tracks the current user has favorited and exposes a toggle.
 * No-ops for logged-out users. Shared by the player bar and expanded view.
 */
export function useFavorites() {
  const { user } = useAuth();
  const [ids, setIds] = useState<Set<number>>(new Set());

  useEffect(() => {
    if (!user) { setIds(new Set()); return; }
    let ignore = false;
    musicApi.getFavoriteIds().then((r) => {
      if (!ignore && r.success) setIds(new Set(r.data));
    }).catch(() => {});
    return () => { ignore = true; };
  }, [user]);

  const isFavorite = useCallback((trackId: number) => ids.has(trackId), [ids]);

  const toggle = useCallback(async (trackId: number) => {
    if (!user) return;
    // Optimistic flip, reconcile with the server response.
    setIds((prev) => {
      const next = new Set(prev);
      if (next.has(trackId)) next.delete(trackId); else next.add(trackId);
      return next;
    });
    try {
      const r = await musicApi.toggleFavorite(trackId);
      if (r.success) {
        setIds((prev) => {
          const next = new Set(prev);
          if (r.data.isFavorited) next.add(trackId); else next.delete(trackId);
          return next;
        });
      }
    } catch { /* leave optimistic state */ }
  }, [user]);

  return { isFavorite, toggle, canFavorite: !!user };
}
