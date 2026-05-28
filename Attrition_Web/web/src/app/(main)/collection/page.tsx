"use client";

import { useEffect, useState, useRef } from "react";
import Link from "next/link";
import { api } from "@/lib/api";
import { usePlayer } from "@/contexts/PlayerContext";
import styles from "./collection.module.css";
import { assetUrl, cn, formatFileSize } from "@/lib/utils";
import { Music, Image as ImageIcon, Box, X, ChevronLeft, ChevronRight } from "lucide-react";

interface Album {
  albumId: number;
  title: string;
  artists: string[];
  coverPath: string | null;
  trackCount: number;
  totalDuration: number;
  albumType: string;
  releaseDate: string;
}

interface AssetDto {
  id: string;
  fileName: string;
  filePath: string;
  assetType: string;
  mimeType: string;
  fileSize: number;
  title: string | null;
  description: string | null;
  tags: string | null;
  uploadedBy: string | null;
  uploadedAt: string;
  updatedAt: string | null;
}

interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

type TabType = 'soundtrack' | 'gallery' | 'sprites';

export default function CollectionHome() {
  const [activeTab, setActiveTab] = useState<TabType>('soundtrack');
  
  // Soundtrack State
  const [albums, setAlbums] = useState<Album[]>([]);
  const [albumsLoading, setAlbumsLoading] = useState(true);

  // Gallery State (Concept Art + Screenshots)
  const [galleryItems, setGalleryItems] = useState<AssetDto[]>([]);
  const [galleryLoading, setGalleryLoading] = useState(true);
  const [galleryPage, setGalleryPage] = useState(1);
  const [galleryHasMore, setGalleryHasMore] = useState(false);

  // Sprites State
  const [sprites, setSprites] = useState<AssetDto[]>([]);
  const [spritesLoading, setSpritesLoading] = useState(true);
  const [spritesPage, setSpritesPage] = useState(1);
  const [spritesHasMore, setSpritesHasMore] = useState(false);

  // Lightbox State
  const [lightboxImg, setLightboxImg] = useState<AssetDto | null>(null);

  useEffect(() => {
    // Fetch albums
    api.get<Album[]>("/music/albums")
      .then((res) => {
        if (res.success && res.data) setAlbums(res.data);
      })
      .catch(() => {})
      .finally(() => setAlbumsLoading(false));

    // Initial fetch for gallery & sprites
    fetchGallery(1);
    fetchSprites(1);
  }, []);

  const fetchGallery = (page: number) => {
    setGalleryLoading(true);
    // Fetch concept art
    api.get<PaginatedResponse<AssetDto>>(`/assets?assetType=concept-art&page=${page}&pageSize=20`)
      .then((res) => {
        if (res.success && res.data) {
          setGalleryItems(prev => page === 1 ? res.data!.items : [...prev, ...res.data!.items]);
          setGalleryHasMore(page < res.data.totalPages);
          setGalleryPage(page);
        }
      })
      .catch(() => {})
      .finally(() => setGalleryLoading(false));
  };

  const fetchSprites = (page: number) => {
    setSpritesLoading(true);
    api.get<PaginatedResponse<AssetDto>>(`/assets?assetType=sprite&page=${page}&pageSize=40`)
      .then((res) => {
        if (res.success && res.data) {
          setSprites(prev => page === 1 ? res.data!.items : [...prev, ...res.data!.items]);
          setSpritesHasMore(page < res.data.totalPages);
          setSpritesPage(page);
        }
      })
      .catch(() => {})
      .finally(() => setSpritesLoading(false));
  };

  const getHeroSubtitle = () => {
    switch (activeTab) {
      case 'soundtrack': return 'Attrition Original Soundtrack';
      case 'gallery': return 'Concept Art & Screenshots';
      case 'sprites': return 'In-Game Assets & Sprites';
    }
  };

  // Close lightbox on Escape key
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && lightboxImg) setLightboxImg(null);
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [lightboxImg]);

  return (
    <div>
      {/* Hero */}
      <div className={styles.hero}>
        <h1 className={styles.heroTitle}>COLLECTION</h1>
        <p className={styles.heroSubtitle}>{getHeroSubtitle()}</p>
      </div>

      {/* Tabs */}
      <div className={styles.tabs}>
        <button 
          className={cn(styles.tabBtn, activeTab === 'soundtrack' && styles.tabBtnActive)}
          onClick={() => setActiveTab('soundtrack')}
        >
          <Music size={18} className="inline mr-2 mb-1" />
          Soundtrack
        </button>
        <button 
          className={cn(styles.tabBtn, activeTab === 'gallery' && styles.tabBtnActive)}
          onClick={() => setActiveTab('gallery')}
        >
          <ImageIcon size={18} className="inline mr-2 mb-1" />
          Art Gallery
        </button>
        <button 
          className={cn(styles.tabBtn, activeTab === 'sprites' && styles.tabBtnActive)}
          onClick={() => setActiveTab('sprites')}
        >
          <Box size={18} className="inline mr-2 mb-1" />
          Game Sprites
        </button>
      </div>

      {/* Tab Content: Soundtrack */}
      {activeTab === 'soundtrack' && (
        <section className={styles.section}>
          {albumsLoading ? (
            <div className={styles.albumGrid}>
              {Array.from({ length: 4 }).map((_, i) => (
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
                  href={`/collection/albums/${album.albumId}`}
                  className={styles.albumCard}
                >
                  <div className={styles.albumCover}>
                    {album.coverPath ? (
                      // eslint-disable-next-line @next/next/no-img-element
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
        </section>
      )}

      {/* Tab Content: Art Gallery */}
      {activeTab === 'gallery' && (
        <section className={styles.section}>
          {galleryLoading && galleryPage === 1 ? (
            <div className={styles.galleryGrid}>
              {Array.from({ length: 6 }).map((_, i) => (
                <div key={i} className={styles.galleryCard}>
                  <div className="skeleton" style={{ height: `${Math.random() * 100 + 150}px` }} />
                </div>
              ))}
            </div>
          ) : galleryItems.length > 0 ? (
            <>
              <div className={styles.galleryGrid}>
                {galleryItems.map((item) => (
                  <div 
                    key={item.id} 
                    className={styles.galleryCard}
                    onClick={() => setLightboxImg(item)}
                    style={{ gridRowEnd: `span ${Math.floor(Math.random() * 3) + 12}` }} // Masonry approximation
                  >
                    <div className={styles.galleryImgWrapper} style={{ height: '100%' }}>
                      <img src={assetUrl(item.filePath)} alt={item.title || item.fileName} style={{ height: '100%' }} />
                    </div>
                  </div>
                ))}
              </div>
              
              {galleryHasMore && (
                <div className={styles.pagination}>
                  <button 
                    className="btn btn-secondary btn-md" 
                    onClick={() => fetchGallery(galleryPage + 1)}
                    disabled={galleryLoading}
                  >
                    {galleryLoading ? 'Loading...' : 'Load More'}
                  </button>
                </div>
              )}
            </>
          ) : (
            <div className="empty-state">
              <span className="empty-state-icon">🖼️</span>
              <h3>No art found</h3>
              <p>Concept art and screenshots will appear here.</p>
            </div>
          )}
        </section>
      )}

      {/* Tab Content: Game Sprites */}
      {activeTab === 'sprites' && (
        <section className={styles.section}>
          {spritesLoading && spritesPage === 1 ? (
            <div className={styles.spriteGrid}>
              {Array.from({ length: 12 }).map((_, i) => (
                <div key={i} className={styles.spriteCard}>
                  <div className="skeleton" style={{ aspectRatio: '1' }} />
                </div>
              ))}
            </div>
          ) : sprites.length > 0 ? (
            <>
              <div className={styles.spriteGrid}>
                {sprites.map((sprite) => (
                  <div 
                    key={sprite.id} 
                    className={styles.spriteCard}
                    onClick={() => setLightboxImg(sprite)}
                    style={{ cursor: 'zoom-in' }}
                  >
                    <div className={styles.checkerboardBg}>
                      <img src={assetUrl(sprite.filePath)} alt={sprite.title || sprite.fileName} />
                    </div>
                    <div className={styles.spriteInfo}>
                      <div className={styles.spriteTitle} title={sprite.title || sprite.fileName}>
                        {sprite.title || sprite.fileName}
                      </div>
                      <div className={styles.spriteMeta}>
                        {formatFileSize(sprite.fileSize)}
                      </div>
                    </div>
                  </div>
                ))}
              </div>

              {spritesHasMore && (
                <div className={styles.pagination}>
                  <button 
                    className="btn btn-secondary btn-md" 
                    onClick={() => fetchSprites(spritesPage + 1)}
                    disabled={spritesLoading}
                  >
                    {spritesLoading ? 'Loading...' : 'Load More'}
                  </button>
                </div>
              )}
            </>
          ) : (
            <div className="empty-state">
              <span className="empty-state-icon">👾</span>
              <h3>No sprites found</h3>
              <p>Game sprites and textures will appear here.</p>
            </div>
          )}
        </section>
      )}

      {/* Lightbox Modal */}
      {lightboxImg && (
        <div className={styles.lightbox} onClick={() => setLightboxImg(null)}>
          <button className={styles.lightboxClose} onClick={() => setLightboxImg(null)}>
            <X size={24} />
          </button>
          <img 
            src={assetUrl(lightboxImg.filePath)} 
            alt={lightboxImg.title || lightboxImg.fileName} 
            className={styles.lightboxImg}
            onClick={e => e.stopPropagation()} // Prevent click-through closing
            style={lightboxImg.assetType === 'sprite' ? { imageRendering: 'pixelated' } : {}}
          />
          <div className={styles.lightboxInfo}>
            <h3 style={{ fontSize: 'var(--text-xl)', fontWeight: 'var(--weight-bold)', marginBottom: 'var(--space-1)' }}>
              {lightboxImg.title || lightboxImg.fileName}
            </h3>
            {lightboxImg.description && (
              <p style={{ color: 'rgba(255,255,255,0.8)', fontSize: 'var(--text-sm)', marginBottom: 'var(--space-2)' }}>
                {lightboxImg.description}
              </p>
            )}
            <div style={{ display: 'flex', gap: 'var(--space-3)', fontSize: 'var(--text-xs)', color: 'rgba(255,255,255,0.6)' }}>
              <span>{formatFileSize(lightboxImg.fileSize)}</span>
              <span>•</span>
              <span style={{ textTransform: 'uppercase' }}>{lightboxImg.assetType}</span>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
