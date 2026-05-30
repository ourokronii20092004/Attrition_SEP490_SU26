import { create } from "zustand";
import type { MusicTrackDto } from "@/lib/types";

export type RepeatMode = "off" | "all" | "one";

interface AudioState {
  currentTrack: MusicTrackDto | null;
  queue: MusicTrackDto[];
  isPlaying: boolean;
  volume: number;
  progress: number;
  duration: number;
  shuffle: boolean;
  repeat: RepeatMode;

  play: (track: MusicTrackDto, queue?: MusicTrackDto[]) => void;
  playAt: (index: number) => void;
  pause: () => void;
  resume: () => void;
  next: () => void;
  prev: () => void;
  toggleShuffle: () => void;
  cycleRepeat: () => void;
  setVolume: (v: number) => void;
  setProgress: (p: number) => void;
  setDuration: (d: number) => void;
  stop: () => void;
}

function indexOfTrack(queue: MusicTrackDto[], track: MusicTrackDto | null): number {
  if (!track) return -1;
  return queue.findIndex((t) => t.trackId === track.trackId);
}

export const useAudioStore = create<AudioState>((set, get) => ({
  currentTrack: null,
  queue: [],
  isPlaying: false,
  volume: 0.8,
  progress: 0,
  duration: 0,
  shuffle: false,
  repeat: "off",

  play: (track, queue) =>
    set({
      currentTrack: track,
      queue: queue ?? get().queue,
      isPlaying: true,
      progress: 0,
      duration: 0,
    }),

  playAt: (index) => {
    const { queue } = get();
    const track = queue[index];
    if (track) set({ currentTrack: track, isPlaying: true, progress: 0, duration: 0 });
  },

  pause: () => set({ isPlaying: false }),
  resume: () => set({ isPlaying: true }),

  next: () => {
    const { queue, currentTrack, shuffle, repeat } = get();
    if (!currentTrack || queue.length === 0) return;
    if (repeat === "one") { set({ progress: 0 }); return; }
    if (shuffle && queue.length > 1) {
      // Pick a random track that isn't the current one.
      const cur = indexOfTrack(queue, currentTrack);
      let r = cur;
      while (r === cur) r = Math.floor(Math.random() * queue.length);
      set({ currentTrack: queue[r], isPlaying: true, progress: 0, duration: 0 });
      return;
    }
    const idx = indexOfTrack(queue, currentTrack);
    const atEnd = idx >= queue.length - 1;
    if (atEnd && repeat === "off") { set({ isPlaying: false, progress: 0 }); return; }
    const nextIdx = (idx + 1) % queue.length;
    set({ currentTrack: queue[nextIdx], isPlaying: true, progress: 0, duration: 0 });
  },

  prev: () => {
    const { queue, currentTrack } = get();
    if (!currentTrack || queue.length === 0) return;
    // Spotify behavior: restart current track if we're past 3s; else go to previous.
    if (get().progress > 3) { set({ progress: 0 }); return; }
    const idx = indexOfTrack(queue, currentTrack);
    const prevIdx = (idx - 1 + queue.length) % queue.length;
    set({ currentTrack: queue[prevIdx], isPlaying: true, progress: 0, duration: 0 });
  },

  toggleShuffle: () => set((s) => ({ shuffle: !s.shuffle })),
  cycleRepeat: () => set((s) => ({ repeat: s.repeat === "off" ? "all" : s.repeat === "all" ? "one" : "off" })),

  setVolume: (v) => set({ volume: v }),
  setProgress: (p) => set({ progress: p }),
  setDuration: (d) => set({ duration: d }),
  stop: () => set({ currentTrack: null, isPlaying: false, progress: 0, duration: 0, queue: [] }),
}));
