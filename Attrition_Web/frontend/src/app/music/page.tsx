"use client";

import { Suspense } from "react";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { Music as MusicIcon, Heart, ListMusic, Sparkles, Play, Pause } from "lucide-react";
import { musicApi } from "@/lib/api/music";
import { resolveMediaUrl } from "@/lib/api/media";
import { useAuth } from "@/lib/providers";
import { useAudioStore } from "@/lib/stores/audio-store";
import { PageShell } from "@/components/ui/page-shell";
import { PageTitle } from "@/components/ui/page-title";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { SkeletonGrid } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { qk } from "@/lib/query-keys";
import { useUrlPage } from "@/lib/hooks/use-url-pagination";
import type { MusicTrackDto } from "@/lib/types";

const PAGE_SIZE = 24;

function MusicList() {
  const [page, setPage] = useUrlPage();
  const { user } = useAuth();

  const { data, isPending } = useQuery({
    queryKey: qk.music.albums(page),
    queryFn: async () => {
      const res = await musicApi.getAlbumsPaged(page, PAGE_SIZE);
      return res.success ? res.data : null;
    },
  });

  // Hand-picked tracks admins flagged as featured — the music library's front row.
  const { data: featured = [] } = useQuery({
    queryKey: qk.music.featured(),
    queryFn: async () => {
      const res = await musicApi.getFeatured();
      return res.success ? res.data.featuredTracks : [];
    },
  });

  const albums = data?.items ?? [];
  const total = data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));

  return (
    <PageShell>
      <div className="flex items-start justify-between gap-4">
        <PageTitle description="Original soundtrack of the Attrition universe.">Music</PageTitle>
        {user && (
          <div className="mt-1 flex shrink-0 items-center gap-2">
            <Link
              href="/music/favorites"
              className="inline-flex items-center gap-1.5 rounded-md border border-border-strong px-3 py-1.5 text-sm text-fg-muted transition-colors hover:border-accent/60 hover:text-fg"
            >
              <Heart size={15} /> Favorites
            </Link>
            <Link
              href="/music/playlists"
              className="inline-flex items-center gap-1.5 rounded-md border border-border-strong px-3 py-1.5 text-sm text-fg-muted transition-colors hover:border-accent/60 hover:text-fg"
            >
              <ListMusic size={15} /> Playlists
            </Link>
          </div>
        )}
      </div>

      <FeaturedStrip tracks={featured} />

      {isPending ? (
        <SkeletonGrid count={8} className="lg:grid-cols-4" />
      ) : albums.length === 0 ? (
        <EmptyState
          icon={MusicIcon}
          title="No albums yet"
          description="The soundtrack will appear here once albums are published."
          className="mt-4"
        />
      ) : (
        <>
          <div className="stagger mt-2 grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4">
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

          {totalPages > 1 && (
            <div className="mt-8 flex items-center justify-center gap-3">
              <Button size="sm" variant="secondary" disabled={page <= 1} onClick={() => setPage(page - 1)}>Prev</Button>
              <span className="text-sm text-fg-muted">Page {page} of {totalPages}</span>
              <Button size="sm" variant="secondary" disabled={page >= totalPages} onClick={() => setPage(page + 1)}>Next</Button>
            </div>
          )}
        </>
      )}
    </PageShell>
  );
}

// Admins flag certain tracks as featured (admin/music); this strip is where that flag lands.
// Plays straight into the global audio queue, same behavior as album rows.
function FeaturedStrip({ tracks }: { tracks: MusicTrackDto[] }) {
  const { play, pause, resume, currentTrack, isPlaying } = useAudioStore();
  if (tracks.length === 0) return null;

  const onPlay = (t: MusicTrackDto) => {
    if (currentTrack?.trackId === t.trackId) {
      isPlaying ? pause() : resume();
    } else {
      play(t, tracks);
    }
  };

  return (
    <section className="mt-6">
      <h2 className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-fg-muted">
        <Sparkles size={13} className="text-accent" /> Featured
      </h2>
      <div className="mt-3 flex gap-3 overflow-x-auto pb-2">
        {tracks.map((t) => {
          const active = currentTrack?.trackId === t.trackId;
          const cover = resolveMediaUrl(t.albumCoverPath ?? t.coverPath ?? "");
          return (
            <div
              key={t.trackId}
              className={`group w-52 shrink-0 overflow-hidden rounded-xl border bg-surface-2 transition-colors ${active ? "border-accent/50" : "border-border"}`}
            >
              <button
                type="button"
                onClick={() => onPlay(t)}
                className="relative block w-full"
                aria-label={active && isPlaying ? `Pause ${t.title}` : `Play ${t.title}`}
              >
                {cover ? (
                  <img src={cover} alt="" className="aspect-square w-full object-cover" />
                ) : (
                  <div className="flex aspect-square items-center justify-center bg-surface-3 text-fg-subtle">
                    <MusicIcon size={28} />
                  </div>
                )}
                <span className="absolute inset-0 flex items-center justify-center bg-black/0 opacity-0 transition-all group-hover:bg-black/30 group-hover:opacity-100">
                  <span className="rounded-full bg-accent p-2.5 text-accent-fg shadow-[var(--shadow-lg)]">
                    {active && isPlaying ? <Pause size={16} /> : <Play size={16} />}
                  </span>
                </span>
              </button>
              <div className="p-2.5">
                <Link href={`/music/${t.albumId}`} className="block truncate text-sm font-medium text-fg transition-colors hover:text-accent">
                  {t.title}
                </Link>
                <p className="mt-0.5 truncate text-xs text-fg-subtle">
                  {t.artists.join(", ")}{t.albumTitle ? ` · ${t.albumTitle}` : ""}
                </p>
              </div>
            </div>
          );
        })}
      </div>
    </section>
  );
}

export default function MusicPage() {
  return (
    <Suspense fallback={null}>
      <MusicList />
    </Suspense>
  );
}
