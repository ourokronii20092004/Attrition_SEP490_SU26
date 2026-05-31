"use client";

import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { X, Images, ChevronLeft, ChevronRight } from "lucide-react";
import { assetsApi } from "@/lib/api/assets";
import { resolveMediaUrl } from "@/lib/api/media";
import { PageShell } from "@/components/ui/page-shell";
import { PageTitle } from "@/components/ui/page-title";
import { Card } from "@/components/ui/card";
import { SkeletonGrid } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { Pagination } from "@/components/ui/pagination";

export default function GalleryPage() {
  const [page, setPage] = useState(1);
  // Lightbox tracks an index into the current page's items so prev/next can navigate.
  const [lightboxIdx, setLightboxIdx] = useState<number | null>(null);

  const { data: assets, isPending } = useQuery({
    queryKey: ["assets", page],
    queryFn: async () => {
      const res = await assetsApi.list({ page, pageSize: 18 });
      return res.success ? res.data : null;
    },
  });

  const totalPages = assets ? Math.ceil(assets.totalCount / assets.pageSize) : 0;
  const items = assets?.items ?? [];
  const lightbox = lightboxIdx != null ? items[lightboxIdx] ?? null : null;

  const showPrev = () => setLightboxIdx((i) => (i == null ? i : (i - 1 + items.length) % items.length));
  const showNext = () => setLightboxIdx((i) => (i == null ? i : (i + 1) % items.length));

  useEffect(() => {
    if (lightboxIdx == null) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") setLightboxIdx(null);
      else if (e.key === "ArrowLeft") showPrev();
      else if (e.key === "ArrowRight") showNext();
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [lightboxIdx, items.length]);

  const parseTags = (tags: string | null): string[] => {
    if (!tags) return [];
    return tags.split(",").map((t) => t.trim()).filter(Boolean);
  };

  return (
    <PageShell>
      <PageTitle description="Concept art, sprites, and assets from the Attrition world.">Gallery</PageTitle>

      {isPending ? (
        <SkeletonGrid count={12} />
      ) : !assets?.items.length ? (
        <EmptyState icon={Images} title="Nothing here yet" description="Artwork and assets will appear here once uploaded." />
      ) : (
        <>
          <div className="stagger grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4">
            {assets.items.map((asset, i) => (
              <Card key={asset.id} interactive style={{ "--i": i } as React.CSSProperties} className="overflow-hidden p-0">
                <button onClick={() => setLightboxIdx(i)} className="group block w-full text-left">
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
          className="fixed inset-0 z-[400] flex flex-col bg-black/90 backdrop-blur-md motion-safe:animate-fade-in"
          role="dialog"
          aria-modal="true"
          aria-label={lightbox.title ?? lightbox.fileName}
        >
          {/* Top bar: title + counter + close */}
          <div className="flex items-center justify-between gap-4 px-4 py-3 sm:px-6">
            <div className="min-w-0">
              <p className="truncate font-display text-base font-semibold text-white">{lightbox.title ?? lightbox.fileName}</p>
              <p className="text-xs text-white/50">{(lightboxIdx ?? 0) + 1} of {items.length}</p>
            </div>
            <button
              onClick={() => setLightboxIdx(null)}
              className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-white/10 text-white/80 transition-colors hover:bg-white/20 hover:text-white"
              aria-label="Close"
            >
              <X size={20} />
            </button>
          </div>

          {/* Stage: image flanked by edge nav arrows. Click backdrop to close. */}
          <div className="relative flex flex-1 items-center justify-center overflow-hidden px-2 sm:px-16" onClick={() => setLightboxIdx(null)}>
            {items.length > 1 && (
              <button
                onClick={(e) => { e.stopPropagation(); showPrev(); }}
                className="absolute left-2 top-1/2 z-10 flex h-12 w-12 -translate-y-1/2 items-center justify-center rounded-full bg-white/10 text-white/80 transition-colors hover:bg-white/20 hover:text-white sm:left-4"
                aria-label="Previous image"
              >
                <ChevronLeft size={26} />
              </button>
            )}

            <img
              src={resolveMediaUrl(lightbox.filePath) ?? ""}
              alt={lightbox.title ?? lightbox.fileName}
              onClick={(e) => e.stopPropagation()}
              className="max-h-full max-w-full rounded-lg object-contain shadow-[var(--shadow-lg)] motion-safe:animate-fade-in"
            />

            {items.length > 1 && (
              <button
                onClick={(e) => { e.stopPropagation(); showNext(); }}
                className="absolute right-2 top-1/2 z-10 flex h-12 w-12 -translate-y-1/2 items-center justify-center rounded-full bg-white/10 text-white/80 transition-colors hover:bg-white/20 hover:text-white sm:right-4"
                aria-label="Next image"
              >
                <ChevronRight size={26} />
              </button>
            )}
          </div>

          {/* Caption: description + tags */}
          {(lightbox.description || parseTags(lightbox.tags).length > 0) && (
            <div className="px-4 pb-5 pt-2 text-center sm:px-6">
              {lightbox.description && <p className="mx-auto max-w-2xl text-sm text-white/70">{lightbox.description}</p>}
              {parseTags(lightbox.tags).length > 0 && (
                <div className="mt-2 flex flex-wrap justify-center gap-1.5">
                  {parseTags(lightbox.tags).map((tag) => (
                    <span key={tag} className="rounded-full bg-white/10 px-2.5 py-0.5 text-xs text-white/60">{tag}</span>
                  ))}
                </div>
              )}
            </div>
          )}
        </div>
      )}
    </PageShell>
  );
}
