"use client";

import React, { useEffect, useState } from "react";
import Link from "next/link";
import { api } from "@/lib/api";
import { usePlayer, type Track } from "@/contexts/PlayerContext";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { formatDuration, cn, assetUrl } from "@/lib/utils";
import { Music, Play, Pause, Heart } from "lucide-react";
import styles from "../collection.module.css";

interface FeaturedTrack {
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
  playCount: number;
}

interface FeaturedAlbum {
  albumId: number;
  title: string;
  coverPath: string | null;
  artists: string[];
  trackCount: number;
  newestTrackAddedAt: string;
}

interface FeaturedResponse {
  featuredTracks: FeaturedTrack[];
  newestAlbums: FeaturedAlbum[];
}

export default function FeaturedPage() {
  const { isAuthenticated, isLoading: authLoading } = useAuth();
  const { play, currentTrack, isPlaying, togglePlayPause } = usePlayer();
  const toast = useToast();

  const [tracks, setTracks] = useState<FeaturedTrack[]>([]);
  const [newestAlbums, setNewestAlbums] = useState<FeaturedAlbum[]>([]);
  const [loading, setLoading] = useState(true);
  const [favoriteIds, setFavoriteIds] = useState<number[]>([]);

  useEffect(() => {
    // Fetch featured tracks and recently updated albums
    api
      .get<FeaturedResponse>("/music/tracks/featured")
      .then((res) => {
        if (res.success && res.data) {
          setTracks(res.data.featuredTracks || []);
          setNewestAlbums(res.data.newestAlbums || []);
        }
      })
      .catch((err) => {
        toast.error("Failed to load featured content");
      })
      .finally(() => setLoading(false));
  }, [toast]);

  useEffect(() => {
    // Fetch favorite track IDs if authenticated
    if (isAuthenticated) {
      api
        .get<number[]>("/music/favorites/ids")
        .then((res) => {
          if (res.success && res.data) setFavoriteIds(res.data);
        })
        .catch(() => {});
    } else {
      setFavoriteIds([]);
    }
  }, [isAuthenticated]);

  const handlePlayTrack = (track: FeaturedTrack) => {
    const fullTrack: Track = {
      id: track.trackId,
      title: track.title,
      trackNumber: track.trackNumber,
      duration: track.duration,
      genre: track.genre,
      albumId: track.albumId,
      albumTitle: track.albumTitle,
      albumArtist: track.artists?.join(", ") || "Attrition OST",
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
      albumArtist: t.artists?.join(", ") || "Attrition OST",
      artists: t.artists || [],
      coverPath: t.coverPath || null,
      albumCoverPath: t.albumCoverPath || null,
    }));
    play(fullTrack, fullQueue);
  };

  const toggleFavorite = async (trackId: number) => {
    if (!isAuthenticated) {
      toast.error("Please sign in to favorite tracks");
      return;
    }

    const isFavorited = favoriteIds.includes(trackId);
    // Optimistic UI update
    setFavoriteIds((prev) =>
      isFavorited ? prev.filter((id) => id !== trackId) : [...prev, trackId]
    );

    try {
      const res = await api.post<{ isFavorited: boolean }>(`/music/favorites/${trackId}`);
      if (res.success) {
        toast.success(isFavorited ? "Removed from favorites" : "Added to favorites");
      } else {
        throw new Error();
      }
    } catch {
      // Revert optimistic update
      setFavoriteIds((prev) =>
        isFavorited ? [...prev, trackId] : prev.filter((id) => id !== trackId)
      );
      toast.error("Failed to update favorite status");
    }
  };

  return (
    <div>
      <div className={styles.favoritesHeader}>
        <h1 className={styles.favoritesTitle}>Featured</h1>
        <p className={styles.favoritesCount}>
          {loading ? "Loading..." : `${tracks.length} ${tracks.length === 1 ? "track" : "tracks"}`}
        </p>
      </div>

      {loading ? (
        <div className={styles.trackList} style={{ marginTop: "var(--space-8)" }}>
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
        <>
          <div className={styles.trackList} style={{ marginTop: "var(--space-8)" }}>
            {/* Header */}
            <div className={styles.trackListHeader} style={{ gridTemplateColumns: "36px 1fr 100px 48px 48px" }}>
              <span style={{ textAlign: "center" }}>#</span>
              <span>Title</span>
              <span style={{ textAlign: "right" }}>Plays</span>
              <span></span>
              <span style={{ textAlign: "right" }}>Time</span>
            </div>

            {tracks.map((track, i) => {
              const isActive = currentTrack?.id === track.trackId;
              const isFavorited = favoriteIds.includes(track.trackId);
              const coverToUse = track.coverPath || track.albumCoverPath;

              return (
                <div
                  key={track.trackId}
                  className={cn(styles.trackRow, isActive && styles.trackRowActive)}
                  style={{ gridTemplateColumns: "36px 1fr 100px 48px 48px" }}
                  onClick={() => {
                    if (isActive) togglePlayPause();
                    else handlePlayTrack(track);
                  }}
                >
                  <div>
                    <span className={styles.trackNumber}>{i + 1}</span>
                    <span className={styles.trackPlayIcon}>
                      {isActive && isPlaying ? <Pause size={14} /> : <Play size={14} />}
                    </span>
                  </div>

                  <div style={{ display: "flex", alignItems: "center", gap: "var(--space-3)", overflow: "hidden" }}>
                    <div style={{
                      width: 40, height: 40,
                      borderRadius: "var(--radius-sm)",
                      overflow: "hidden",
                      flexShrink: 0,
                      background: "var(--bg-secondary)",
                      display: "flex", alignItems: "center", justifyContent: "center",
                      border: "1px solid var(--border)"
                    }}>
                      {coverToUse ? (
                        <img src={assetUrl(coverToUse)} alt="" style={{ width: "100%", height: "100%", objectFit: "cover" }} />
                      ) : (
                        <Music size={16} className="text-muted" />
                      )}
                    </div>
                    <div className={styles.trackInfo}>
                      <span className={styles.trackTitle} style={{ fontSize: "var(--text-sm)", fontWeight: "var(--weight-semibold)" }}>
                        {track.title}
                      </span>
                      <span className={styles.trackArtist} style={{ display: "block", marginTop: "2px" }}>
                        {track.artists && track.artists.length > 0 ? track.artists.join(", ") + " · " : ""}{track.albumTitle}
                      </span>
                    </div>
                  </div>

                  <span style={{
                    fontSize: "var(--text-xs)",
                    color: "var(--text-muted)",
                    textAlign: "right",
                    fontVariantNumeric: "tabular-nums"
                  }}>
                    {track.playCount.toLocaleString()} plays
                  </span>

                  <div style={{ display: "flex", justifyContent: "center" }}>
                    <button
                      className={cn(styles.trackFavorite, isFavorited && styles.trackFavoriteActive)}
                      style={{ background: "none", border: "none", cursor: "pointer", display: "flex", alignItems: "center" }}
                      onClick={(e) => {
                        e.stopPropagation();
                        toggleFavorite(track.trackId);
                      }}
                      aria-label={isFavorited ? "Remove from favorites" : "Add to favorites"}
                    >
                      <Heart size={16} fill={isFavorited ? "currentColor" : "none"} />
                    </button>
                  </div>

                  <span className={styles.trackDuration}>
                    {formatDuration(track.duration)}
                  </span>
                </div>
              );
            })}
          </div>

          {/* Recently Updated Albums Section */}
          {newestAlbums.length > 0 && (
            <div style={{ marginTop: "var(--space-10)" }}>
              <h2 style={{ fontSize: "var(--text-xl)", fontWeight: "var(--weight-bold)", marginBottom: "var(--space-4)" }}>
                Recently Updated Albums
              </h2>
              <div className={styles.albumGrid}>
                {newestAlbums.map((album) => (
                  <Link
                    key={album.albumId}
                    href={`/albums/${album.albumId}`}
                    className={styles.albumCard}
                  >
                    <div className={styles.albumCover}>
                      {album.coverPath ? (
                        <img src={assetUrl(album.coverPath)} alt={album.title} />
                      ) : (
                        <span className={styles.albumCoverPlaceholder}>♫</span>
                      )}
                      <div className={styles.albumPlayOverlay}>
                        <span className={styles.albumPlayBtn}>▶</span>
                      </div>
                    </div>
                    <h3 className={styles.albumTitle}>{album.title}</h3>
                    <p className={styles.albumArtist}>
                      {album.artists?.join(", ") || "Attrition OST"} · {album.trackCount} tracks
                    </p>
                  </Link>
                ))}
              </div>
            </div>
          )}
        </>
      ) : (
        <div className="empty-state">
          <span className="empty-state-icon">⭐</span>
          <h3>No featured tracks yet</h3>
          <p>Soundtracks with play activity will appear here.</p>
        </div>
      )}
    </div>
  );
}
