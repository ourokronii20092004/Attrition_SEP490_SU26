"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@/lib/providers";
import { musicApi } from "@/lib/api/music";
import { resolveMediaUrl } from "@/lib/api/media";
import { parseApiError } from "@/lib/api/parse-error";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { PageLoader } from "@/components/ui/spinner";
import { TrackUploadFlow } from "./track-upload-flow";

export default function AdminMusicPage() {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const [showAlbumForm, setShowAlbumForm] = useState(false);
  const [showTrackUpload, setShowTrackUpload] = useState(false);

  const { data: albums = [], isPending: albumsLoading } = useQuery({
    queryKey: ["admin", "music", "albums"],
    enabled: user?.role === "Admin",
    queryFn: async () => {
      const res = await musicApi.getAlbums();
      return res.success ? res.data : [];
    },
  });

  const { data: tracks = [], isPending: tracksLoading } = useQuery({
    queryKey: ["admin", "music", "tracks"],
    enabled: user?.role === "Admin",
    queryFn: async () => {
      const res = await musicApi.getTracks();
      return res.success ? res.data : [];
    },
  });

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ["admin", "music", "albums"] });
    queryClient.invalidateQueries({ queryKey: ["admin", "music", "tracks"] });
  };

  const deleteAlbumMutation = useMutation({
    mutationFn: async (id: number) => { await musicApi.deleteAlbum(id); },
    onSuccess: invalidate,
  });

  const deleteTrackMutation = useMutation({
    mutationFn: async (id: number) => { await musicApi.deleteTrack(id); },
    onSuccess: invalidate,
  });

  const handleDeleteAlbum = (id: number) => {
    if (!confirm("Delete this album?")) return;
    deleteAlbumMutation.mutate(id);
  };

  const handleDeleteTrack = (id: number) => {
    if (!confirm("Delete this track?")) return;
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

function AlbumForm({ onDone, onCancel }: { onDone: () => void; onCancel: () => void }) {
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [error, setError] = useState<string | null>(null);

  const createMutation = useMutation({
    mutationFn: async () => {
      await musicApi.createAlbum({ title, description: description || undefined });
    },
    onSuccess: () => {
      onDone();
    },
    onError: (err) => {
      setError(parseApiError(err, "Failed to create the album. Please try again."));
    },
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!title) return;
    setError(null);
    createMutation.mutate();
  };

  return (
    <form onSubmit={handleSubmit} className="card mt-4 space-y-3 p-4">
      {error && <p className="rounded-md bg-danger/10 px-3 py-2 text-sm text-danger">{error}</p>}
      <Input label="Title" value={title} onChange={(e) => setTitle(e.target.value)} />
      <Input label="Description" value={description} onChange={(e) => setDescription(e.target.value)} />
      <div className="flex gap-2">
        <Button type="submit" loading={createMutation.isPending} disabled={!title}>Create Album</Button>
        <Button type="button" variant="secondary" onClick={onCancel}>Cancel</Button>
      </div>
    </form>
  );
}
