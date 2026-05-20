'use client';

import { useEffect, useState, useCallback } from 'react';
import { useParams } from 'next/navigation';
import Link from 'next/link';
import {
  FiPlay,
  FiPause,
  FiShuffle,
  FiHeart,
  FiMusic,
  FiClock,
  FiChevronLeft,
} from 'react-icons/fi';
import { api } from '@/lib/api';
import { usePlayer, Track } from '@/contexts/PlayerContext';
import { useAuth } from '@/contexts/AuthContext';

interface AlbumDetail {
  id: string | number;
  title: string;
  artistName?: string;
  description?: string;
  coverImagePath?: string | null;
  tracks: TrackItem[];
}

interface TrackItem extends Track {
  trackNumber?: number;
  playCount?: number;
}

function formatDuration(seconds?: number): string {
  if (!seconds) return '--:--';
  const m = Math.floor(seconds / 60);
  const s = Math.floor(seconds % 60);
  return `${m}:${s.toString().padStart(2, '0')}`;
}

function formatTotalDuration(seconds: number): string {
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  if (h > 0) return `${h} hr ${m} min`;
  return `${m} min`;
}

export default function AlbumDetailPage() {
  const params = useParams();
  const albumId = params.id as string;

  const [album, setAlbum] = useState<AlbumDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [favorites, setFavorites] = useState<Set<string | number>>(new Set());

  const { currentTrack, isPlaying, play, togglePlay } = usePlayer();
  const { user } = useAuth();

  useEffect(() => {
    async function load() {
      try {
        const res = await api.get(`/api/music/albums/${albumId}`);
        if (res?.data) {
          const data = res.data;
          setAlbum({
            ...data,
            tracks: (data.tracks || []).map((t: any, i: number) => ({
              ...t,
              albumTitle: data.title,
              albumCoverPath: data.coverImagePath,
              trackNumber: t.trackNumber || i + 1,
            })),
          });
        }
      } catch {
        // album not found or API error
      } finally {
        setLoading(false);
      }
    }
    load();
  }, [albumId]);

  // Load favorites
  useEffect(() => {
    if (!user) return;
    async function loadFavorites() {
      try {
        const res = await api.get('/api/music/favorites');
        if (res?.data) {
          const items = Array.isArray(res.data)
            ? res.data
            : res.data.items || [];
          setFavorites(new Set(items.map((f: any) => f.trackId || f.id)));
        }
      } catch {}
    }
    loadFavorites();
  }, [user]);

  const handlePlayAll = useCallback(() => {
    if (!album || album.tracks.length === 0) return;
    play(album.tracks[0], album.tracks);
  }, [album, play]);

  const handleShuffle = useCallback(() => {
    if (!album || album.tracks.length === 0) return;
    const shuffled = [...album.tracks].sort(() => Math.random() - 0.5);
    play(shuffled[0], shuffled);
  }, [album, play]);

  const handlePlayTrack = useCallback(
    (track: TrackItem) => {
      if (!album) return;
      if (currentTrack?.id === track.id && isPlaying) {
        togglePlay();
      } else {
        play(track, album.tracks);
      }
    },
    [album, currentTrack, isPlaying, play, togglePlay]
  );

  const handleToggleFavorite = useCallback(
    async (trackId: string | number) => {
      if (!user) return;
      try {
        await api.post(`/api/music/favorites/${trackId}`);
        setFavorites((prev) => {
          const next = new Set(prev);
          if (next.has(trackId)) {
            next.delete(trackId);
          } else {
            next.add(trackId);
          }
          return next;
        });
      } catch {}
    },
    [user]
  );

  if (loading) {
    return (
      <div>
        <div className="album-detail-header">
          <div className="album-detail-cover">
            <div className="skeleton" style={{ width: '100%', height: '100%' }} />
          </div>
          <div className="album-detail-info" style={{ flex: 1 }}>
            <div className="skeleton skeleton-text" style={{ width: '80px' }} />
            <div className="skeleton skeleton-title" style={{ width: '60%' }} />
            <div className="skeleton skeleton-text" style={{ width: '200px' }} />
          </div>
        </div>
      </div>
    );
  }

  if (!album) {
    return (
      <div className="empty-state">
        <div className="empty-state-icon">
          <FiMusic />
        </div>
        <h3>Album not found</h3>
        <p>This album may have been removed or doesn&apos;t exist.</p>
        <Link href="/" className="btn btn-secondary" style={{ marginTop: 'var(--space-lg)' }}>
          Back to Home
        </Link>
      </div>
    );
  }

  const totalDuration = album.tracks.reduce(
    (acc, t) => acc + (t.duration || 0),
    0
  );

  const coverUrl = album.coverImagePath
    ? `/uploads/${album.coverImagePath}`
    : null;

  return (
    <div className="animate-fade-in">
      {/* Breadcrumb */}
      <div className="breadcrumb">
        <Link href="/">Home</Link>
        <span className="sep">/</span>
        <Link href="/albums">Albums</Link>
        <span className="sep">/</span>
        <span>{album.title}</span>
      </div>

      {/* Header */}
      <div className="album-detail-header">
        <div className="album-detail-cover">
          {coverUrl ? (
            <img src={coverUrl} alt={album.title} />
          ) : (
            <FiMusic className="album-detail-cover-placeholder" />
          )}
        </div>
        <div className="album-detail-info">
          <div className="album-detail-label">Album</div>
          <h1 className="album-detail-title">{album.title}</h1>
          <div className="album-detail-meta">
            <span>{album.artistName || 'Attrition OST'}</span>
            <span className="dot" />
            <span>{album.tracks.length} tracks</span>
            {totalDuration > 0 && (
              <>
                <span className="dot" />
                <span>{formatTotalDuration(totalDuration)}</span>
              </>
            )}
          </div>
        </div>
      </div>

      {/* Actions */}
      <div className="album-detail-actions">
        <button className="btn btn-primary btn-lg btn-round" onClick={handlePlayAll}>
          <FiPlay /> Play All
        </button>
        <button className="btn btn-secondary btn-round" onClick={handleShuffle}>
          <FiShuffle /> Shuffle
        </button>
      </div>

      {/* Track list */}
      {album.tracks.length === 0 ? (
        <div className="empty-state">
          <h3>No tracks yet</h3>
          <p>Tracks haven&apos;t been added to this album yet.</p>
        </div>
      ) : (
        <div className="track-list">
          <div className="track-list-header">
            <span>#</span>
            <span>Title</span>
            <span />
            <span><FiClock /></span>
          </div>
          {album.tracks.map((track) => {
            const isCurrentTrack = currentTrack?.id === track.id;
            return (
              <div
                key={track.id}
                className={`track-item ${isCurrentTrack ? 'playing' : ''}`}
                onClick={() => handlePlayTrack(track)}
              >
                <div>
                  <span className="track-number">
                    {isCurrentTrack && isPlaying ? '♫' : track.trackNumber}
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
                    {track.artistName || album.artistName || 'Attrition OST'}
                  </span>
                </div>
                <div>
                  {user && (
                    <button
                      className={`track-favorite-btn ${
                        favorites.has(track.id) ? 'favorited' : ''
                      }`}
                      onClick={(e) => {
                        e.stopPropagation();
                        handleToggleFavorite(track.id);
                      }}
                    >
                      <FiHeart
                        fill={favorites.has(track.id) ? 'currentColor' : 'none'}
                      />
                    </button>
                  )}
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
