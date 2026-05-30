"use client";

import { useEffect, useState } from "react";
import { useAuth } from "@/lib/providers";
import { musicApi } from "@/lib/api/music";
import { resolveMediaUrl } from "@/lib/api/media";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { PageLoader } from "@/components/ui/spinner";
import type { MusicAlbumDto, MusicTrackDto } from "@/lib/types";
import { TrackUploadFlow } from "./track-upload-flow";

export default function AdminMusicPage() {
  const { user } = useAuth();
  const [albums, setAlbums] = useState<MusicAlbumDto[]>([]);
  const [tracks, setTracks] = useState<MusicTrackDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [showAlbumForm, setShowAlbumForm] = useState(false);
  const [showTrackUpload, setShowTrackUpload] = useState(false);

  const fetchData = () => {
    setLoading(true);
    Promise.all([musicApi.getAlbums(), musicApi.getTracks()])
      .then(([albumsRes, tracksRes]) => {
        if (albumsRes.success) setAlbums(albumsRes.data);
        if (tracksRes.success) setTracks(tracksRes.data);
      })
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    if (user?.role !== "Admin") return;
    fetchData();
  }, [user]);

  const handleDeleteAlbum = async (id: number) => {
    if (!confirm("Delete this album?")) return;
    await musicApi.deleteAlbum(id);
    fetchData();
  };

  const handleDeleteTrack = async (id: number) => {
    if (!confirm("Delete this track?")) return;
    await musicApi.deleteTrack(id);
    fetchData();
  };

  if (!user || user.role !== "Admin") return null;
  if (loading) return <PageLoader />;

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
          <AlbumForm onDone={() => { setShowAlbumForm(false); fetchData(); }} onCancel={() => setShowAlbumForm(false)} />
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
          <TrackUploadFlow albums={albums} onDone={() => { setShowTrackUpload(false); fetchData(); }} onCancel={() => setShowTrackUpload(false)} />
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
  const [saving, setSaving] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!title) return;
    setSaving(true);
    try { await musicApi.createAlbum({ title, description: description || undefined }); onDone(); }
    catch {} finally { setSaving(false); }
  };

  return (
    <form onSubmit={handleSubmit} className="card mt-4 space-y-3 p-4">
      <Input label="Title" value={title} onChange={(e) => setTitle(e.target.value)} />
      <Input label="Description" value={description} onChange={(e) => setDescription(e.target.value)} />
      <div className="flex gap-2">
        <Button type="submit" loading={saving} disabled={!title}>Create Album</Button>
        <Button type="button" variant="secondary" onClick={onCancel}>Cancel</Button>
      </div>
    </form>
  );
}
