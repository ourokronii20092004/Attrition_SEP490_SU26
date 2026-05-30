"use client";

import { useEffect, useRef } from "react";
import { Play, Pause, SkipBack, SkipForward, Volume2, X } from "lucide-react";
import { useAudioStore } from "@/lib/stores/audio-store";
import { getStreamUrl } from "@/lib/api/music";
import { musicApi } from "@/lib/api/music";
import { resolveMediaUrl } from "@/lib/api/media";

export function AudioPlayer() {
  const { currentTrack, isPlaying, volume, progress, duration, pause, resume, next, prev, setProgress, setDuration, setVolume, stop } =
    useAudioStore();
  const audioRef = useRef<HTMLAudioElement>(null);
  const playRecorded = useRef<number | null>(null);

  useEffect(() => {
    const audio = audioRef.current;
    if (!audio || !currentTrack) return;

    const src = getStreamUrl(currentTrack.trackId);
    if (audio.src !== src) {
      audio.src = src;
      audio.load();
    }

    if (isPlaying) {
      audio.play().catch(() => {});
      if (playRecorded.current !== currentTrack.trackId) {
        musicApi.recordPlay(currentTrack.trackId).catch(() => {});
        playRecorded.current = currentTrack.trackId;
      }
    } else {
      audio.pause();
    }
  }, [currentTrack, isPlaying]);

  useEffect(() => {
    const audio = audioRef.current;
    if (!audio) return;
    audio.volume = volume;
  }, [volume]);

  const handleTimeUpdate = () => {
    const audio = audioRef.current;
    if (audio) setProgress(audio.currentTime);
  };

  const handleLoadedMetadata = () => {
    const audio = audioRef.current;
    if (audio) setDuration(audio.duration);
  };

  const handleEnded = () => next();

  const seek = (e: React.ChangeEvent<HTMLInputElement>) => {
    const audio = audioRef.current;
    if (audio) {
      audio.currentTime = Number(e.target.value);
      setProgress(audio.currentTime);
    }
  };

  if (!currentTrack) return <audio ref={audioRef} hidden />;

  const formatTime = (s: number) => {
    const m = Math.floor(s / 60);
    const sec = Math.floor(s % 60);
    return `${m}:${sec.toString().padStart(2, "0")}`;
  };

  return (
    <>
      <audio
        ref={audioRef}
        onTimeUpdate={handleTimeUpdate}
        onLoadedMetadata={handleLoadedMetadata}
        onEnded={handleEnded}
        hidden
      />
      <div className="glass fixed inset-x-0 bottom-0 z-[200] border-x-0 border-b-0 motion-safe:animate-rise-in">
        <div className="mx-auto flex h-20 max-w-7xl items-center gap-4 px-4">
          {/* Track info */}
          <div className="flex min-w-0 flex-1 items-center gap-3">
            {currentTrack.coverPath && (
              <img
                src={resolveMediaUrl(currentTrack.coverPath) ?? ""}
                alt=""
                className="h-12 w-12 rounded-lg object-cover shadow-[var(--shadow-sm)]"
              />
            )}
            <div className="min-w-0">
              <p className="truncate text-sm font-medium text-fg">{currentTrack.title}</p>
              <p className="truncate text-xs text-fg-muted">{currentTrack.albumTitle}</p>
            </div>
          </div>

          {/* Controls */}
          <div className="flex flex-col items-center gap-1">
            <div className="flex items-center gap-3">
              <button onClick={prev} className="text-fg-muted transition-colors hover:text-fg" aria-label="Previous">
                <SkipBack size={18} />
              </button>
              <button
                onClick={isPlaying ? pause : resume}
                className="flex h-10 w-10 items-center justify-center rounded-full bg-accent text-accent-fg shadow-sm transition-transform duration-150 hover:brightness-110 active:scale-95"
                aria-label={isPlaying ? "Pause" : "Play"}
              >
                {isPlaying ? <Pause size={16} /> : <Play size={16} className="ml-0.5" />}
              </button>
              <button onClick={next} className="text-fg-muted transition-colors hover:text-fg" aria-label="Next">
                <SkipForward size={18} />
              </button>
            </div>
            <div className="flex items-center gap-2 text-xs text-fg-muted">
              <span>{formatTime(progress)}</span>
              <input
                type="range"
                min={0}
                max={duration || 0}
                value={progress}
                onChange={seek}
                className="h-1 w-48 cursor-pointer accent-accent"
              />
              <span>{formatTime(duration)}</span>
            </div>
          </div>

          {/* Volume + close */}
          <div className="flex flex-1 items-center justify-end gap-3">
            <Volume2 size={16} className="text-fg-muted" />
            <input
              type="range"
              min={0}
              max={1}
              step={0.01}
              value={volume}
              onChange={(e) => setVolume(Number(e.target.value))}
              className="h-1 w-20 cursor-pointer accent-accent"
            />
            <button onClick={stop} className="text-fg-muted hover:text-fg" aria-label="Close player">
              <X size={16} />
            </button>
          </div>
        </div>
      </div>
    </>
  );
}
