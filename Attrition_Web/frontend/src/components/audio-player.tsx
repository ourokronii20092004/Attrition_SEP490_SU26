"use client";

import { useEffect, useRef, useState } from "react";
import {
  Play, Pause, SkipBack, SkipForward, Volume2, VolumeX, X,
  Shuffle, Repeat, Repeat1, Heart, ListMusic, Maximize2,
} from "lucide-react";
import { useAudioStore } from "@/lib/stores/audio-store";
import { getStreamUrl, musicApi } from "@/lib/api/music";
import { resolveMediaUrl } from "@/lib/api/media";
import { useFavorites } from "./player/use-favorites";
import { NowPlaying } from "./player/now-playing";
import { QueuePanel } from "./player/queue-panel";

function fmt(s: number) {
  if (!Number.isFinite(s)) return "0:00";
  return `${Math.floor(s / 60)}:${String(Math.floor(s % 60)).padStart(2, "0")}`;
}

export function AudioPlayer() {
  const {
    currentTrack, isPlaying, volume, progress, duration, shuffle, repeat,
    pause, resume, next, prev, setProgress, setDuration, setVolume, stop,
    toggleShuffle, cycleRepeat,
  } = useAudioStore();

  const audioRef = useRef<HTMLAudioElement>(null);
  const playRecorded = useRef<number | null>(null);
  const wasPlaying = useRef(false);

  const [scrubbing, setScrubbing] = useState(false);
  const [scrubValue, setScrubValue] = useState(0);
  const [muted, setMuted] = useState(false);
  const [expanded, setExpanded] = useState(false);
  const [queueOpen, setQueueOpen] = useState(false);

  const { isFavorite, toggle: toggleFav, canFavorite } = useFavorites();

  // Source load + play/pause; clear src when the player is closed so nothing plays detached.
  useEffect(() => {
    const audio = audioRef.current;
    if (!audio) return;
    if (!currentTrack) {
      audio.pause(); audio.removeAttribute("src"); audio.load();
      playRecorded.current = null;
      return;
    }
    const src = getStreamUrl(currentTrack.trackId);
    if (audio.src !== src) { audio.src = src; audio.load(); }
    if (isPlaying && !scrubbing) {
      audio.play().catch(() => {});
      if (playRecorded.current !== currentTrack.trackId) {
        musicApi.recordPlay(currentTrack.trackId).catch(() => {});
        playRecorded.current = currentTrack.trackId;
      }
    } else if (!isPlaying) {
      audio.pause();
    }
  }, [currentTrack, isPlaying, scrubbing]);

  useEffect(() => {
    const audio = audioRef.current;
    if (audio) audio.volume = muted ? 0 : volume;
  }, [volume, muted]);

  // Keyboard: space toggles play, Esc collapses the expanded view.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape" && expanded) setExpanded(false);
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [expanded]);

  const onTimeUpdate = () => {
    const audio = audioRef.current;
    if (audio && !scrubbing) setProgress(audio.currentTime);
  };
  const onLoadedMetadata = () => {
    const audio = audioRef.current;
    if (audio) setDuration(audio.duration);
  };

  const beginScrub = (v: number) => { wasPlaying.current = isPlaying; setScrubbing(true); setScrubValue(v); audioRef.current?.pause(); };
  const moveScrub = (v: number) => setScrubValue(v);
  const commitScrub = (v: number) => {
    const audio = audioRef.current;
    if (audio) audio.currentTime = v;
    setProgress(v); setScrubbing(false);
    if (wasPlaying.current) audio?.play().catch(() => {});
  };

  const displayTime = scrubbing ? scrubValue : progress;
  const pct = duration ? (displayTime / duration) * 100 : 0;
  const liked = currentTrack ? isFavorite(currentTrack.trackId) : false;
  const toggle = () => (isPlaying ? pause() : resume());
  const onVolume = (v: number) => { setMuted(false); setVolume(v); };

  const cover = currentTrack ? resolveMediaUrl(currentTrack.coverPath ?? currentTrack.albumCoverPath) : null;

  return (
    <>
      <audio ref={audioRef} onTimeUpdate={onTimeUpdate} onLoadedMetadata={onLoadedMetadata} onEnded={next} hidden />

      {currentTrack && expanded && (
        <NowPlaying
          track={currentTrack} isPlaying={isPlaying} displayTime={displayTime} duration={duration}
          shuffle={shuffle} repeat={repeat} volume={volume} muted={muted} liked={liked} canFavorite={canFavorite}
          onClose={() => setExpanded(false)} onToggle={toggle} onNext={next} onPrev={prev}
          onShuffle={toggleShuffle} onRepeat={cycleRepeat} onLike={() => toggleFav(currentTrack.trackId)}
          onMute={() => setMuted((m) => !m)} onVolume={onVolume}
          onSeekStart={beginScrub} onSeekMove={moveScrub} onSeekCommit={commitScrub}
          onOpenQueue={() => setQueueOpen(true)}
        />
      )}

      <QueuePanel open={queueOpen} onClose={() => setQueueOpen(false)} />

      {currentTrack && (
        <div className="glass fixed inset-x-0 bottom-0 z-[200] border-x-0 border-b-0 motion-safe:animate-rise-in">
          <div className="mx-auto grid h-[5.25rem] max-w-7xl grid-cols-[1fr_auto_1fr] items-center gap-4 px-3 sm:px-6">
            {/* ── Left: track identity ── */}
            <div className="flex min-w-0 items-center gap-3">
              <button onClick={() => setExpanded(true)} className="group relative shrink-0" aria-label="Expand player">
                {cover ? (
                  <img src={cover} alt="" className="h-14 w-14 rounded-md object-cover shadow-[var(--shadow-sm)]" />
                ) : (
                  <div className="h-14 w-14 rounded-md bg-surface-2" />
                )}
                <span className="absolute inset-0 flex items-center justify-center rounded-md bg-black/50 opacity-0 transition-opacity group-hover:opacity-100">
                  <Maximize2 size={18} className="text-white" />
                </span>
              </button>
              <div className="min-w-0">
                <p className="truncate text-sm font-semibold text-fg">{currentTrack.title}</p>
                <p className="truncate text-xs text-fg-muted">{currentTrack.artists?.join(", ") || currentTrack.albumTitle}</p>
              </div>
              {canFavorite && (
                <button onClick={() => toggleFav(currentTrack.trackId)} className={`ml-1 hidden shrink-0 transition-colors sm:block ${liked ? "text-accent" : "text-fg-subtle hover:text-fg"}`} aria-label={liked ? "Unlike" : "Like"}>
                  <Heart size={17} fill={liked ? "currentColor" : "none"} />
                </button>
              )}
            </div>

            {/* ── Center: transport + seek ── */}
            <div className="flex w-[min(40rem,46vw)] flex-col items-center gap-1.5">
              <div className="flex items-center gap-5">
                <button onClick={toggleShuffle} className={`hidden transition-colors sm:block ${shuffle ? "text-accent" : "text-fg-muted hover:text-fg"}`} aria-label="Shuffle"><Shuffle size={16} /></button>
                <button onClick={prev} className="text-fg-muted transition-colors hover:text-fg" aria-label="Previous"><SkipBack size={18} fill="currentColor" /></button>
                <button onClick={toggle} className="flex h-9 w-9 items-center justify-center rounded-full bg-accent text-accent-fg shadow-[var(--shadow-glow)] transition-transform hover:scale-105 active:scale-95" aria-label={isPlaying ? "Pause" : "Play"}>
                  {isPlaying ? <Pause size={16} fill="currentColor" /> : <Play size={16} fill="currentColor" className="ml-0.5" />}
                </button>
                <button onClick={next} className="text-fg-muted transition-colors hover:text-fg" aria-label="Next"><SkipForward size={18} fill="currentColor" /></button>
                <button onClick={cycleRepeat} className={`hidden transition-colors sm:block ${repeat !== "off" ? "text-accent" : "text-fg-muted hover:text-fg"}`} aria-label="Repeat">
                  {repeat === "one" ? <Repeat1 size={16} /> : <Repeat size={16} />}
                </button>
              </div>
              <div className="flex w-full items-center gap-2 text-[11px] tabular-nums text-fg-muted">
                <span className="w-9 text-right">{fmt(displayTime)}</span>
                <input
                  type="range" min={0} max={duration || 0} step={0.1} value={displayTime} aria-label="Seek"
                  onPointerDown={(e) => beginScrub(Number((e.target as HTMLInputElement).value))}
                  onChange={(e) => moveScrub(Number(e.target.value))}
                  onPointerUp={(e) => commitScrub(Number((e.target as HTMLInputElement).value))}
                  className="player-seek h-1 flex-1 cursor-pointer"
                  style={{ ["--pct" as string]: `${pct}%` }}
                />
                <span className="w-9">{fmt(duration)}</span>
              </div>
            </div>

            {/* ── Right: queue, volume, close ── */}
            <div className="flex items-center justify-end gap-2.5">
              <button onClick={() => setQueueOpen((q) => !q)} className={`hidden transition-colors sm:block ${queueOpen ? "text-accent" : "text-fg-muted hover:text-fg"}`} aria-label="Queue"><ListMusic size={18} /></button>
              <button onClick={() => setMuted((m) => !m)} className="text-fg-muted transition-colors hover:text-fg" aria-label={muted ? "Unmute" : "Mute"}>
                {muted || volume === 0 ? <VolumeX size={18} /> : <Volume2 size={18} />}
              </button>
              <input
                type="range" min={0} max={1} step={0.01} value={muted ? 0 : volume} aria-label="Volume"
                onChange={(e) => onVolume(Number(e.target.value))}
                className="player-seek hidden h-1 w-24 cursor-pointer lg:block"
                style={{ ["--pct" as string]: `${(muted ? 0 : volume) * 100}%` }}
              />
              <button onClick={stop} className="ml-1 text-fg-subtle transition-colors hover:text-danger" aria-label="Close player and stop playback" title="Close player">
                <X size={18} />
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
