"use client";

import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useAuth, useConfirm } from "@/lib/providers";
import { musicApi } from "@/lib/api/music";
import { resolveMediaUrl } from "@/lib/api/media";
import { parseApiError } from "@/lib/api/parse-error";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { PageLoader } from "@/components/ui/spinner";
import { qk } from "@/lib/query-keys";
import { TrackUploadFlow } from "./track-upload-flow";

export default function AdminMusicPage() {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const confirm = useConfirm();
  const [showAlbumForm, setShowAlbumForm] = useState(false);
  const [showTrackUpload, setShowTrackUpload] = useState(false);

  const { data: albums = [], isPending: albumsLoading } = useQuery({
    queryKey: qk.admin.music.albums(),
    enabled: user?.role === "Admin",
    queryFn: async () => {
      const res = await musicApi.getAlbums();
      return res.success ? res.data : [];
    },
  });

  const { data: tracks = [], isPending: tracksLoading } = useQuery({
    queryKey: qk.admin.music.tracks(),
    enabled: user?.role === "Admin",
    queryFn: async () => {
      const res = await musicApi.getTracks();
      return res.success ? res.data : [];
    },
  });

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: qk.admin.music.albums() });
    queryClient.invalidateQueries({ queryKey: qk.admin.music.tracks() });
  };

  const deleteAlbumMutation = useMutation({
    mutationFn: async (id: number) => { await musicApi.deleteAlbum(id); },
    onSuccess: invalidate,
  });

  const deleteTrackMutation = useMutation({
    mutationFn: async (id: number) => { await musicApi.deleteTrack(id); },
    onSuccess: invalidate,
  });

  const handleDeleteAlbum = async (id: number) => {
    if (!(await confirm({ message: "Delete this album?", danger: true, confirmLabel: "Delete" }))) return;
    deleteAlbumMutation.mutate(id);
  };

  const handleDeleteTrack = async (id: number) => {
    if (!(await confirm({ message: "Delete this track?", danger: true, confirmLabel: "Delete" }))) return;
    deleteTrackMutation.mutate(id);
  };

  if (!user || user.role !== "Admin") return null;
  if (albumsLoading || tracksLoading) return <PageLoader />;

  return (
    <div className="mx-auto max-w-6xl">
      <h1 className="font-display text-3xl font-bold text-fg">Music Management</h1>
      <p className="mt-2 text-fg-muted">
        Upload a track and we scan its tags first — album is auto-created from metadata, and cover art
        is taken from the embedded image (or the lowest track number in the album).
      </p>

      <section className="mt-8">
        <div className="flex items-center justify-between">
          <h2 className="text-xl font-semibold text-fg">Albums ({albums.length})</h2>
          <Button size="sm" onClick={() => setShowAlbumForm(true)}>New Album</Button>
        </div>
        {showAlbumForm && (
          <AlbumForm onDone={() => { setShowAlbumForm(false); invalidate(); }} onCancel={() => setShowAlbumForm(false)} />
        )}
        <div className="mt-4 space-y-2">
          {albums.map((album) => (
            <div key={album.albumId} className="card flex items-center gap-4 p-3">
              {album.coverPath
                ? <img src={resolveMediaUrl(album.coverPath) ?? ""} alt="" className="h-12 w-12 rounded object-cover" />
                : <div className="h-12 w-12 rounded bg-surface-2" />}
              <div className="min-w-0 flex-1">
                <p className="font-medium text-fg">{album.title}</p>
                <p className="text-xs text-fg-muted">{album.artists.join(", ")} · {album.trackCount} tracks</p>
              </div>
              <Button size="sm" variant="danger" onClick={() => handleDeleteAlbum(album.albumId)}>Delete</Button>
            </div>
          ))}
        </div>
      </section>

      <section className="mt-10">
        <div className="flex items-center justify-between">
          <h2 className="text-xl font-semibold text-fg">Tracks ({tracks.length})</h2>
          <Button size="sm" onClick={() => setShowTrackUpload(true)}>Upload Track</Button>
        </div>
        {showTrackUpload && (
          <TrackUploadFlow albums={albums} onDone={() => { setShowTrackUpload(false); invalidate(); }} onCancel={() => setShowTrackUpload(false)} />
        )}
        <div className="mt-4 space-y-2">
          {tracks.map((track) => (
            <div key={track.trackId} className="card flex items-center gap-4 p-3">
              <div className="min-w-0 flex-1">
                <p className="font-medium text-fg">{track.title}</p>
                <p className="text-xs text-fg-muted">{track.albumTitle} · #{track.trackNumber}</p>
              </div>
              <Button size="sm" variant="danger" onClick={() => handleDeleteTrack(track.trackId)}>Delete</Button>
            </div>
          ))}
        </div>
      </section>
    </div>
  );
}

const albumSchema = z.object({
  title: z.string().min(1, "Title is required."),
  description: z.string(),
});

type AlbumFormValues = z.infer<typeof albumSchema>;

function AlbumForm({ onDone, onCancel }: { onDone: () => void; onCancel: () => void }) {
  const [error, setError] = useState<string | null>(null);

  const {
    register, handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<AlbumFormValues>({
    resolver: zodResolver(albumSchema),
    defaultValues: { title: "", description: "" },
  });

  const onSubmit = handleSubmit(async (values) => {
    setError(null);
    try {
      await musicApi.createAlbum({ title: values.title, description: values.description || undefined });
      onDone();
    } catch (err) {
      setError(parseApiError(err, "Failed to create the album. Please try again."));
    }
  });

  return (
    <form onSubmit={onSubmit} className="card mt-4 space-y-3 p-4">
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
