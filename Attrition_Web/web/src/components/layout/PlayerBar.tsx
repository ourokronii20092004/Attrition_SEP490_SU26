"use client";

import React, { useRef, useState, useEffect } from "react";
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
  ListMusic,
  Trash2
} from "lucide-react";
import styles from "./PlayerBar.module.css";

export default function PlayerBar() {
  const {
    currentTrack,
    queue,
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
    clearQueue,
    play
  } = usePlayer();

  const progressRef = useRef<HTMLDivElement>(null);
  const [dragProgress, setDragProgress] = useState<number | null>(null);
  const [isQueueOpen, setIsQueueOpen] = useState(false);
  const queueRef = useRef<HTMLDivElement>(null);

  // Close queue when clicking outside
  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (queueRef.current && !queueRef.current.contains(event.target as Node)) {
        setIsQueueOpen(false);
      }
    }
    if (isQueueOpen) {
      document.addEventListener("mousedown", handleClickOutside);
    }
    return () => {
      document.removeEventListener("mousedown", handleClickOutside);
    };
  }, [isQueueOpen]);

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

  // Find index of current track in queue to show upcoming tracks
  const currentIndex = queue.findIndex(t => t.id === currentTrack.id);
  const upcomingTracks = queue.slice(currentIndex + 1);

  return (
    <div className={styles.playerBar} id="persistent-player-bar">
      {/* Track Info */}
      <div className={styles.playerTrackInfo}>
        <div className={styles.playerArtWrapper}>
          <div className={cn(styles.playerArt, isPlaying ? styles.playerArtSpinning : cn(styles.playerArtSpinning, styles.playerArtPaused))}>
            {currentTrack.coverPath ? (
              <img src={assetUrl(currentTrack.coverPath)} alt={currentTrack.title} />
            ) : (
              <span>
                <Music size={16} />
              </span>
            )}
          </div>
        </div>
        <div className={styles.playerTrackMeta}>
          <div className={cn(currentTrack.title.length > 25 && styles.marquee)}>
            <span className={styles.playerTrackTitle}>{currentTrack.title}</span>
          </div>
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
            <SkipBack size={18} fill="currentColor" />
          </button>
          <button
            className={cn(styles.controlBtn, styles.playPauseBtn)}
            onClick={togglePlayPause}
            title={isPlaying ? "Pause" : "Play"}
          >
            {isPlaying ? <Pause size={20} fill="currentColor" /> : <Play size={20} fill="currentColor" style={{ marginLeft: '2px' }} />}
          </button>
          <button className={styles.controlBtn} onClick={next} title="Next">
            <SkipForward size={18} fill="currentColor" />
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

      {/* Volume & Extras */}
      <div className={styles.playerExtras}>
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

        {/* Queue Toggle */}
        <div className={styles.queueBtn} ref={queueRef}>
          <button
            className={cn(styles.controlBtn, isQueueOpen && styles.controlBtnActive)}
            onClick={() => setIsQueueOpen(!isQueueOpen)}
            title="Up Next"
          >
            <ListMusic size={18} />
            {upcomingTracks.length > 0 && (
              <span className={styles.queueBadge}>{upcomingTracks.length}</span>
            )}
          </button>

          {/* Queue Drawer */}
          {isQueueOpen && (
            <div className={styles.queueDrawer}>
              <div className={styles.queueHeader}>
                <div className={styles.queueTitle}>
                  <ListMusic size={18} /> Up Next
                </div>
                {queue.length > 0 && (
                  <button className={styles.queueClearBtn} onClick={clearQueue} title="Clear Queue">
                    <Trash2 size={14} />
                  </button>
                )}
              </div>
              <div className={styles.queueList}>
                {queue.length === 0 ? (
                  <div className={styles.queueEmpty}>
                    <Music size={32} />
                    <span>No tracks in queue</span>
                  </div>
                ) : (
                  queue.map((track, idx) => {
                    const isActive = currentTrack.id === track.id && idx === currentIndex;
                    return (
                      <div
                        key={`${track.id}-${idx}`}
                        className={cn(styles.queueItem, isActive && styles.queueItemActive)}
                        onClick={() => play(track, queue)}
                      >
                        <div className={styles.queueItemNum}>
                          {isActive && isPlaying ? (
                            <Music size={14} className="animate-pulse" />
                          ) : (
                            idx + 1
                          )}
                        </div>
                        <div style={{ minWidth: 0 }}>
                          <div className={styles.queueItemTitle}>{track.title}</div>
                          <div className={styles.queueItemArtist}>{track.albumArtist}</div>
                        </div>
                        <div className={styles.queueItemDuration}>
                          {formatDuration(track.duration)}
                        </div>
                      </div>
                    );
                  })
                )}
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
