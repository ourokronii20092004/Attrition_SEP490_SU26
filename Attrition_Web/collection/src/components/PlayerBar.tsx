'use client';

import {
  FiPlay,
  FiPause,
  FiSkipBack,
  FiSkipForward,
  FiVolume2,
  FiVolumeX,
  FiMusic,
} from 'react-icons/fi';
import { usePlayer } from '@/contexts/PlayerContext';
import { useCallback, useRef, useState } from 'react';

function formatTime(seconds: number): string {
  if (!seconds || isNaN(seconds)) return '0:00';
  const m = Math.floor(seconds / 60);
  const s = Math.floor(seconds % 60);
  return `${m}:${s.toString().padStart(2, '0')}`;
}

export default function PlayerBar() {
  const {
    currentTrack,
    isPlaying,
    volume,
    progress,
    duration,
    togglePlay,
    next,
    previous,
    seek,
    setVolume,
  } = usePlayer();

  const progressRef = useRef<HTMLDivElement>(null);
  const volumeRef = useRef<HTMLDivElement>(null);
  const [prevVolume, setPrevVolume] = useState(0.7);

  const handleProgressClick = useCallback(
    (e: React.MouseEvent<HTMLDivElement>) => {
      if (!progressRef.current || !duration) return;
      const rect = progressRef.current.getBoundingClientRect();
      const x = e.clientX - rect.left;
      const pct = x / rect.width;
      seek(pct * duration);
    },
    [duration, seek]
  );

  const handleVolumeClick = useCallback(
    (e: React.MouseEvent<HTMLDivElement>) => {
      if (!volumeRef.current) return;
      const rect = volumeRef.current.getBoundingClientRect();
      const x = e.clientX - rect.left;
      const pct = Math.max(0, Math.min(1, x / rect.width));
      setVolume(pct);
    },
    [setVolume]
  );

  const toggleMute = useCallback(() => {
    if (volume > 0) {
      setPrevVolume(volume);
      setVolume(0);
    } else {
      setVolume(prevVolume || 0.7);
    }
  }, [volume, prevVolume, setVolume]);

  const progressPct = duration > 0 ? (progress / duration) * 100 : 0;
  const volumePct = volume * 100;

  if (!currentTrack) {
    return (
      <div className="player-bar">
        <div className="player-empty">Select a track to start listening</div>
      </div>
    );
  }

  const coverUrl = currentTrack.albumCoverPath
    ? `/uploads/${currentTrack.albumCoverPath}`
    : null;

  return (
    <div className="player-bar">
      {/* Track info */}
      <div className="player-track-info">
        <div className="player-album-art">
          {coverUrl ? (
            <img src={coverUrl} alt={currentTrack.albumTitle || ''} />
          ) : (
            <FiMusic className="player-art-placeholder" />
          )}
        </div>
        <div className="player-track-text">
          <div className="player-track-title">{currentTrack.title}</div>
          <div className="player-track-artist">
            {currentTrack.artistName || currentTrack.albumTitle || 'Attrition OST'}
          </div>
        </div>
      </div>

      {/* Controls */}
      <div className="player-controls">
        <div className="player-buttons">
          <button className="player-btn" onClick={previous} title="Previous">
            <FiSkipBack />
          </button>
          <button
            className="player-btn player-btn-play"
            onClick={togglePlay}
            title={isPlaying ? 'Pause' : 'Play'}
          >
            {isPlaying ? <FiPause /> : <FiPlay />}
          </button>
          <button className="player-btn" onClick={next} title="Next">
            <FiSkipForward />
          </button>
        </div>
        <div className="player-progress-bar">
          <span className="player-time">{formatTime(progress)}</span>
          <div
            className="player-progress-track"
            ref={progressRef}
            onClick={handleProgressClick}
          >
            <div
              className="player-progress-fill"
              style={{ width: `${progressPct}%` }}
            />
          </div>
          <span className="player-time">{formatTime(duration)}</span>
        </div>
      </div>

      {/* Volume */}
      <div className="player-volume">
        <button className="player-volume-btn" onClick={toggleMute}>
          {volume === 0 ? <FiVolumeX /> : <FiVolume2 />}
        </button>
        <div
          className="player-volume-slider"
          ref={volumeRef}
          onClick={handleVolumeClick}
        >
          <div
            className="player-volume-fill"
            style={{ width: `${volumePct}%` }}
          />
        </div>
      </div>
    </div>
  );
}
