"use client";

import React, {
  createContext,
  useContext,
  useState,
  useCallback,
  useRef,
  useEffect,
} from "react";

const STREAM_BASE = (typeof window !== "undefined" ? process.env.NEXT_PUBLIC_API_URL || "" : "") + "/api";

// ─── Types ─────────────────────────────────────────────────

export interface Track {
  id: number;
  title: string;
  trackNumber: number;
  duration: number;
  genre: string | null;
  albumId: number;
  albumTitle: string;
  albumArtist: string; // The active/resolved display artist string
  coverPath: string | null; // The active/resolved display cover path
  artists?: string[]; // Track-specific artists (List<string> in API)
  albumArtists?: string[]; // Album-level artists
  albumCoverPath?: string | null; // Album cover path for fallback
}

type RepeatMode = "none" | "all" | "one";

interface PlayerContextValue {
  // State
  currentTrack: Track | null;
  queue: Track[];
  originalQueue: Track[];
  isPlaying: boolean;
  progress: number;
  duration: number;
  currentTime: number;
  volume: number;
  isMuted: boolean;
  isShuffled: boolean;
  repeatMode: RepeatMode;
  isLoading: boolean;

  // Actions
  play: (track: Track, trackList?: Track[]) => void;
  pause: () => void;
  resume: () => void;
  togglePlayPause: () => void;
  next: () => void;
  previous: () => void;
  seek: (position: number) => void;
  setVolume: (volume: number) => void;
  toggleMute: () => void;
  toggleShuffle: () => void;
  cycleRepeat: () => void;
  addToQueue: (track: Track) => void;
  removeFromQueue: (index: number) => void;
  clearQueue: () => void;
}

const PlayerContext = createContext<PlayerContextValue | undefined>(undefined);

// ─── Helpers ───────────────────────────────────────────────

/** Fisher-Yates shuffle */
function shuffleArray<T>(arr: T[]): T[] {
  const shuffled = [...arr];
  for (let i = shuffled.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [shuffled[i], shuffled[j]] = [shuffled[j], shuffled[i]];
  }
  return shuffled;
}

/** Logarithmic volume curve — makes the slider feel natural */
function toLogVolume(linear: number): number {
  if (linear <= 0) return 0;
  if (linear >= 1) return 1;
  return Math.pow(linear, 2);
}

const VOLUME_KEY = "attrition-volume";
const MUTED_KEY = "attrition-muted";

/** Resolves track-specific cover art and artists with appropriate album fallbacks */
function resolveTrack(track: Track): Track {
  // Fall back to album cover if track-specific cover is missing
  const resolvedCover = track.coverPath || track.albumCoverPath || null;

  // Resolve artist string prioritizing track-level artists list, then album-level artists list, then fallback albumArtist string
  let resolvedArtist = "Attrition OST";
  if (track.artists && track.artists.length > 0) {
    resolvedArtist = track.artists.join(", ");
  } else if (track.albumArtists && track.albumArtists.length > 0) {
    resolvedArtist = track.albumArtists.join(", ");
  } else if (track.albumArtist) {
    resolvedArtist = track.albumArtist;
  }

  return {
    ...track,
    coverPath: resolvedCover,
    albumArtist: resolvedArtist,
  };
}

// ─── Provider ──────────────────────────────────────────────

