"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { api } from "@/lib/api";
import { usePlayer, type Track } from "@/contexts/PlayerContext";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { formatDuration, cn, assetUrl } from "@/lib/utils";
import styles from "../collection.module.css";

interface FavoriteTrack {
  trackId: number;
  title: string;
  trackNumber: number;
  duration: number;
  genre: string | null;
  albumId: number;
  albumTitle: string;
  albumCoverPath: string | null;
  artists?: string[];
  coverPath?: string | null;
}

export default function FavoritesPage() {
  const { isAuthenticated, isLoading: authLoading } = useAuth();
  const { play, currentTrack, isPlaying, togglePlayPause } = usePlayer();
  const toast = useToast();

  const [tracks, setTracks] = useState<FavoriteTrack[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!isAuthenticated) {
      setLoading(false);
      return;
    }

    api
      .get<FavoriteTrack[]>("/music/favorites")
      .then((res) => {
        if (res.success && res.data) setTracks(res.data);
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [isAuthenticated]);

  const handlePlayTrack = (track: FavoriteTrack) => {
    const fullTrack: Track = {
      id: track.trackId,
      title: track.title,
      trackNumber: track.trackNumber,
      duration: track.duration,
      genre: track.genre,
      albumId: track.albumId,
      albumTitle: track.albumTitle,
      albumArtist: track.artists?.join(", ") || "",
      artists: track.artists || [],
      coverPath: track.coverPath || null,
      albumCoverPath: track.albumCoverPath || null,
    };
    const fullQueue: Track[] = tracks.map((t) => ({
      id: t.trackId,
      title: t.title,
      trackNumber: t.trackNumber,
      duration: t.duration,
      genre: t.genre,
      albumId: t.albumId,
      albumTitle: t.albumTitle,
      albumArtist: t.artists?.join(", ") || "",
      artists: t.artists || [],
      coverPath: t.coverPath || null,
      albumCoverPath: t.albumCoverPath || null,
    }));
    play(fullTrack, fullQueue);
  };

  const removeFavorite = async (trackId: number) => {
    // Optimistic UI update
    const removed = tracks.find((t) => t.trackId === trackId);
    setTracks((prev) => prev.filter((t) => t.trackId !== trackId));

    try {
      await api.post(`/music/favorites/${trackId}`);
      toast.success("Removed from favorites");
    } catch {
      if (removed) setTracks((prev) => [...prev, removed]);
      toast.error("Failed to remove from favorites");
    }
  };

  if (!authLoading && !isAuthenticated) {
    return (
      <div className="empty-state">
        <span className="empty-state-icon">♥</span>
        <h3>Sign in to see your favorites</h3>
        <p>Save your favorite tracks and access them anytime.</p>
      </div>
    );
  }

  return (
    <div>
      <div className={styles.favoritesHeader}>
        <h1 className={styles.favoritesTitle}>Favorites</h1>
        <p className={styles.favoritesCount}>
          {tracks.length} {tracks.length === 1 ? "track" : "tracks"}
        </p>
      </div>

      {loading ? (
        <div className={styles.trackList}>
          {Array.from({ length: 5 }).map((_, i) => (
            <div key={i} className={styles.trackRow}>
              <div className="skeleton" style={{ width: 24, height: 14, borderRadius: 4 }} />
              <div>
                <div className="skeleton skeleton-text" style={{ width: "60%" }} />
              </div>
              <div />
              <div className="skeleton skeleton-text" style={{ width: 30 }} />
            </div>
          ))}
        </div>
      ) : tracks.length > 0 ? (
        <div className={styles.trackList}>
          {tracks.map((track, i) => {
            const isActive = currentTrack?.id === track.trackId;
            return (
              <div
                key={track.trackId}
                className={cn(styles.trackRow, isActive && styles.trackRowActive)}
                onClick={() => {
                  if (isActive) togglePlayPause();
                  else handlePlayTrack(track);
                }}
              >
                <div>
                  <span className={styles.trackNumber}>{i + 1}</span>
                  <span className={styles.trackPlayIcon}>
                    {isActive && isPlaying ? "⏸" : "▶"}
                  </span>
                </div>
                <div className={styles.trackInfo}>
                  <span className={styles.trackTitle}>{track.title}</span>
                  <span className={styles.trackArtist}>
                    {track.artists && track.artists.length > 0 ? track.artists.join(", ") + " · " : ""}{track.albumTitle}
                  </span>
                </div>
                <button
                  className={cn(styles.trackFavorite, styles.trackFavoriteActive)}
                  onClick={(e) => {
                    e.stopPropagation();
                    removeFavorite(track.trackId);
                  }}
                  aria-label="Remove from favorites"
                >
                  ♥
                </button>
                <span className={styles.trackDuration}>
                  {formatDuration(track.duration)}
                </span>
              </div>
            );
          })}
        </div>
      ) : (
        <div className="empty-state">
          <span className="empty-state-icon">♥</span>
          <h3>No favorites yet</h3>
          <p>Explore albums and click the heart icon to save tracks you love.</p>
          <Link href="/collection/albums" className="btn btn-primary btn-md" style={{ marginTop: "var(--space-4)" }}>
            Browse Albums
          </Link>
        </div>
      )}
    </div>
  );
}
