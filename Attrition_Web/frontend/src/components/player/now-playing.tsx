"use client";

import { ChevronDown, Play, Pause, SkipBack, SkipForward, Shuffle, Repeat, Repeat1, Heart, ListMusic, Volume2, VolumeX } from "lucide-react";
import { resolveMediaUrl } from "@/lib/api/media";
import type { MusicTrackDto } from "@/lib/types";
import type { RepeatMode } from "@/lib/stores/audio-store";
import { formatDuration as fmt } from "@/lib/format-duration";

export interface NowPlayingProps {
  track: MusicTrackDto;
  isPlaying: boolean;
  displayTime: number;
  duration: number;
  shuffle: boolean;
  repeat: RepeatMode;
  volume: number;
  muted: boolean;
  liked: boolean;
  canFavorite: boolean;
  onClose: () => void;
  onToggle: () => void;
  onNext: () => void;
  onPrev: () => void;
  onShuffle: () => void;
  onRepeat: () => void;
  onLike: () => void;
  onMute: () => void;
  onVolume: (v: number) => void;
  onSeekStart: (v: number) => void;
  onSeekMove: (v: number) => void;
  onSeekCommit: (v: number) => void;
  onOpenQueue: () => void;
}

/** Full-screen "now playing" surface: blurred cover backdrop, large art, full controls. */
export function NowPlaying(p: NowPlayingProps) {
  const cover = resolveMediaUrl(p.track.coverPath ?? p.track.albumCoverPath);
  const pct = p.duration ? (p.displayTime / p.duration) * 100 : 0;

  return (
    <div className="fixed inset-0 z-[280] flex flex-col overflow-hidden motion-safe:animate-fade-in">
      {/* Blurred cover backdrop + scrim */}
      {cover && <img src={cover} alt="" aria-hidden className="absolute inset-0 h-full w-full scale-110 object-cover opacity-40 blur-3xl" />}
      <div className="absolute inset-0 bg-gradient-to-b from-bg/70 via-bg/85 to-bg" aria-hidden />

      <div className="relative flex items-center justify-between px-5 py-4 sm:px-8">
        <button onClick={p.onClose} className="flex items-center gap-1.5 text-sm text-fg-muted transition-colors hover:text-fg" aria-label="Collapse player">
          <ChevronDown size={20} /> <span className="hidden sm:inline">Now Playing</span>
        </button>
        <button onClick={p.onOpenQueue} className="text-fg-muted transition-colors hover:text-fg" aria-label="Open queue">
          <ListMusic size={20} />
        </button>
      </div>

      <div className="relative mx-auto flex w-full max-w-md flex-1 flex-col items-center justify-center gap-8 px-6 pb-10">
        {cover ? (
          <img src={cover} alt="" className="aspect-square w-full max-w-sm rounded-2xl object-cover shadow-[var(--shadow-lg)]" />
        ) : (
          <div className="aspect-square w-full max-w-sm rounded-2xl bg-surface-2" />
        )}

        <div className="w-full">
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <h1 className="truncate font-display text-2xl font-bold text-fg">{p.track.title}</h1>
              <p className="truncate text-fg-muted">{p.track.artists?.join(", ") || p.track.albumTitle}</p>
            </div>
            {p.canFavorite && (
              <button onClick={p.onLike} className={`shrink-0 transition-colors ${p.liked ? "text-accent" : "text-fg-muted hover:text-fg"}`} aria-label={p.liked ? "Unlike" : "Like"}>
                <Heart size={24} fill={p.liked ? "currentColor" : "none"} />
              </button>
            )}
          </div>

          {/* Seek */}
          <div className="mt-5">
            <input
              type="range" min={0} max={p.duration || 0} step={0.1} value={p.displayTime} aria-label="Seek"
              onPointerDown={(e) => p.onSeekStart(Number((e.target as HTMLInputElement).value))}
              onChange={(e) => p.onSeekMove(Number(e.target.value))}
              onPointerUp={(e) => p.onSeekCommit(Number((e.target as HTMLInputElement).value))}
              className="player-seek h-1.5 w-full cursor-pointer"
              style={{ ["--pct" as string]: `${pct}%` }}
            />
            <div className="mt-1.5 flex justify-between text-xs tabular-nums text-fg-muted">
              <span>{fmt(p.displayTime)}</span><span>{fmt(p.duration)}</span>
            </div>
          </div>

          {/* Transport */}
          <div className="mt-4 flex items-center justify-center gap-6">
            <button onClick={p.onShuffle} className={`transition-colors ${p.shuffle ? "text-accent" : "text-fg-muted hover:text-fg"}`} aria-label="Shuffle"><Shuffle size={20} /></button>
            <button onClick={p.onPrev} className="text-fg transition-transform hover:scale-110" aria-label="Previous"><SkipBack size={26} fill="currentColor" /></button>
            <button onClick={p.onToggle} className="flex h-16 w-16 items-center justify-center rounded-full bg-accent text-accent-fg shadow-[var(--shadow-glow)] transition-transform hover:scale-105 active:scale-95" aria-label={p.isPlaying ? "Pause" : "Play"}>
              {p.isPlaying ? <Pause size={26} fill="currentColor" /> : <Play size={26} fill="currentColor" className="ml-1" />}
            </button>
            <button onClick={p.onNext} className="text-fg transition-transform hover:scale-110" aria-label="Next"><SkipForward size={26} fill="currentColor" /></button>
            <button onClick={p.onRepeat} className={`transition-colors ${p.repeat !== "off" ? "text-accent" : "text-fg-muted hover:text-fg"}`} aria-label="Repeat">
              {p.repeat === "one" ? <Repeat1 size={20} /> : <Repeat size={20} />}
            </button>
          </div>

          {/* Volume */}
          <div className="mt-6 flex items-center gap-3">
            <button onClick={p.onMute} className="text-fg-muted transition-colors hover:text-fg" aria-label={p.muted ? "Unmute" : "Mute"}>
              {p.muted || p.volume === 0 ? <VolumeX size={18} /> : <Volume2 size={18} />}
            </button>
            <input
              type="range" min={0} max={1} step={0.01} value={p.muted ? 0 : p.volume} aria-label="Volume"
              onChange={(e) => p.onVolume(Number(e.target.value))}
              className="player-seek h-1 flex-1 cursor-pointer"
              style={{ ["--pct" as string]: `${(p.muted ? 0 : p.volume) * 100}%` }}
            />
          </div>
        </div>
      </div>
    </div>
  );
}
