"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { api } from "@/lib/api";
import styles from "../collection.module.css";
import { assetUrl } from "@/lib/utils";

interface Album {
  albumId: number;
  title: string;
  artists: string[]; // Updated from artist: string
  coverPath: string | null;
  trackCount: number;
  totalDuration: number;
  albumType: string;
  releaseDate: string;
}

export default function AlbumsPage() {
  const [albums, setAlbums] = useState<Album[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api
      .get<Album[]>("/music/albums")
      .then((res) => {
        if (res.success && res.data) setAlbums(res.data);
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  return (
    <div>
      <div className={styles.favoritesHeader}>
        <h1 className={styles.favoritesTitle}>Albums</h1>
        <p className={styles.favoritesCount}>
          {albums.length} {albums.length === 1 ? "album" : "albums"}
        </p>
      </div>

      {loading ? (
        <div className={styles.albumGrid}>
          {Array.from({ length: 6 }).map((_, i) => (
            <div key={i} className={styles.albumCard}>
              <div className={`${styles.albumCover} skeleton`} />
              <div className="skeleton skeleton-text" style={{ width: "80%" }} />
              <div className="skeleton skeleton-text" style={{ width: "50%" }} />
            </div>
          ))}
        </div>
      ) : albums.length > 0 ? (
        <div className={styles.albumGrid}>
          {albums.map((album) => (
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
      ) : (
        <div className="empty-state">
          <span className="empty-state-icon">🎵</span>
          <h3>No albums yet</h3>
          <p>Albums will appear here once music is uploaded.</p>
        </div>
      )}
    </div>
  );
}
