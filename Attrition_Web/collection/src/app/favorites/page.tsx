'use client';

import { useEffect, useState, useCallback } from 'react';
import {
  FiHeart,
  FiPlay,
  FiPause,
  FiMusic,
  FiLogIn,
  FiClock,
} from 'react-icons/fi';
import { api } from '@/lib/api';
import { useAuth } from '@/contexts/AuthContext';
import { usePlayer, Track } from '@/contexts/PlayerContext';

const LOGIN_URL =
  'https://attrition.hault.io.vn/auth/login?returnUrl=https://collection.hault.io.vn/auth/relay';

interface FavoriteTrack extends Track {
  trackNumber?: number;
}

function formatDuration(seconds?: number): string {
  if (!seconds) return '--:--';
  const m = Math.floor(seconds / 60);
  const s = Math.floor(seconds % 60);
  return `${m}:${s.toString().padStart(2, '0')}`;
}

export default function FavoritesPage() {
  const { user, loading: authLoading } = useAuth();
  const { currentTrack, isPlaying, play, togglePlay } = usePlayer();

  const [tracks, setTracks] = useState<FavoriteTrack[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!user) {
      setLoading(false);
      return;
    }

    async function load() {
      try {
        const res = await api.get('/api/music/favorites');
        if (res?.data) {
          const items = Array.isArray(res.data)
            ? res.data
            : res.data.items || [];
          setTracks(items);
        }
      } catch {
        // API error
      } finally {
        setLoading(false);
      }
    }
    load();
  }, [user]);

  const handlePlayTrack = useCallback(
    (track: FavoriteTrack) => {
      if (currentTrack?.id === track.id && isPlaying) {
        togglePlay();
      } else {
        play(track, tracks);
      }
    },
    [currentTrack, isPlaying, play, togglePlay, tracks]
  );

  const handleRemoveFavorite = useCallback(
    async (trackId: string | number) => {
      try {
        await api.post(`/api/music/favorites/${trackId}`);
        setTracks((prev) => prev.filter((t) => t.id !== trackId));
      } catch {}
    },
    []
  );

  // Not logged in
  if (!authLoading && !user) {
    return (
      <div className="favorites-empty animate-fade-in-up">
        <div className="favorites-empty-icon">
          <FiHeart />
        </div>
        <h2>Your Favorites</h2>
        <p>
          Login to save your favorite tracks and access them anytime.
        </p>
        <a href={LOGIN_URL} className="btn btn-primary btn-lg btn-round">
          <FiLogIn /> Login to Save Favorites
        </a>
      </div>
    );
  }

  return (
    <div className="animate-fade-in">
      <div className="section-header" style={{ marginBottom: 'var(--space-xl)' }}>
        <h1 className="section-title" style={{ fontSize: '32px' }}>
          Your Favorites
        </h1>
      </div>

      {loading ? (
        <div className="track-list">
          {[1, 2, 3, 4].map((i) => (
            <div key={i} className="track-item" style={{ cursor: 'default' }}>
              <div className="skeleton" style={{ width: '24px', height: '14px' }} />
              <div>
                <div className="skeleton skeleton-text" style={{ width: '200px' }} />
                <div className="skeleton skeleton-text" style={{ width: '120px' }} />
              </div>
              <div />
              <div className="skeleton" style={{ width: '40px', height: '14px' }} />
            </div>
          ))}
        </div>
      ) : tracks.length === 0 ? (
        <div className="empty-state">
          <div className="empty-state-icon">
            <FiHeart />
          </div>
          <h3>No favorites yet</h3>
          <p>Start exploring albums and add tracks to your favorites.</p>
        </div>
      ) : (
        <div className="track-list">
          <div className="track-list-header">
            <span>#</span>
            <span>Title</span>
            <span />
            <span><FiClock /></span>
          </div>
          {tracks.map((track, index) => {
            const isCurrentTrack = currentTrack?.id === track.id;
            return (
              <div
                key={track.id}
                className={`track-item ${isCurrentTrack ? 'playing' : ''}`}
                onClick={() => handlePlayTrack(track)}
              >
                <div>
                  <span className="track-number">
                    {isCurrentTrack && isPlaying ? '♫' : index + 1}
                  </span>
                  <button className="track-play-btn">
                    {isCurrentTrack && isPlaying ? (
                      <FiPause />
                    ) : (
                      <FiPlay />
                    )}
                  </button>
                </div>
                <div className="track-title-cell">
                  <span className="track-title-text">{track.title}</span>
                  <span className="track-title-artist">
                    {track.albumTitle || 'Attrition OST'}
                  </span>
                </div>
                <div>
                  <button
                    className="track-favorite-btn favorited"
                    onClick={(e) => {
                      e.stopPropagation();
                      handleRemoveFavorite(track.id);
                    }}
                    title="Remove from favorites"
                  >
                    <FiHeart fill="currentColor" />
                  </button>
                </div>
                <span className="track-duration">
                  {formatDuration(track.duration)}
                </span>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
