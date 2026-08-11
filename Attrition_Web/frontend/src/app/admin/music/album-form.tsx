"use client";

import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { musicApi } from "@/lib/api/music";
import { resolveMediaUrl } from "@/lib/api/media";
import { parseApiError } from "@/lib/api/parse-error";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

const albumSchema = z.object({
  title: z.string().min(1, "Title is required."),
  description: z.string(),
  // Lower shows first in album lists; ties break by newest. Coerced from the number input.
  sortOrder: z.coerce.number().int("Must be a whole number.").min(0, "Cannot be negative."),
});
type AlbumFormValues = z.infer<typeof albumSchema>;

/** The subset of album fields the form edits — both the list and detail DTOs satisfy it. */
interface AlbumEditable {
  albumId: number;
  title: string;
  description: string | null;
  /** The album detail response omits sortOrder (backend gap), so it's optional here. */
  sortOrder?: number;
  coverPath: string | null;
}

export function AlbumForm({ initial, onDone, onCancel, onDirtyChange }: {
  initial?: AlbumEditable; onDone: () => void; onCancel: () => void; onDirtyChange?: (dirty: boolean) => void;
}) {
  const [error, setError] = useState<string | null>(null);
  const [coverFile, setCoverFile] = useState<File | null>(null);
  const [uploadingCover, setUploadingCover] = useState(false);
  const {
    register, handleSubmit, watch,
    formState: { errors, isSubmitting },
  } = useForm<AlbumFormValues>({
    resolver: zodResolver(albumSchema),
    defaultValues: { title: initial?.title ?? "", description: initial?.description ?? "", sortOrder: initial?.sortOrder ?? 0 },
  });

  // Report dirty (any field touched) so the Modal can guard an accidental close.
  const dirty = !!(watch("title") || watch("description") || coverFile);
  onDirtyChange?.(dirty);

  const onSubmit = handleSubmit(async (values) => {
    setError(null);
    try {
      if (initial) {
        await musicApi.updateAlbum(initial.albumId, { title: values.title, description: values.description || undefined, sortOrder: values.sortOrder });
      } else {
        await musicApi.createAlbum({ title: values.title, description: values.description || undefined, sortOrder: values.sortOrder });
      }
      onDirtyChange?.(false);
      onDone();
    } catch (err) {
      setError(parseApiError(err, initial ? "Failed to update the album. Please try again." : "Failed to create the album. Please try again."));
    }
  });

  const uploadCover = async () => {
    if (!initial || !coverFile) return;
    setUploadingCover(true);
    setError(null);
    try {
      await musicApi.uploadAlbumCover(initial.albumId, coverFile);
      setCoverFile(null);
      onDirtyChange?.(false);
      onDone();
    } catch (err) {
      setError(parseApiError(err, "Failed to upload the cover."));
    } finally {
      setUploadingCover(false);
    }
  };

  const coverUrl = initial ? resolveMediaUrl(initial.coverPath) : null;

  return (
    <form onSubmit={onSubmit} className="space-y-3">
      {error && <p className="rounded-md bg-danger/10 px-3 py-2 text-sm text-danger">{error}</p>}
      <Input label="Title" error={errors.title?.message} {...register("title")} />
      <Input label="Description" {...register("description")} />
      <div>
        <Input label="Sort order" type="number" min={0} error={errors.sortOrder?.message} {...register("sortOrder")} />
        <p className="mt-1 text-xs text-fg-subtle">Lower numbers appear first in album lists; ties break by newest.</p>
      </div>

      {/* Cover — only when editing an existing album (create has no album id to attach to yet). */}
      {initial && (
        <div className="flex items-center gap-3 rounded-lg border border-border bg-surface-2/40 p-3">
          {coverUrl ? (
            <img src={coverUrl} alt="" className="h-14 w-14 shrink-0 rounded object-cover" />
          ) : (
            <div className="flex h-14 w-14 shrink-0 items-center justify-center rounded bg-surface-3 text-xs text-fg-subtle">No art</div>
          )}
          <div className="min-w-0 flex-1">
            <label className="inline-flex cursor-pointer items-center gap-1.5 rounded-md border border-border-strong px-3 py-1.5 text-sm text-fg-muted transition-colors hover:border-accent/60 hover:text-fg">
              {uploadingCover ? "Uploading…" : coverFile ? "Replace file" : "Upload cover"}
              <input type="file" accept="image/*" className="hidden" disabled={uploadingCover}
                onChange={(e) => { const f = e.target.files?.[0]; e.target.value = ""; if (f) setCoverFile(f); }} />
            </label>
            {coverFile && (
              <Button size="sm" onClick={uploadCover} loading={uploadingCover} className="ml-2">
                Save cover
              </Button>
            )}
            {coverFile && (
              <button type="button" onClick={() => setCoverFile(null)} className="ml-2 text-xs text-fg-muted hover:text-fg">Cancel</button>
            )}
          </div>
        </div>
      )}

      <div className="flex gap-2">
        <Button type="submit" loading={isSubmitting}>{initial ? "Save Changes" : "Create Album"}</Button>
        <Button type="button" variant="secondary" onClick={onCancel}>Cancel</Button>
      </div>
    </form>
  );
}
