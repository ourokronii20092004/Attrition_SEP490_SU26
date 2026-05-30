"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { Music as MusicIcon } from "lucide-react";
import { musicApi } from "@/lib/api/music";
import { resolveMediaUrl } from "@/lib/api/media";
import { PageShell } from "@/components/ui/page-shell";
import { PageTitle } from "@/components/ui/page-title";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { SkeletonGrid } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import type { MusicAlbumDto } from "@/lib/types";

const PAGE_SIZE = 24;

export default function MusicPage() {
  const [albums, setAlbums] = useState<MusicAlbumDto[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let ignore = false;
    setLoading(true);
    musicApi.getAlbumsPaged(page, PAGE_SIZE)
      .then((res) => { if (!ignore && res.success) { setAlbums(res.data.items); setTotal(res.data.totalCount); } })
      .finally(() => { if (!ignore) setLoading(false); });
    return () => { ignore = true; };
  }, [page]);

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));

  return (
    <PageShell>
      <PageTitle description="Original soundtrack of the Attrition universe.">Music</PageTitle>

      {loading ? (
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
