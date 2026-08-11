"use client";

import { useState } from "react";
import {
  Play, Pause, SkipBack, SkipForward, Volume2, VolumeX, X,
  Shuffle, Repeat, Repeat1, Heart, Disc3,
} from "lucide-react";
import { useAudioStore } from "@/lib/stores/audio-store";
import { resolveMediaUrl } from "@/lib/api/media";
import { useAudioEngine } from "./player/use-audio-engine";
import { formatDuration as fmt } from "@/lib/format-duration";

/**
 * Admin sidebar music player.
 *
 * A widget, not a modal: pinned inside the account block (between the user card
 * and Sign Out). While a track is loaded it renders EITHER the slim collapsed
 * line OR the expanded square — never both. Clicking the line expands it IN
 * PLACE into the square (seek + transport + volume), like a minimized widget
 * opening up; clicking its header collapses it back to the line. X (in both
 * states) stops playback and hides the widget; the track stays playable from
 * any music surface afterwards.
 *
 * It shares the exact playback engine with the public player via useAudioEngine, so
 * a track an admin queued here and one started on the user site behave identically.
 */

function ScrubSlider({ value, max, onChange, onCommit, label, className }: {
  value: number; max: number;
  onChange: (v: number) => void; onCommit: (v: number) => void;
  label: string; className?: string;
}) {
  const pct = max ? (value / max) * 100 : 0;
  return (
    <input
      type="range" min={0} max={max || 0} step={0.1} value={value} aria-label={label}
      onPointerDown={(e) => onChange(Number((e.target as HTMLInputElement).value))}
      onChange={(e) => onChange(Number(e.target.value))}
      onPointerUp={(e) => onCommit(Number((e.target as HTMLInputElement).value))}
      className={`player-seek h-1 cursor-pointer ${className ?? ""}`}
      style={{ ["--pct" as string]: `${pct}%` }}
    />
  );
}

function IconButton({ onClick, label, active, children, className }: {
  onClick: () => void; label: string; active?: boolean; children: React.ReactNode; className?: string;
}) {
  return (
    <button
      onClick={onClick}
      aria-label={label}
      title={label}
      className={`shrink-0 rounded-md p-1.5 transition-colors ${
        active ? "text-accent" : "text-fg-muted hover:bg-surface-2 hover:text-fg"
      } ${className ?? ""}`}
    >
      {children}
    </button>
  );
}

