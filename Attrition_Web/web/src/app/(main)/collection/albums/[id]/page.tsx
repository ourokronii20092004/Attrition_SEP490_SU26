"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import { api } from "@/lib/api";
import { usePlayer, type Track } from "@/contexts/PlayerContext";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { formatDuration, formatDurationLong, cn, assetUrl } from "@/lib/utils";
import styles from "../../collection.module.css";

interface AlbumDetail {
  albumId: number;
  title: string;
  artists: string[];
  description: string;
  coverPath: string | null;
  albumType: string;
  releaseDate: string;
  tracks: TrackItem[];
}

interface TrackItem {
  trackId: number;
  title: string;
  trackNumber: number;
  duration: number;
  genre: string | null;
  artists?: string[];
  coverPath?: string | null;
}

export default function AlbumDetailPage() {
  const params = useParams();
  const albumId = params.id as string;
  const { play, currentTrack, isPlaying, togglePlayPause } = usePlayer();
  const { isAuthenticated } = useAuth();
  const toast = useToast();

  const [album, setAlbum] = useState<AlbumDetail | null>(null);
  const [favorites, setFavorites] = useState<Set<number>>(new Set());
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api
      .get<AlbumDetail>(`/music/albums/${albumId}`)
      .then((res) => {
        if (res.success && res.data) setAlbum(res.data);
      })
      .catch(() => {})
      .finally(() => setLoading(false));

    if (isAuthenticated) {
      api
        .get<number[]>("/music/favorites/ids")
        .then((res) => {
          if (res.success && res.data) setFavorites(new Set(res.data));
        })
        .catch(() => {});
    }
  }, [albumId, isAuthenticated]);

  const handlePlayTrack = (track: TrackItem) => {
    if (!album) return;
    const fullTrack: Track = {
      id: track.trackId,
      title: track.title,
      trackNumber: track.trackNumber,
      duration: track.duration,
      genre: track.genre,
      albumId: album.albumId,
      albumTitle: album.title,
      albumArtist: album.artists?.join(", ") || "Attrition OST",
      albumArtists: album.artists || [],
      artists: track.artists || [],
      coverPath: track.coverPath || null,
      albumCoverPath: album.coverPath || null,
    };
    const fullQueue: Track[] = album.tracks.map((t) => ({
      id: t.trackId,
      title: t.title,
      trackNumber: t.trackNumber,
      duration: t.duration,
      genre: t.genre,
      albumId: album.albumId,
      albumTitle: album.title,
      albumArtist: album.artists?.join(", ") || "Attrition OST",
      albumArtists: album.artists || [],
      artists: t.artists || [],
      coverPath: t.coverPath || null,
      albumCoverPath: album.coverPath || null,
    }));
    play(fullTrack, fullQueue);
  };

  const handlePlayAll = () => {
    if (album && album.tracks.length > 0) {
      handlePlayTrack(album.tracks[0]);
    }
  };

  const toggleFavorite = async (trackId: number) => {
    if (!isAuthenticated) {
      toast.error("Sign in to save favorites");
      return;
    }
    // Optimistic UI
    const wasFavorited = favorites.has(trackId);
    setFavorites((prev) => {
      const next = new Set(prev);
      if (wasFavorited) next.delete(trackId);
      else next.add(trackId);
      return next;
    });

    try {
      await api.post(`/music/favorites/${trackId}`);
      toast.success(wasFavorited ? "Removed from favorites" : "Added to favorites");
    } catch {
      // Rollback
      setFavorites((prev) => {
        const next = new Set(prev);
        if (wasFavorited) next.add(trackId);
        else next.delete(trackId);
        return next;
      });
      toast.error("Failed to update favorites");
    }
  };

  if (loading) {
    return (
      <div>
        <div className={styles.albumHeader}>
          <div className={`${styles.albumHeaderCover} skeleton`} />
          <div className={styles.albumHeaderInfo}>
            <div className="skeleton skeleton-text" style={{ width: "60px" }} />
            <div className="skeleton skeleton-heading" style={{ width: "200px" }} />
            <div className="skeleton skeleton-text" style={{ width: "120px" }} />
          </div>
        </div>
      </div>
    );
  }

  if (!album) {
    return (
      <div className="empty-state">
        <span className="empty-state-icon">🎵</span>
        <h3>Album not found</h3>
        <p>This album doesn&apos;t exist or has been removed.</p>
      </div>
    );
  }

  const totalDuration = album.tracks.reduce((sum, t) => sum + t.duration, 0);

  return (
    <div>
      {/* Album header */}
      <div className={styles.albumHeader}>
        <div className={styles.albumHeaderCover}>
          {album.coverPath ? (
            // eslint-disable-next-line @next/next/no-img-element
            <img src={assetUrl(album.coverPath)} alt={album.title} />
          ) : (
            <span className={styles.albumCoverPlaceholder}>♫</span>
          )}
        </div>
        <div className={styles.albumHeaderInfo}>
          <span className={styles.albumHeaderType}>{album.albumType || "Album"}</span>
          <h1 className={styles.albumHeaderTitle}>{album.title}</h1>
          <p className={styles.albumHeaderMeta}>
            {album.artists?.join(", ") || "Attrition OST"} · {album.tracks.length} tracks · {formatDurationLong(totalDuration)}
          </p>
          <div className={styles.albumActions}>
            <button className="btn btn-primary btn-md" onClick={handlePlayAll}>
              ▶ Play All
            </button>
          </div>
        </div>
      </div>

      {/* Track list */}
      <div className={styles.trackListHeader}>
        <span>#</span>
        <span>Title</span>
        <span></span>
        <span>Duration</span>
      </div>
      <div className={styles.trackList}>
        {album.tracks.map((track) => {
          const isActive = currentTrack?.id === track.trackId;
          return (
            <div
              key={track.trackId}
              className={cn(styles.trackRow, isActive && styles.trackRowActive)}
              onClick={() => {
                if (isActive) {
                  togglePlayPause();
                } else {
                  handlePlayTrack(track);
                }
              }}
            >
              <div>
                <span className={styles.trackNumber}>{track.trackNumber}</span>
                <span className={styles.trackPlayIcon}>
                  {isActive && isPlaying ? "⏸" : "▶"}
                </span>
              </div>
              <div className={styles.trackInfo}>
                <span className={styles.trackTitle}>{track.title}</span>
                {track.artists && track.artists.length > 0 && (
                  <span className={styles.trackArtist} style={{ display: "block", marginTop: "2px" }}>
                    {track.artists.join(", ")}
                  </span>
                )}
              </div>
              <button
                className={cn(
                  styles.trackFavorite,
                  favorites.has(track.trackId) && styles.trackFavoriteActive
                )}
                onClick={(e) => {
                  e.stopPropagation();
                  toggleFavorite(track.trackId);
                }}
                aria-label={favorites.has(track.trackId) ? "Remove from favorites" : "Add to favorites"}
              >
                {favorites.has(track.trackId) ? "♥" : "♡"}
              </button>
              <span className={styles.trackDuration}>
                {formatDuration(track.duration)}
              </span>
            </div>
          );
        })}
      </div>
    </div>
  );
}
