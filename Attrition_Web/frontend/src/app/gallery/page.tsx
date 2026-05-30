"use client";

import { useEffect, useState } from "react";
import { assetsApi } from "@/lib/api/assets";
import { resolveMediaUrl } from "@/lib/api/media";
import { PageLoader } from "@/components/ui/spinner";
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

  const parseTags = (tags: string | null): string[] => {
    if (!tags) return [];
    return tags.split(",").map((t) => t.trim()).filter(Boolean);
  };

  return (
    <div className="mx-auto max-w-6xl px-4 py-8">
      <h1 className="font-display text-4xl font-bold text-fg">Gallery</h1>
      <p className="mt-2 text-fg-muted">Concept art, sprites, and assets from Attrition</p>

      {loading ? (
        <PageLoader />
      ) : (
        <>
          <div className="mt-6 grid gap-4 grid-cols-2 sm:grid-cols-3 lg:grid-cols-4">
            {assets?.items.map((asset) => (
              <button
                key={asset.id}
                onClick={() => setLightbox(asset)}
                className="group card overflow-hidden transition hover:border-accent"
              >
                <img
                  src={resolveMediaUrl(asset.filePath) ?? ""}
                  alt={asset.title ?? asset.fileName}
                  className="aspect-square w-full object-cover transition group-hover:scale-105"
                />
                <div className="p-2">
                  <p className="truncate text-xs font-medium text-fg">{asset.title ?? asset.fileName}</p>
                  <p className="text-xs text-fg-subtle">{asset.assetType}</p>
                </div>
              </button>
            ))}
          </div>

          {totalPages > 1 && (
            <div className="mt-8 flex items-center justify-center gap-2">
              <button onClick={() => setPage((p) => Math.max(1, p - 1))} disabled={page <= 1} className="rounded-md border border-border px-3 py-1 text-sm text-fg-muted disabled:opacity-50">Prev</button>
              <span className="text-sm text-fg-muted">{page} / {totalPages}</span>
              <button onClick={() => setPage((p) => Math.min(totalPages, p + 1))} disabled={page >= totalPages} className="rounded-md border border-border px-3 py-1 text-sm text-fg-muted disabled:opacity-50">Next</button>
            </div>
          )}
        </>
      )}

      {lightbox && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-bg/80 backdrop-blur-sm" onClick={() => setLightbox(null)}>
          <div className="max-h-[90vh] max-w-[90vw] overflow-auto" onClick={(e) => e.stopPropagation()}>
            <img src={resolveMediaUrl(lightbox.filePath) ?? ""} alt={lightbox.title ?? lightbox.fileName} className="max-h-[85vh] rounded-lg" />
            <div className="mt-3 text-center">
              <p className="font-medium text-fg">{lightbox.title ?? lightbox.fileName}</p>
              {lightbox.description && <p className="mt-1 text-sm text-fg-muted">{lightbox.description}</p>}
              {parseTags(lightbox.tags).length > 0 && (
                <div className="mt-2 flex flex-wrap justify-center gap-1">
                  {parseTags(lightbox.tags).map((tag) => (
                    <span key={tag} className="rounded bg-surface-3 px-2 py-0.5 text-xs text-fg-muted">{tag}</span>
                  ))}
                </div>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
