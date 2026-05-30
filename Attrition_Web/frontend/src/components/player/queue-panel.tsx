"use client";

import { X, Music as MusicIcon, GripVertical } from "lucide-react";
import { useAudioStore } from "@/lib/stores/audio-store";
import { resolveMediaUrl } from "@/lib/api/media";

function fmt(s: number) {
  if (!Number.isFinite(s)) return "0:00";
  return `${Math.floor(s / 60)}:${String(Math.floor(s % 60)).padStart(2, "0")}`;
}

/** Spotify-style queue panel: now-playing + the rest of the queue, click to jump. */
export function QueuePanel({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { queue, currentTrack, isPlaying, playAt } = useAudioStore();
  if (!open) return null;

  const curIdx = currentTrack ? queue.findIndex((t) => t.trackId === currentTrack.trackId) : -1;
  const upcoming = curIdx >= 0 ? queue.slice(curIdx + 1) : queue;

  return (
    <>
      <div className="fixed inset-0 z-[290] bg-black/40 backdrop-blur-sm motion-safe:animate-fade-in" onClick={onClose} aria-hidden />
      <aside className="glass fixed bottom-24 right-4 top-20 z-[300] flex w-[22rem] max-w-[calc(100vw-2rem)] flex-col rounded-2xl p-4 shadow-[var(--shadow-lg)] motion-safe:animate-rise-in">
        <div className="flex items-center justify-between">
          <h2 className="font-display text-lg font-bold text-fg">Queue</h2>
          <button onClick={onClose} className="text-fg-muted transition-colors hover:text-fg" aria-label="Close queue"><X size={18} /></button>
        </div>

        <div className="mt-4 flex-1 overflow-y-auto">
          {currentTrack && (
            <>
              <p className="mb-2 text-xs font-semibold uppercase tracking-wider text-fg-subtle">Now playing</p>
              <QueueRow track={currentTrack} active onClick={() => {}} playing={isPlaying} />
            </>
          )}

          {upcoming.length > 0 ? (
            <>
              <p className="mb-2 mt-5 text-xs font-semibold uppercase tracking-wider text-fg-subtle">Next up</p>
              <div className="space-y-0.5">
                {upcoming.map((t, i) => (
                  <QueueRow key={`${t.trackId}-${i}`} track={t} onClick={() => playAt(curIdx + 1 + i)} />
                ))}
              </div>
            </>
          ) : (
            <p className="mt-6 text-center text-sm text-fg-muted">Nothing else in the queue.</p>
          )}
        </div>
      </aside>
    </>
  );
}

function QueueRow({ track, active, playing, onClick }: {
  track: { trackId: number; title: string; artists?: string[]; albumTitle?: string | null; coverPath?: string | null; albumCoverPath?: string | null; duration: number };
  active?: boolean; playing?: boolean; onClick: () => void;
}) {
  const cover = resolveMediaUrl(track.coverPath ?? track.albumCoverPath);
  return (
    <button
      onClick={onClick}
      className={`group flex w-full items-center gap-3 rounded-lg p-2 text-left transition-colors hover:bg-surface-2 ${active ? "bg-surface-2/60" : ""}`}
    >
      {cover ? (
        <img src={cover} alt="" className="h-10 w-10 shrink-0 rounded object-cover" />
      ) : (
        <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded bg-surface-3 text-fg-subtle"><MusicIcon size={16} /></div>
      )}
      <div className="min-w-0 flex-1">
        <p className={`truncate text-sm ${active ? "font-semibold text-accent" : "font-medium text-fg"}`}>{track.title}</p>
        <p className="truncate text-xs text-fg-muted">{track.artists?.join(", ") || track.albumTitle}</p>
      </div>
      {active && playing ? (
        <EqualizerBars />
      ) : (
        <span className="shrink-0 text-xs tabular-nums text-fg-subtle">{fmt(track.duration)}</span>
      )}
    </button>
  );
}

/** Tiny animated equalizer shown next to the currently playing row. */
function EqualizerBars() {
  return (
    <span className="flex h-4 items-end gap-0.5" aria-hidden>
      <span className="w-0.5 bg-accent animate-eq" style={{ animationDelay: "0ms" }} />
      <span className="w-0.5 bg-accent animate-eq" style={{ animationDelay: "150ms" }} />
      <span className="w-0.5 bg-accent animate-eq" style={{ animationDelay: "300ms" }} />
    </span>
  );
}