export function PlayerProvider({ children }: { children: React.ReactNode }) {
  const audioRef = useRef<HTMLAudioElement | null>(null);

  const [currentTrack, setCurrentTrack] = useState<Track | null>(null);
  const [queue, setQueue] = useState<Track[]>([]);
  const [originalQueue, setOriginalQueue] = useState<Track[]>([]);
  const [isPlaying, setIsPlaying] = useState(false);
  const [progress, setProgress] = useState(0);
  const [duration, setDuration] = useState(0);
  const [currentTime, setCurrentTime] = useState(0);
  const [volume, setVolumeState] = useState(0.7);
  const [isMuted, setIsMuted] = useState(false);
  const [isShuffled, setIsShuffled] = useState(false);
  const [repeatMode, setRepeatMode] = useState<RepeatMode>("none");
  const [isLoading, setIsLoading] = useState(false);

  // Initialize audio element
  useEffect(() => {
    const audio = new Audio();
    audio.preload = "auto";
    audioRef.current = audio;

    // Restore volume
    const savedVolume = localStorage.getItem(VOLUME_KEY);
    const savedMuted = localStorage.getItem(MUTED_KEY);
    if (savedVolume) {
      const vol = parseFloat(savedVolume);
      setVolumeState(vol);
      audio.volume = toLogVolume(vol);
    }
    if (savedMuted === "true") {
      setIsMuted(true);
      audio.muted = true;
    }

    // Audio events
    const onTimeUpdate = () => {
      setCurrentTime(audio.currentTime);
      if (audio.duration) {
        setProgress(audio.currentTime / audio.duration);
      }
    };

    const onLoadedMetadata = () => {
      setDuration(audio.duration);
      setIsLoading(false);
    };


    const onPlay = () => setIsPlaying(true);
    const onPause = () => setIsPlaying(false);
    const onWaiting = () => setIsLoading(true);
    const onCanPlay = () => setIsLoading(false);

    audio.addEventListener("timeupdate", onTimeUpdate);
    audio.addEventListener("loadedmetadata", onLoadedMetadata);
    audio.addEventListener("play", onPlay);
    audio.addEventListener("pause", onPause);
    audio.addEventListener("waiting", onWaiting);
    audio.addEventListener("canplay", onCanPlay);

    return () => {
      audio.removeEventListener("timeupdate", onTimeUpdate);
      audio.removeEventListener("loadedmetadata", onLoadedMetadata);
      audio.removeEventListener("play", onPlay);
      audio.removeEventListener("pause", onPause);
      audio.removeEventListener("waiting", onWaiting);
      audio.removeEventListener("canplay", onCanPlay);
      audio.pause();
      audio.src = "";
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Handle track end — implements repeat logic
  const handleTrackEnd = useCallback(() => {
    if (repeatMode === "one") {
      const audio = audioRef.current;
      if (audio) {
        audio.currentTime = 0;
        audio.play();
      }
      return;
    }

    // Find current index in queue
    setQueue((currentQueue) => {
      const idx = currentQueue.findIndex((t) => t.id === currentTrack?.id);
      if (idx < currentQueue.length - 1) {
        // Play next
        const nextTrack = currentQueue[idx + 1];
        loadAndPlay(nextTrack);
      } else if (repeatMode === "all" && currentQueue.length > 0) {
        // Loop back to start
        loadAndPlay(currentQueue[0]);
      } else {
        // Track ended, no repeat — stop and reset to beginning
        setIsPlaying(false);
        const audio = audioRef.current;
        if (audio) {
          audio.currentTime = 0;
        }
        setProgress(0);
        setCurrentTime(0);
      }
      return currentQueue;
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [repeatMode, currentTrack]);

  // Update ended handler when repeat mode changes
  useEffect(() => {
    const audio = audioRef.current;
    if (!audio) return;

    const onEnded = () => handleTrackEnd();
    audio.addEventListener("ended", onEnded);
    return () => audio.removeEventListener("ended", onEnded);
  }, [handleTrackEnd]);

  // MediaSession API — OS-level controls
  useEffect(() => {
    if (!currentTrack || !("mediaSession" in navigator)) return;

    navigator.mediaSession.metadata = new MediaMetadata({
      title: currentTrack.title,
      artist: currentTrack.albumArtist,
      album: currentTrack.albumTitle,
      artwork: currentTrack.coverPath
        ? [{ src: currentTrack.coverPath, sizes: "512x512", type: "image/jpeg" }]
        : [],
    });

    navigator.mediaSession.setActionHandler("play", () => resume());
    navigator.mediaSession.setActionHandler("pause", () => pause());
    navigator.mediaSession.setActionHandler("previoustrack", () => previous());
    navigator.mediaSession.setActionHandler("nexttrack", () => next());
    navigator.mediaSession.setActionHandler("seekto", (details) => {
      if (details.seekTime != null) seek(details.seekTime);
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [currentTrack]);

  // ─── Actions ─────────────────────────────────────────────

  const loadAndPlay = useCallback((track: Track) => {
    const audio = audioRef.current;
    if (!audio) return;

    const resolved = resolveTrack(track);
    setCurrentTrack(resolved);
    setIsLoading(true);
    audio.src = `${STREAM_BASE}/music/tracks/${resolved.id}/stream`;
    audio.play()
      .then(() => {
        fetch(`${STREAM_BASE}/music/tracks/${resolved.id}/play`, { method: "POST" })
          .catch((err) => console.error("Error updating play count:", err));
      })
      .catch(() => {
        // Autoplay blocked — user interaction needed
        setIsPlaying(false);
        setIsLoading(false);
      });
  }, []);

  const play = useCallback(
    (track: Track, trackList?: Track[]) => {
      const resolvedTrack = resolveTrack(track);
      if (trackList) {
        const resolvedList = trackList.map(resolveTrack);
        setOriginalQueue(resolvedList);
        if (isShuffled) {
          const shuffled = shuffleArray(resolvedList.filter((t) => t.id !== resolvedTrack.id));
          setQueue([resolvedTrack, ...shuffled]);
        } else {
          setQueue(resolvedList);
        }
      }
      loadAndPlay(resolvedTrack);
    },
    [isShuffled, loadAndPlay]
  );

  const pause = useCallback(() => {
    audioRef.current?.pause();
  }, []);

  const resume = useCallback(() => {
    audioRef.current?.play();
  }, []);

  const togglePlayPause = useCallback(() => {
    if (isPlaying) {
      pause();
    } else {
      resume();
    }
  }, [isPlaying, pause, resume]);

  const next = useCallback(() => {
    const idx = queue.findIndex((t) => t.id === currentTrack?.id);
    if (idx < queue.length - 1) {
      loadAndPlay(queue[idx + 1]);
    } else if (repeatMode === "all" && queue.length > 0) {
      loadAndPlay(queue[0]);
    }
  }, [queue, currentTrack, repeatMode, loadAndPlay]);

  const previous = useCallback(() => {
    const audio = audioRef.current;
    // If more than 3 seconds in, restart current track
    if (audio && audio.currentTime > 3) {
      audio.currentTime = 0;
      return;
    }

    const idx = queue.findIndex((t) => t.id === currentTrack?.id);
    if (idx > 0) {
      loadAndPlay(queue[idx - 1]);
    } else if (audio) {
      audio.currentTime = 0;
    }
  }, [queue, currentTrack, loadAndPlay]);

  const seek = useCallback((position: number) => {
    const audio = audioRef.current;
    if (audio && audio.duration) {
      audio.currentTime = position;
    }
  }, []);

  const setVolume = useCallback(
    (vol: number) => {
      const clamped = Math.max(0, Math.min(1, vol));
      setVolumeState(clamped);
      if (audioRef.current) {
        audioRef.current.volume = toLogVolume(clamped);
      }
      localStorage.setItem(VOLUME_KEY, String(clamped));
      if (isMuted && clamped > 0) {
        setIsMuted(false);
        if (audioRef.current) audioRef.current.muted = false;
        localStorage.setItem(MUTED_KEY, "false");
      }
    },
    [isMuted]
  );

  const toggleMute = useCallback(() => {
    const next = !isMuted;
    setIsMuted(next);
    if (audioRef.current) audioRef.current.muted = next;
    localStorage.setItem(MUTED_KEY, String(next));
  }, [isMuted]);

  const toggleShuffle = useCallback(() => {
    setIsShuffled((prev) => {
      const next = !prev;
      if (next) {
        // Shuffle queue, keeping current track first
        const rest = queue.filter((t) => t.id !== currentTrack?.id);
        const shuffled = shuffleArray(rest);
        if (currentTrack) {
          setQueue([currentTrack, ...shuffled]);
        }
      } else {
        // Restore original order
        setQueue(originalQueue);
      }
      return next;
    });
  }, [queue, currentTrack, originalQueue]);

  const cycleRepeat = useCallback(() => {
    setRepeatMode((prev) => {
      if (prev === "none") return "all";
      if (prev === "all") return "one";
      return "none";
    });
  }, []);

  const addToQueue = useCallback((track: Track) => {
    const resolved = resolveTrack(track);
    setQueue((prev) => [...prev, resolved]);
    setOriginalQueue((prev) => [...prev, resolved]);
  }, []);

  const removeFromQueue = useCallback((index: number) => {
    setQueue((prev) => prev.filter((_, i) => i !== index));
  }, []);

  const clearQueue = useCallback(() => {
    setQueue([]);
    setOriginalQueue([]);
  }, []);

  return (
    <PlayerContext.Provider
      value={{
        currentTrack,
        queue,
        originalQueue,
        isPlaying,
        progress,
        duration,
        currentTime,
        volume,
        isMuted,
        isShuffled,
        repeatMode,
        isLoading,
        play,
        pause,
        resume,
        togglePlayPause,
        next,
        previous,
        seek,
        setVolume,
        toggleMute,
        toggleShuffle,
        cycleRepeat,
        addToQueue,
        removeFromQueue,
        clearQueue,
      }}
    >
      {children}
    </PlayerContext.Provider>
  );
}

// ─── Hook ──────────────────────────────────────────────────

export function usePlayer() {
  const ctx = useContext(PlayerContext);
  if (!ctx) throw new Error("usePlayer must be used within PlayerProvider");
  return ctx;
}
