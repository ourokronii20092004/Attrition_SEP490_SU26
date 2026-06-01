"use client";

import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { musicApi } from "@/lib/api/music";
import { parseApiError } from "@/lib/api/parse-error";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

const albumSchema = z.object({
  title: z.string().min(1, "Title is required."),
  description: z.string(),
});
type AlbumFormValues = z.infer<typeof albumSchema>;

export function AlbumForm({ onDone, onCancel, onDirtyChange }: {
  onDone: () => void; onCancel: () => void; onDirtyChange?: (dirty: boolean) => void;
}) {
  const [error, setError] = useState<string | null>(null);
  const {
    register, handleSubmit, watch,
    formState: { errors, isSubmitting },
  } = useForm<AlbumFormValues>({
    resolver: zodResolver(albumSchema),
    defaultValues: { title: "", description: "" },
  });

  // Report dirty (any field touched) so the Modal can guard an accidental close.
  const dirty = !!(watch("title") || watch("description"));
  onDirtyChange?.(dirty);

  const onSubmit = handleSubmit(async (values) => {
    setError(null);
    try {
      await musicApi.createAlbum({ title: values.title, description: values.description || undefined });
      onDirtyChange?.(false);
      onDone();
    } catch (err) {
      setError(parseApiError(err, "Failed to create the album. Please try again."));
    }
  });

  return (
    <form onSubmit={onSubmit} className="space-y-3">
      {error && <p className="rounded-md bg-danger/10 px-3 py-2 text-sm text-danger">{error}</p>}
      <Input label="Title" error={errors.title?.message} {...register("title")} />
      <Input label="Description" {...register("description")} />
      <div className="flex gap-2">
        <Button type="submit" loading={isSubmitting}>Create Album</Button>
        <Button type="button" variant="secondary" onClick={onCancel}>Cancel</Button>
      </div>
    </form>
  );
}
