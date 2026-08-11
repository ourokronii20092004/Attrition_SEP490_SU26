"use client";

import { useEffect, useRef, useState } from "react";
import { useAudioStore } from "@/lib/stores/audio-store";
import { getStreamUrl, musicApi } from "@/lib/api/music";
import { useFavorites } from "./use-favorites";

/**
 * Shared audio-engine wiring for the public bottom player and the admin sidebar
 * player. Owns the <audio> element lifecycle, playback sync with the zustand
 * store, scrubbing, volume, and the 10s play-count rule — the UI above only maps
 * buttons to the returned callbacks, so the two players can't drift apart.
 *
 * Each consumer renders its own <audio> element and forwards the returned
 * on* handlers to it.
 */
export function useAudioEngine() {
  const {
    currentTrack, isPlaying, volume, progress, duration, repeat,
    pause, resume, next, setProgress, setDuration, setVolume,
  } = useAudioStore();

  const audioRef = useRef<HTMLAudioElement>(null);
  const playRecorded = useRef<number | null>(null);
  const wasPlaying = useRef(false);
  const loadedTrackId = useRef<number | null>(null);

  const [scrubbing, setScrubbing] = useState(false);
  const [scrubValue, setScrubValue] = useState(0);
  const [muted, setMuted] = useState(false);

  const { isFavorite, toggle: toggleFav, canFavorite } = useFavorites();

  // We key off the track id (not audio.src, which resolves to an absolute URL and never
  // string-equals a relative stream path — the old comparison reloaded on every render,
  // resetting playback to 0 on each play/pause and scrub).
  useEffect(() => {
    // Clear the loaded-track marker whenever the track is cleared, even if the <audio>
    // element has already unmounted (audioRef.current is null once we render null). The
    // old order bailed on `if (!audio) return` first, so the marker survived a stop and
    // the next play of the SAME track skipped assigning src to the fresh element — it
    // silently errored and only a different track (different id) recovered.
    if (!currentTrack) {
      loadedTrackId.current = null;
      playRecorded.current = null;
      const audio = audioRef.current;
      if (audio) { audio.pause(); audio.removeAttribute("src"); audio.load(); }
      return;
    }
    const audio = audioRef.current;
    if (!audio) return;
    if (loadedTrackId.current !== currentTrack.trackId) {
      audio.src = getStreamUrl(currentTrack.trackId);
      audio.load();
      loadedTrackId.current = currentTrack.trackId;
    }
  }, [currentTrack]);

  // Play counts are NOT recorded here — a listen only counts after 10s of playback (see
  // maybeRecordPlay), not the instant a track starts.
  useEffect(() => {
    const audio = audioRef.current;
    if (!audio || !currentTrack) return;
    if (isPlaying && !scrubbing) {
      audio.play().catch(() => {});
    } else if (!isPlaying) {
      audio.pause();
    }
  }, [currentTrack, isPlaying, scrubbing]);

  useEffect(() => {
    const audio = audioRef.current;
    if (audio) audio.volume = muted ? 0 : volume;
  }, [volume, muted]);

  // Store-driven seeks: some actions (prev/next in repeat-one, "restart current") only zero the
  // store's `progress` without touching the element. When progress is reset to ~0 but the audio
  // is still mid-track, snap the element back so the two don't drift out of sync.
  useEffect(() => {
    const audio = audioRef.current;
    if (!audio || scrubbing) return;
    if (progress === 0 && audio.currentTime > 0.5) {
      audio.currentTime = 0;
      if (isPlaying) audio.play().catch(() => {});
    }
  }, [progress, scrubbing, isPlaying]);

  // A play counts once the listener reaches 10s of the track — or, for tracks shorter than 10s,
  // once they're essentially finished. Deduped per track via playRecorded, so scrubbing back and
  // forth or repeat-one can't inflate the count.
  const maybeRecordPlay = () => {
    const audio = audioRef.current;
    const track = currentTrack;
    if (!audio || !track || playRecorded.current === track.trackId) return;
    const dur = audio.duration;
    const threshold = Number.isFinite(dur) && dur > 0 && dur < 10 ? Math.max(0.1, dur - 0.5) : 10;
    if (audio.currentTime >= threshold) {
      musicApi.recordPlay(track.trackId).catch(() => {});
      playRecorded.current = track.trackId;
    }
  };

  const onTimeUpdate = () => {
    const audio = audioRef.current;
    if (audio && !scrubbing) setProgress(audio.currentTime);
    maybeRecordPlay();
  };
  const onLoadedMetadata = () => {
    const audio = audioRef.current;
    if (audio) setDuration(audio.duration);
  };
  // On track end: repeat-one replays the same element from 0 (the store's `next` only zeroes
  // progress for that mode and would otherwise leave the audio stopped). Everything else advances.
  const onEnded = () => {
    const audio = audioRef.current;
    maybeRecordPlay(); // count short tracks that finish before hitting the 10s mark
    if (repeat === "one" && audio) {
      audio.currentTime = 0;
      audio.play().catch(() => {});
      return;
    }
    next();
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

  return {
    audioRef,
    volume,
    muted, setMuted,
    displayTime, pct, duration,
    toggle, onVolume,
    onTimeUpdate, onLoadedMetadata, onEnded,
    beginScrub, moveScrub, commitScrub,
    liked, canFavorite, toggleFav,
  };
}
