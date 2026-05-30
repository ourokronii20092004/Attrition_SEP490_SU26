"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { Play, Music as MusicIcon } from "lucide-react";
import { musicApi } from "@/lib/api/music";
import { resolveMediaUrl } from "@/lib/api/media";
import { useAudioStore } from "@/lib/stores/audio-store";
import { PageShell } from "@/components/ui/page-shell";
import { PageTitle, SectionTitle } from "@/components/ui/page-title";
import { Card } from "@/components/ui/card";
import { SkeletonGrid } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import type { MusicAlbumDto, FeaturedTracksResponse } from "@/lib/types";

export default function MusicPage() {
  const [albums, setAlbums] = useState<MusicAlbumDto[]>([]);
  const [featured, setFeatured] = useState<FeaturedTracksResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const play = useAudioStore((s) => s.play);

  useEffect(() => {
    Promise.all([musicApi.getAlbums(), musicApi.getFeatured()])
      .then(([albumsRes, featuredRes]) => {
        if (albumsRes.success) setAlbums(albumsRes.data ?? []);
        if (featuredRes.success) setFeatured(featuredRes.data);
      })
      .finally(() => setLoading(false));
  }, []);

  if (loading) {
    return (
      <PageShell>
        <PageTitle description="Original soundtrack of the Attrition universe.">Music</PageTitle>
        <SkeletonGrid count={6} className="lg:grid-cols-3" />
      </PageShell>
    );
  }

  return (
    <PageShell>
      <PageTitle description="Original soundtrack of the Attrition universe.">Music</PageTitle>

      {featured && featured.featuredTracks.length > 0 && (
        <section className="mb-10">
          <SectionTitle>Featured Tracks</SectionTitle>
          <div className="stagger mt-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {featured.featuredTracks.map((track, i) => (
              <Card key={track.trackId} style={{ "--i": i } as React.CSSProperties} className="group flex items-center gap-3 p-3">
                {track.coverPath ? (
                  <img src={resolveMediaUrl(track.coverPath) ?? ""} alt="" className="h-12 w-12 rounded-lg object-cover" />
                ) : (
                  <div className="flex h-12 w-12 items-center justify-center rounded-lg bg-surface-2 text-fg-subtle">
                    <MusicIcon size={18} />
                  </div>
                )}
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-medium text-fg">{track.title}</p>
                  <p className="truncate text-xs text-fg-muted">{track.albumTitle}</p>
                </div>
                <button
                  onClick={() => play(track, featured.featuredTracks)}
                  className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-accent text-accent-fg shadow-sm transition-transform duration-150 hover:brightness-110 active:scale-95"
                  aria-label={`Play ${track.title}`}
                >
                  <Play size={14} className="ml-0.5" />
                </button>
              </Card>
            ))}
          </div>
        </section>
      )}

      <section>
        <SectionTitle>Albums</SectionTitle>
        {albums.length === 0 ? (
          <EmptyState
            icon={MusicIcon}
            title="No albums yet"
            description="The soundtrack will appear here once albums are published."
            className="mt-4"
          />
        ) : (
          <div className="stagger mt-4 grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4">
            {albums.map((album, i) => (
              <Card key={album.albumId} interactive style={{ "--i": i } as React.CSSProperties} className="overflow-hidden p-0">
                <Link href={`/music/${album.albumId}`} className="group block">
                  {album.coverPath ? (
                    <img src={resolveMediaUrl(album.coverPath) ?? ""} alt="" className="aspect-square w-full object-cover" />
                  ) : (
                    <div className="flex aspect-square items-center justify-center bg-surface-2 text-fg-subtle">
                      <MusicIcon size={28} />
                    </div>
                  )}
                  <div className="p-3">
                    <h3 className="truncate font-medium text-fg transition-colors group-hover:text-accent">{album.title}</h3>
                    <p className="mt-1 truncate text-xs text-fg-muted">
                      {album.artists.join(", ")} &middot; {album.trackCount} tracks
                    </p>
                  </div>
                </Link>
              </Card>
            ))}
          </div>
        )}
      </section>
    </PageShell>
  );
}
