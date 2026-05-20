'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { FiMusic, FiPlay } from 'react-icons/fi';
import { api } from '@/lib/api';
import { usePlayer, Track } from '@/contexts/PlayerContext';

interface Album {
  id: string | number;
  title: string;
  artistName?: string;
  coverImagePath?: string | null;
  trackCount?: number;
  tracks?: Track[];
}

interface FeaturedTrack extends Track {
  albumId?: string | number;
}

export default function HomePage() {
  const [albums, setAlbums] = useState<Album[]>([]);
  const [featured, setFeatured] = useState<FeaturedTrack[]>([]);
  const [loading, setLoading] = useState(true);
  const { play } = usePlayer();

  useEffect(() => {
    async function load() {
      try {
        const [albumRes, featuredRes] = await Promise.allSettled([
          api.get('/api/music/albums'),
          api.get('/api/music/tracks/featured'),
        ]);

        if (albumRes.status === 'fulfilled' && albumRes.value?.data) {
          setAlbums(
            Array.isArray(albumRes.value.data)
              ? albumRes.value.data
              : albumRes.value.data.items || []
          );
        }

        if (featuredRes.status === 'fulfilled' && featuredRes.value?.data) {
          setFeatured(
            Array.isArray(featuredRes.value.data)
              ? featuredRes.value.data
              : featuredRes.value.data.items || []
          );
        }
      } catch {
        // API might not be ready yet
      } finally {
        setLoading(false);
      }
    }
    load();
  }, []);

  return (
    <>
      {/* Hero */}
      <section className="hero-section animate-fade-in-up">
        <h1 className="hero-title">COLLECTION</h1>
        <p className="hero-subtitle">The Attrition Soundtrack</p>
        <div className="hero-ember-line" />
      </section>

      {/* Featured Tracks */}
      {featured.length > 0 && (
        <section className="animate-fade-in-up" style={{ animationDelay: '0.1s' }}>
          <div className="section-header">
            <h2 className="section-title">Featured</h2>
          </div>
          <div className="featured-grid">
            {featured.map((track) => (
              <div
                key={track.id}
                className="featured-card"
                onClick={() => play(track)}
              >
                <div className="featured-card-art">
                  {track.albumCoverPath ? (
                    <img
                      src={`/uploads/${track.albumCoverPath}`}
                      alt={track.albumTitle || ''}
                    />
                  ) : (
                    <FiMusic style={{ fontSize: 24, color: 'var(--text-ghost)' }} />
                  )}
                </div>
                <div className="featured-card-info">
                  <div className="featured-card-title">{track.title}</div>
                  <div className="featured-card-album">
                    {track.albumTitle || 'Attrition OST'}
                  </div>
                </div>
              </div>
            ))}
          </div>
        </section>
      )}

      {/* Albums */}
      <section className="animate-fade-in-up" style={{ animationDelay: '0.2s' }}>
        <div className="section-header">
          <h2 className="section-title">Albums</h2>
        </div>

        {loading ? (
          <div className="album-grid">
            {[1, 2, 3, 4].map((i) => (
              <div key={i} className="album-card" style={{ cursor: 'default' }}>
                <div className="album-card-cover">
                  <div className="skeleton skeleton-cover" />
                </div>
                <div className="skeleton skeleton-text" style={{ width: '80%' }} />
                <div className="skeleton skeleton-text" style={{ width: '50%' }} />
              </div>
            ))}
          </div>
        ) : albums.length === 0 ? (
          <div className="empty-state">
            <div className="empty-state-icon">
              <FiMusic />
            </div>
            <h3>No albums yet</h3>
            <p>The soundtrack collection is being prepared. Check back soon.</p>
          </div>
        ) : (
          <div className="album-grid">
            {albums.map((album) => (
              <Link
                key={album.id}
                href={`/albums/${album.id}`}
                className="album-card"
              >
                <div className="album-card-cover">
                  {album.coverImagePath ? (
                    <img
                      src={`/uploads/${album.coverImagePath}`}
                      alt={album.title}
                    />
                  ) : (
                    <FiMusic className="album-card-cover-placeholder" />
                  )}
                  <button
                    className="album-card-play"
                    onClick={(e) => {
                      e.preventDefault();
                      // Play first track if available
                      if (album.tracks && album.tracks.length > 0) {
                        play(album.tracks[0], album.tracks);
                      }
                    }}
                  >
                    <FiPlay />
                  </button>
                </div>
                <div className="album-card-title">{album.title}</div>
                <div className="album-card-meta">
                  {album.artistName || 'Attrition OST'}
                  {album.trackCount != null && ` · ${album.trackCount} tracks`}
                </div>
              </Link>
            ))}
          </div>
        )}
      </section>
    </>
  );
}