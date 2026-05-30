import { create } from "zustand";
import type { MusicTrackDto } from "@/lib/types";

interface AudioState {
  currentTrack: MusicTrackDto | null;
  queue: MusicTrackDto[];
  isPlaying: boolean;
  volume: number;
  progress: number;
  duration: number;

  play: (track: MusicTrackDto, queue?: MusicTrackDto[]) => void;
  pause: () => void;
  resume: () => void;
  next: () => void;
  prev: () => void;
  setVolume: (v: number) => void;
  setProgress: (p: number) => void;
  setDuration: (d: number) => void;
  stop: () => void;
}

export const useAudioStore = create<AudioState>((set, get) => ({
  currentTrack: null,
  queue: [],
  isPlaying: false,
  volume: 0.8,
  progress: 0,
  duration: 0,

  play: (track, queue) => {
    set({
      currentTrack: track,
      queue: queue ?? get().queue,
      isPlaying: true,
      progress: 0,
      duration: 0,
    });
  },

  pause: () => set({ isPlaying: false }),
  resume: () => set({ isPlaying: true }),

  next: () => {
    const { queue, currentTrack } = get();
    if (!currentTrack || queue.length === 0) return;
    const idx = queue.findIndex((t) => t.trackId === currentTrack.trackId);
    const next = queue[(idx + 1) % queue.length];
    if (next) set({ currentTrack: next, isPlaying: true, progress: 0, duration: 0 });
  },

  prev: () => {
    const { queue, currentTrack } = get();
    if (!currentTrack || queue.length === 0) return;
    const idx = queue.findIndex((t) => t.trackId === currentTrack.trackId);
    const prev = queue[(idx - 1 + queue.length) % queue.length];
    if (prev) set({ currentTrack: prev, isPlaying: true, progress: 0, duration: 0 });
  },

  setVolume: (v) => set({ volume: v }),
  setProgress: (p) => set({ progress: p }),
  setDuration: (d) => set({ duration: d }),
  stop: () => set({ currentTrack: null, isPlaying: false, progress: 0, duration: 0, queue: [] }),
}));
