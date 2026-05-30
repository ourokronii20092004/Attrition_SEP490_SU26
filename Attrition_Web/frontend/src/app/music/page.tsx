"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { Play } from "lucide-react";
import { musicApi } from "@/lib/api/music";
import { resolveMediaUrl } from "@/lib/api/media";
import { useAudioStore } from "@/lib/stores/audio-store";
import { PageLoader } from "@/components/ui/spinner";
import type { MusicAlbumDto, MusicTrackDto, FeaturedTracksResponse } from "@/lib/types";

export default function MusicPage() {
  const [albums, setAlbums] = useState<MusicAlbumDto[]>([]);
  const [featured, setFeatured] = useState<FeaturedTracksResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const play = useAudioStore((s) => s.play);

  useEffect(() => {
    Promise.all([musicApi.getAlbums(), musicApi.getFeatured()])
      .then(([albumsRes, featuredRes]) => {
        if (albumsRes.success) setAlbums(albumsRes.data);
        if (featuredRes.success) setFeatured(featuredRes.data);
      })
      .finally(() => setLoading(false));
  }, []);

  if (loading) return <PageLoader />;

  return (
    <div className="mx-auto max-w-6xl px-4 py-8">
      <h1 className="font-display text-4xl font-bold text-fg">Music</h1>
      <p className="mt-2 text-fg-muted">Original soundtrack of Attrition</p>

      {featured && featured.featuredTracks.length > 0 && (
        <section className="mt-8">
          <h2 className="font-display text-2xl font-semibold text-fg">Featured Tracks</h2>
          <div className="mt-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {featured.featuredTracks.map((track) => (
              <div key={track.trackId} className="card flex items-center gap-3 p-3">
                {track.coverPath && (
                  <img src={resolveMediaUrl(track.coverPath) ?? ""} alt="" className="h-12 w-12 rounded object-cover" />
                )}
                <div className="flex-1 min-w-0">
                  <p className="truncate text-sm font-medium text-fg">{track.title}</p>
                  <p className="truncate text-xs text-fg-muted">{track.albumTitle}</p>
                </div>
                <button
                  onClick={() => play(track, featured.featuredTracks)}
                  className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-accent text-accent-fg"
                  aria-label={`Play ${track.title}`}
                >
                  <Play size={14} className="ml-0.5" />
                </button>
              </div>
            ))}
          </div>
        </section>
      )}

      <section className="mt-10">
        <h2 className="font-display text-2xl font-semibold text-fg">Albums</h2>
        <div className="mt-4 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {albums.map((album) => (
            <Link key={album.albumId} href={`/music/${album.albumId}`} className="card overflow-hidden transition hover:border-accent">
              {album.coverPath ? (
                <img src={resolveMediaUrl(album.coverPath) ?? ""} alt="" className="aspect-square w-full object-cover" />
              ) : (
                <div className="flex aspect-square items-center justify-center bg-surface-2 text-fg-subtle">No Cover</div>
              )}
              <div className="p-4">
                <h3 className="font-medium text-fg">{album.title}</h3>
                <p className="mt-1 text-xs text-fg-muted">{album.artists.join(", ")} &middot; {album.trackCount} tracks</p>
              </div>
            </Link>
          ))}
          {albums.length === 0 && <p className="col-span-full py-8 text-center text-fg-muted">No albums yet.</p>}
        </div>
      </section>
    </div>
  );
}