export function AdminMusicPlayer() {
  const { currentTrack, isPlaying, shuffle, repeat, next, prev, stop, toggleShuffle, cycleRepeat } = useAudioStore();
  const {
    audioRef, muted, setMuted, displayTime, duration, volume,
    toggle, onVolume, onTimeUpdate, onLoadedMetadata, onEnded,
    moveScrub, commitScrub,
    liked, canFavorite, toggleFav,
  } = useAudioEngine();
  const [open, setOpen] = useState(false);

  // X in both states: stop playback and collapse the widget. stop() clears
  // currentTrack so the whole widget unmounts.
  const stopAll = () => { stop(); setOpen(false); };

  const cover = currentTrack ? resolveMediaUrl(currentTrack.coverPath ?? currentTrack.albumCoverPath) : null;
  const artists = currentTrack
    ? (currentTrack.artists?.join(", ") || currentTrack.albumTitle || "Unknown artist")
    : "";

  return (
    <div>
      {/* The <audio> element this player controls — without it the engine has no element to
          drive (src never loads, play() is a no-op, and no timeupdate fires so play counts
          never record). Kept mounted even when no track is loaded: unmounting it on stop is
          what broke replaying the same track after pressing X. */}
      <audio ref={audioRef} onTimeUpdate={onTimeUpdate} onLoadedMetadata={onLoadedMetadata} onEnded={onEnded} hidden />

      {currentTrack && (
        <div className="mt-1 rounded-lg border border-border/70 bg-surface-2/50 p-1 pr-1.5">
          {open ? (
            /* ── Expanded square widget: replaces the collapsed line entirely ── */
            <div className="p-1 motion-safe:animate-rise-in">
              {/* Header: cover + title + X (collapse is the same X / stop) */}
              <div className="flex items-center gap-2">
                {cover ? (
                  <img src={cover} alt="" className="h-9 w-9 shrink-0 rounded-md object-cover" />
                ) : (
                  <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-md bg-accent-soft text-accent">
                    <Disc3 size={16} />
                  </span>
                )}
                <button
                  onClick={() => setOpen(false)}
                  aria-expanded={true}
                  aria-label="Collapse music player"
                  className="min-w-0 flex-1 text-left"
                >
                  <span className="block truncate text-xs font-semibold text-fg">{currentTrack.title}</span>
                  <span className="block truncate text-[11px] text-fg-muted">{artists}</span>
                </button>
                <button
                  onClick={stopAll}
                  aria-label="Stop playback"
                  title="Stop"
                  className="flex h-7 w-7 shrink-0 items-center justify-center rounded-md text-fg-subtle transition-colors hover:bg-danger/10 hover:text-danger"
                >
                  <X size={14} />
                </button>
              </div>

              {/* Seek */}
              <div className="mt-2 flex items-center gap-2 text-[11px] tabular-nums text-fg-muted">
                <span className="w-8 text-right">{fmt(displayTime)}</span>
                <ScrubSlider
                  value={displayTime} max={duration} label="Seek"
                  onChange={moveScrub} onCommit={commitScrub} className="flex-1"
                />
                <span className="w-8">{fmt(duration)}</span>
              </div>

              {/* Transport */}
              <div className="mt-1.5 flex items-center justify-center gap-1.5">
                <IconButton onClick={toggleShuffle} label="Shuffle" active={shuffle}><Shuffle size={15} /></IconButton>
                <IconButton onClick={prev} label="Previous"><SkipBack size={17} fill="currentColor" /></IconButton>
                <button
                  onClick={toggle}
                  aria-label={isPlaying ? "Pause" : "Play"}
                  className="flex h-9 w-9 items-center justify-center rounded-full bg-accent text-accent-fg shadow-[var(--shadow-glow)] transition-transform hover:scale-105 active:scale-95"
                >
                  {isPlaying ? <Pause size={16} fill="currentColor" /> : <Play size={16} fill="currentColor" className="ml-0.5" />}
                </button>
                <IconButton onClick={next} label="Next"><SkipForward size={17} fill="currentColor" /></IconButton>
                <IconButton onClick={cycleRepeat} label="Repeat" active={repeat !== "off"}>
                  {repeat === "one" ? <Repeat1 size={15} /> : <Repeat size={15} />}
                </IconButton>
              </div>

              {/* Volume + like */}
              <div className="mt-1.5 flex items-center gap-2">
                <button
                  onClick={() => setMuted((m) => !m)}
                  aria-label={muted ? "Unmute" : "Mute"}
                  className="shrink-0 rounded-md p-1 text-fg-muted transition-colors hover:bg-surface-2 hover:text-fg"
                >
                  {muted || volume === 0 ? <VolumeX size={15} /> : <Volume2 size={15} />}
                </button>
                <ScrubSlider
                  value={muted ? 0 : volume} max={1} label="Volume"
                  onChange={(v) => onVolume(v)} onCommit={() => {}}
                  className="w-full"
                />
                {canFavorite && (
                  <button
                    onClick={() => toggleFav(currentTrack.trackId)}
                    aria-label={liked ? "Unlike" : "Like"}
                    className={`shrink-0 rounded-md p-1 transition-colors ${liked ? "text-accent" : "text-fg-muted hover:bg-surface-2 hover:text-fg"}`}
                  >
                    <Heart size={15} fill={liked ? "currentColor" : "none"} />
                  </button>
                )}
              </div>
            </div>
          ) : (
            /* ── Collapsed now-playing line: click to expand into the widget ── */
            <div className="flex items-center gap-1">
              <button
                onClick={() => setOpen(true)}
                aria-expanded={false}
                aria-label="Expand music player"
                className="flex min-w-0 flex-1 items-center gap-2.5 rounded-md px-1.5 py-1 text-left transition-colors hover:bg-surface-2"
              >
                {cover ? (
                  <img src={cover} alt="" className="h-8 w-8 shrink-0 rounded-md object-cover" />
                ) : (
                  <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-md bg-accent-soft text-accent">
                    <Disc3 size={15} />
                  </span>
                )}
                <span className="min-w-0">
                  <span className="block truncate text-xs font-medium text-fg">{currentTrack.title}</span>
                  <span className="block truncate text-[11px] text-fg-muted">{artists}</span>
                </span>
              </button>
              <button
                onClick={toggle}
                aria-label={isPlaying ? "Pause" : "Play"}
                className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-accent/90 text-accent-fg transition-colors hover:bg-accent"
              >
                {isPlaying ? <Pause size={12} fill="currentColor" /> : <Play size={12} fill="currentColor" className="ml-px" />}
              </button>
              <button
                onClick={stopAll}
                aria-label="Stop playback"
                title="Stop"
                className="flex h-7 w-7 shrink-0 items-center justify-center rounded-md text-fg-subtle transition-colors hover:bg-danger/10 hover:text-danger"
              >
                <X size={13} />
              </button>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
