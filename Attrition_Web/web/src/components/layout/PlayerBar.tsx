"use client";

import React, { useRef, useState } from "react";
import { usePlayer } from "@/contexts/PlayerContext";
import { cn, formatDuration, assetUrl } from "@/lib/utils";
import {
  Music,
  Play,
  Pause,
  SkipForward,
  SkipBack,
  Shuffle,
  Repeat,
  Repeat1,
  Volume2,
  Volume1,
  VolumeX,
} from "lucide-react";
import styles from "./PlayerBar.module.css";

export default function PlayerBar() {
  const {
    currentTrack,
    isPlaying,
    progress,
    currentTime,
    duration,
    volume,
    isMuted,
    isShuffled,
    repeatMode,
    togglePlayPause,
    next,
    previous,
    seek,
    pause,
    resume,
    setVolume,
    toggleMute,
    toggleShuffle,
    cycleRepeat,
  } = usePlayer();

  const progressRef = useRef<HTMLDivElement>(null);
  const [dragProgress, setDragProgress] = useState<number | null>(null);

  if (!currentTrack) {
    return null;
  }

  const getSeekPosition = (e: MouseEvent | React.MouseEvent) => {
    const bar = progressRef.current;
    if (!bar) return null;
    const rect = bar.getBoundingClientRect();
    return Math.max(0, Math.min(1, (e.clientX - rect.left) / rect.width));
  };

  const handleProgressMouseDown = (e: React.MouseEvent<HTMLDivElement>) => {
    const pos = getSeekPosition(e);
    if (pos !== null) {
      setDragProgress(pos);
      pause();
    }

    const onMouseMove = (moveEvent: MouseEvent) => {
      const movePos = getSeekPosition(moveEvent);
      if (movePos !== null) {
        setDragProgress(movePos);
      }
    };

    const onMouseUp = (upEvent: MouseEvent) => {
      const upPos = getSeekPosition(upEvent);
      const finalPos = upPos !== null ? upPos : pos;
      if (finalPos !== null) {
        seek(finalPos * duration);
        resume();
      }
      setDragProgress(null);
      document.removeEventListener("mousemove", onMouseMove);
      document.removeEventListener("mouseup", onMouseUp);
    };

    document.addEventListener("mousemove", onMouseMove);
    document.addEventListener("mouseup", onMouseUp);
  };

  const handleVolumeChange = (e: React.MouseEvent<HTMLDivElement>) => {
    const rect = e.currentTarget.getBoundingClientRect();
    const x = (e.clientX - rect.left) / rect.width;
    setVolume(Math.max(0, Math.min(1, x)));
  };

  const displayProgress = dragProgress !== null ? dragProgress : progress;
  const displayTime = dragProgress !== null ? dragProgress * duration : currentTime;

  return (
    <div className={styles.playerBar} id="persistent-player-bar">
      {/* Track Info */}
      <div className={styles.playerTrackInfo}>
        <div className={styles.playerArt}>
          {currentTrack.coverPath ? (
            <img src={assetUrl(currentTrack.coverPath)} alt={currentTrack.title} />
          ) : (
            <span>
              <Music size={16} />
            </span>
          )}
        </div>
        <div className={styles.playerTrackMeta}>
          <span className={styles.playerTrackTitle}>{currentTrack.title}</span>
          <span className={styles.playerTrackArtist}>{currentTrack.albumArtist}</span>
        </div>
      </div>

      {/* Controls */}
      <div className={styles.playerControls}>
        <div className={styles.playerButtons}>
          <button
            className={cn(styles.controlBtn, isShuffled && styles.controlBtnActive)}
            onClick={toggleShuffle}
            title="Shuffle"
          >
            <Shuffle size={16} />
          </button>
          <button className={styles.controlBtn} onClick={previous} title="Previous">
            <SkipBack size={16} />
          </button>
          <button
            className={cn(styles.controlBtn, styles.playPauseBtn)}
            onClick={togglePlayPause}
            title={isPlaying ? "Pause" : "Play"}
          >
            {isPlaying ? <Pause size={18} /> : <Play size={18} />}
          </button>
          <button className={styles.controlBtn} onClick={next} title="Next">
            <SkipForward size={16} />
          </button>
          <button
            className={cn(styles.controlBtn, repeatMode !== "none" && styles.controlBtnActive)}
            onClick={cycleRepeat}
            title={`Repeat: ${repeatMode}`}
          >
            {repeatMode === "one" ? <Repeat1 size={16} /> : <Repeat size={16} />}
          </button>
        </div>

        {/* Progress Bar */}
        <div className={styles.progressRow}>
          <span className={styles.timeLabel}>{formatDuration(displayTime)}</span>
          <div
            className={styles.progressBar}
            onMouseDown={handleProgressMouseDown}
            ref={progressRef}
            data-dragging={dragProgress !== null || undefined}
          >
            <div
              className={styles.progressFill}
              style={{ width: `${displayProgress * 100}%` }}
            />
          </div>
          <span className={styles.timeLabel}>{formatDuration(duration)}</span>
        </div>
      </div>

      {/* Volume */}
      <div className={styles.playerVolume}>
        <button
          className={styles.controlBtn}
          onClick={toggleMute}
          title={isMuted ? "Unmute" : "Mute"}
        >
          {isMuted || volume === 0 ? (
            <VolumeX size={16} />
          ) : volume < 0.5 ? (
            <Volume1 size={16} />
          ) : (
            <Volume2 size={16} />
          )}
        </button>
        <div className={styles.volumeBar} onClick={handleVolumeChange}>
          <div
            className={styles.volumeFill}
            style={{ width: `${(isMuted ? 0 : volume) * 100}%` }}
          />
        </div>
      </div>
    </div>
  );
}
