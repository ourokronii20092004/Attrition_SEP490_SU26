"use client";

import { useEffect, useState } from "react";
import { X, Images } from "lucide-react";
import { assetsApi } from "@/lib/api/assets";
import { resolveMediaUrl } from "@/lib/api/media";
import { PageShell } from "@/components/ui/page-shell";
import { PageTitle } from "@/components/ui/page-title";
import { Card } from "@/components/ui/card";
import { SkeletonGrid } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { Pagination } from "@/components/ui/pagination";
import type { AssetDto, PaginatedResponse } from "@/lib/types";

export default function GalleryPage() {
  const [assets, setAssets] = useState<PaginatedResponse<AssetDto> | null>(null);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [lightbox, setLightbox] = useState<AssetDto | null>(null);
  const totalPages = assets ? Math.ceil(assets.totalCount / assets.pageSize) : 0;

  useEffect(() => {
    setLoading(true);
    assetsApi
      .list({ page, pageSize: 18 })
      .then((res) => {
        if (res.success) setAssets(res.data);
      })
      .finally(() => setLoading(false));
  }, [page]);

  useEffect(() => {
    if (!lightbox) return;
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") setLightbox(null); };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [lightbox]);

  const parseTags = (tags: string | null): string[] => {
    if (!tags) return [];
    return tags.split(",").map((t) => t.trim()).filter(Boolean);
  };

  return (
    <PageShell>
      <PageTitle description="Concept art, sprites, and assets from the Attrition world.">Gallery</PageTitle>

      {loading ? (
        <SkeletonGrid count={12} />
      ) : !assets?.items.length ? (
        <EmptyState icon={Images} title="Nothing here yet" description="Artwork and assets will appear here once uploaded." />
      ) : (
        <>
          <div className="stagger grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4">
            {assets.items.map((asset, i) => (
              <Card key={asset.id} interactive style={{ "--i": i } as React.CSSProperties} className="overflow-hidden p-0">
                <button onClick={() => setLightbox(asset)} className="group block w-full text-left">
                  <div className="overflow-hidden">
                    <img
                      src={resolveMediaUrl(asset.filePath) ?? ""}
                      alt={asset.title ?? asset.fileName}
                      loading="lazy"
                      className="aspect-square w-full object-cover transition-transform duration-300 group-hover:scale-105"
                    />
                  </div>
                  <div className="p-2.5">
                    <p className="truncate text-xs font-medium text-fg">{asset.title ?? asset.fileName}</p>
                    <p className="text-xs text-fg-subtle">{asset.assetType}</p>
                  </div>
                </button>
              </Card>
            ))}
          </div>
          <Pagination page={page} totalPages={totalPages} onChange={setPage} />
        </>
      )}

      {lightbox && (
        <div
          className="fixed inset-0 z-[400] flex items-center justify-center bg-bg/85 p-4 backdrop-blur-md motion-safe:animate-fade-in"
          onClick={() => setLightbox(null)}
          role="dialog"
          aria-modal="true"
          aria-label={lightbox.title ?? lightbox.fileName}
        >
          <button
            onClick={() => setLightbox(null)}
            className="absolute right-4 top-4 flex h-10 w-10 items-center justify-center rounded-full bg-surface/80 text-fg-muted transition-colors hover:text-fg"
            aria-label="Close"
          >
            <X size={20} />
          </button>
          <div className="max-h-[90vh] max-w-3xl overflow-auto motion-safe:animate-rise-in" onClick={(e) => e.stopPropagation()}>
            <img
              src={resolveMediaUrl(lightbox.filePath) ?? ""}
              alt={lightbox.title ?? lightbox.fileName}
              className="mx-auto max-h-[78vh] rounded-xl shadow-[var(--shadow-lg)]"
            />
            <div className="mt-4 text-center">
              <p className="font-display text-lg font-semibold text-fg">{lightbox.title ?? lightbox.fileName}</p>
              {lightbox.description && <p className="mt-1 text-sm text-fg-muted">{lightbox.description}</p>}
              {parseTags(lightbox.tags).length > 0 && (
                <div className="mt-3 flex flex-wrap justify-center gap-1.5">
                  {parseTags(lightbox.tags).map((tag) => (
                    <span key={tag} className="rounded-full bg-surface-3 px-2.5 py-0.5 text-xs text-fg-muted">{tag}</span>
                  ))}
                </div>
              )}
            </div>
          </div>
        </div>
      )}
    </PageShell>
  );
}
