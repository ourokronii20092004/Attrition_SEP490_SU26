"use client";

import { useState } from "react";
import { ImagePlus, X } from "lucide-react";
import { assetsApi } from "@/lib/api/assets";
import { resolveMediaUrl } from "@/lib/api/media";
import { parseApiError } from "@/lib/api/parse-error";

/**
 * Admin image picker that uploads through the Assets service. The upload creates a real gallery
 * Asset row (tagged with sourceType/sourceId so it's traceable to the entity), and reports the
 * stored public URL back via onChange so the parent form can persist it on the enemy/item.
 */
export function AssetImageField({ value, onChange, sourceType, sourceId, label = "Image", assetType = "sprite" }: {
  value: string | null | undefined;
  onChange: (url: string | null) => void;
  sourceType: string;
  sourceId?: string;
  label?: string;
  assetType?: string;
}) {
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const upload = async (file: File) => {
    setError(null);
    setUploading(true);
    try {
      const res = await assetsApi.create(file, {
        assetType,
        title: file.name,
        sourceType,
        sourceId,
        tags: sourceType,
      });
      if (res.success && res.data) onChange(res.data.filePath);
      else setError(res.error || "Upload failed.");
    } catch (e) {
      setError(parseApiError(e, "Upload failed."));
    } finally {
      setUploading(false);
    }
  };

  const preview = resolveMediaUrl(value ?? null);

  return (
    <div className="space-y-1.5">
      <label className="block text-xs font-medium uppercase tracking-wider text-fg-muted">{label}</label>
      <div className="flex items-center gap-3">
        {preview ? (
          <div className="relative h-20 w-20 shrink-0 overflow-hidden rounded-lg border border-border">
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img src={preview} alt="" className="h-full w-full object-cover" />
            <button
              type="button"
              onClick={() => onChange(null)}
              aria-label="Remove image"
              className="absolute right-0.5 top-0.5 rounded-full bg-black/70 p-0.5 text-white transition-colors hover:bg-danger"
            >
              <X size={13} />
            </button>
          </div>
        ) : (
          <div className="flex h-20 w-20 shrink-0 items-center justify-center rounded-lg border border-dashed border-border text-fg-subtle">
            <ImagePlus size={20} />
          </div>
        )}
        <div>
          <label className="inline-block cursor-pointer rounded-md border border-border-strong px-3 py-1.5 text-xs text-fg-muted transition-colors hover:border-accent/60 hover:text-fg">
            {uploading ? "Uploading…" : preview ? "Replace image" : "Upload image"}
            <input
              type="file"
              accept="image/*"
              className="hidden"
              disabled={uploading}
              onChange={(e) => { const f = e.target.files?.[0]; if (f) upload(f); e.target.value = ""; }}
            />
          </label>
          <p className="mt-1 text-xs text-fg-subtle">Also added to the Assets gallery.</p>
        </div>
      </div>
      {error && <p className="text-xs text-danger">{error}</p>}
    </div>
  );
}
