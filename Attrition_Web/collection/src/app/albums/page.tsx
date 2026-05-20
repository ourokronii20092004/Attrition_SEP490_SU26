'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { FiMusic } from 'react-icons/fi';
import { api } from '@/lib/api';

interface Album {
  id: string | number;
  title: string;
  artistName?: string;
  coverImagePath?: string | null;
  trackCount?: number;
}

export default function AlbumsPage() {
  const [albums, setAlbums] = useState<Album[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    async function load() {
      try {
        const res = await api.get('/api/music/albums');
        if (res?.data) {
          setAlbums(
            Array.isArray(res.data) ? res.data : res.data.items || []
          );
        }
      } catch {
        // API might not be ready
      } finally {
        setLoading(false);
      }
    }
    load();
  }, []);

  return (
    <>
      <div className="section-header" style={{ marginBottom: 'var(--space-xl)' }}>
        <h1 className="section-title" style={{ fontSize: '32px' }}>Albums</h1>
      </div>

      {loading ? (
        <div className="album-grid">
          {[1, 2, 3, 4, 5, 6].map((i) => (
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
    </>
  );
}
