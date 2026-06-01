"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { Music as MusicIcon, Heart } from "lucide-react";
import { musicApi } from "@/lib/api/music";
import { resolveMediaUrl } from "@/lib/api/media";
import { useAuth } from "@/lib/providers";
import { PageShell } from "@/components/ui/page-shell";
import { PageTitle } from "@/components/ui/page-title";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { SkeletonGrid } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { qk } from "@/lib/query-keys";

const PAGE_SIZE = 24;

export default function MusicPage() {
  const [page, setPage] = useState(1);
  const { user } = useAuth();

  const { data, isPending } = useQuery({
    queryKey: qk.music.albums(page),
    queryFn: async () => {
      const res = await musicApi.getAlbumsPaged(page, PAGE_SIZE);
      return res.success ? res.data : null;
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
          <Link
            href="/music/favorites"
            className="mt-1 inline-flex shrink-0 items-center gap-1.5 rounded-md border border-border-strong px-3 py-1.5 text-sm text-fg-muted transition-colors hover:border-accent/60 hover:text-fg"
          >
            <Heart size={15} /> Favorites
          </Link>
        )}
      </div>

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
              <Button size="sm" variant="secondary" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Prev</Button>
              <span className="text-sm text-fg-muted">Page {page} of {totalPages}</span>
              <Button size="sm" variant="secondary" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>Next</Button>
            </div>
          )}
        </>
      )}
    </PageShell>
  );
}
