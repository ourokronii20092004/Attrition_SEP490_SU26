"use client";

import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { ListPlus, Plus } from "lucide-react";
import { musicApi } from "@/lib/api/music";
import { useAuth, useToast } from "@/lib/providers";
import { qk } from "@/lib/query-keys";

/**
 * Self-contained "add this track to a playlist" control: a small button that opens a menu of the
 * user's playlists (+ create-new). Fetches playlists lazily on open. Renders nothing for signed-out
 * users. Safe to drop into track rows and the player without prop threading.
 */
export function AddToPlaylistButton({ trackId, className, iconSize = 15 }: { trackId: number; className?: string; iconSize?: number }) {
  const { user } = useAuth();
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);

  const { data: playlists = [], isPending } = useQuery({
    queryKey: qk.music.playlists(),
    enabled: open && !!user,
    queryFn: async () => {
      const res = await musicApi.getPlaylists();
      return res.success ? res.data ?? [] : [];
    },
  });

  if (!user) return null;

  const stop = (e: React.MouseEvent) => { e.stopPropagation(); e.preventDefault(); };

  const add = async (playlistId: string, title: string) => {
    setBusy(true);
    try {
      const res = await musicApi.addTrackToPlaylist(playlistId, { trackId });
      if (res.success) {
        toast(`Added to "${title}".`, "success");
        queryClient.invalidateQueries({ queryKey: qk.music.playlists() });
        queryClient.invalidateQueries({ queryKey: qk.music.playlist(playlistId) });
        setOpen(false);
      } else {
        toast(res.error || "Couldn't add to playlist.", "error");
      }
    } catch {
      toast("Couldn't add to playlist.", "error");
    } finally {
      setBusy(false);
    }
  };

  const createAndAdd = async () => {
    const name = window.prompt("New playlist name:");
    if (!name?.trim()) return;
    setBusy(true);
    try {
      const created = await musicApi.createPlaylist({ name: name.trim() });
      if (created.success && created.data) {
        await musicApi.addTrackToPlaylist(created.data.playlistId, { trackId });
        toast(`Added to "${created.data.title}".`, "success");
        queryClient.invalidateQueries({ queryKey: qk.music.playlists() });
        setOpen(false);
      } else {
        toast(created.error || "Couldn't create playlist.", "error");
      }
    } catch {
      toast("Couldn't create playlist.", "error");
    } finally {
      setBusy(false);
    }
  };

  return (
    <span className="relative inline-flex">
      <button
        onClick={(e) => { stop(e); setOpen((v) => !v); }}
        className={className ?? "shrink-0 rounded-md p-1.5 text-fg-subtle transition-colors hover:bg-surface-3 hover:text-fg"}
        aria-label="Add to playlist"
        aria-haspopup="menu"
        aria-expanded={open}
      >
        <ListPlus size={iconSize} />
      </button>
      {open && (
        <>
          <span className="fixed inset-0 z-[var(--z-dropdown)]" onClick={(e) => { stop(e); setOpen(false); }} />
          <div
            className="absolute right-0 top-full z-[var(--z-dropdown)] mt-1 max-h-72 w-56 overflow-y-auto rounded-lg border border-border bg-surface p-1 shadow-[var(--shadow-lg)]"
            role="menu"
            onClick={stop}
          >
            <button onClick={createAndAdd} disabled={busy}
              className="flex w-full items-center gap-2 rounded-md px-2.5 py-2 text-left text-sm font-medium text-accent transition-colors hover:bg-surface-2 disabled:opacity-50">
              <Plus size={15} /> New playlist
            </button>
            <div className="my-1 h-px bg-border" />
            {isPending ? (
              <p className="px-2.5 py-2 text-xs text-fg-subtle">Loading…</p>
            ) : playlists.length === 0 ? (
              <p className="px-2.5 py-2 text-xs text-fg-subtle">No playlists yet.</p>
            ) : (
              playlists.map((pl) => (
                <button key={pl.playlistId} onClick={() => add(pl.playlistId, pl.title)} disabled={busy}
                  className="flex w-full items-center justify-between gap-2 rounded-md px-2.5 py-2 text-left text-sm text-fg transition-colors hover:bg-surface-2 disabled:opacity-50">
                  <span className="truncate">{pl.title}</span>
                  <span className="shrink-0 text-xs tabular-nums text-fg-subtle">{pl.trackCount}</span>
                </button>
              ))
            )}
          </div>
        </>
      )}
    </span>
  );
}
