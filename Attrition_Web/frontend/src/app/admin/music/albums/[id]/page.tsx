"use client";

import { useParams } from "next/navigation";
import Link from "next/link";
import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, Music as MusicIcon, Play, Pause, Disc3, Upload } from "lucide-react";
import { useAuth, useConfirm } from "@/lib/providers";
import { musicApi } from "@/lib/api/music";
import { useAudioStore } from "@/lib/stores/audio-store";
import { resolveMediaUrl } from "@/lib/api/media";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Modal } from "@/components/ui/modal";
import { PageLoader } from "@/components/ui/spinner";
import { EmptyState } from "@/components/ui/empty-state";
import { qk } from "@/lib/query-keys";
import { AlbumForm } from "../../album-form";
import { TrackEditForm } from "../../track-edit-form";
import { TrackUploadFlow } from "../../track-upload-flow";
import type { MusicTrackDto } from "@/lib/types";

const fmtDuration = (s: number) => {
  const m = Math.floor(s / 60);
  const sec = Math.floor(s % 60);
  return `${m}:${sec.toString().padStart(2, "0")}`;
};

export default function AdminAlbumDetailPage() {
  const params = useParams<{ id: string }>();
  const { user } = useAuth();
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const { play, pause, resume, currentTrack, isPlaying } = useAudioStore();
  const [showAlbumEdit, setShowAlbumEdit] = useState(false);
  const [showUpload, setShowUpload] = useState(false);
  const [editingTrack, setEditingTrack] = useState<MusicTrackDto | null>(null);

  const { data: album, isPending } = useQuery({
    queryKey: qk.admin.music.album(params.id),
    enabled: user?.role === "Admin" && !!params.id,
    queryFn: async () => {
      const res = await musicApi.getAlbum(Number(params.id));
      return res.success ? res.data : null;
    },
  });

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: qk.admin.music.album(params.id) });
    queryClient.invalidateQueries({ queryKey: qk.admin.music.albums() });
  };

  const deleteTrack = useMutation({
    mutationFn: (trackId: number) => musicApi.deleteTrack(trackId),
    onSuccess: invalidate,
  });

  const onDeleteTrack = async (trackId: number) => {
    if (!(await confirm({ message: "Delete this track?", danger: true, confirmLabel: "Delete" }))) return;
    deleteTrack.mutate(trackId);
  };

  const toggleTrack = (t: MusicTrackDto) => {
    if (currentTrack?.trackId === t.trackId) {
      if (isPlaying) pause(); else resume();
      return;
    }
    play(t, album!.tracks);
  };

  if (!user || user.role !== "Admin") return null;
  if (isPending) return <PageLoader />;
  if (!album) {
    return (
      <EmptyState
        title="Album not found"
        description="This album may have been removed."
        action={<Link href="/admin/music/albums"><Button variant="secondary">Back to albums</Button></Link>}
      />
    );
  }

  return (
    <div>
      <Link href="/admin/music/albums" className="inline-flex items-center gap-1.5 text-sm text-fg-muted transition-colors hover:text-fg">
        <ArrowLeft size={16} /> Music
      </Link>

      {/* Album details */}
      <Card className="mt-4 flex flex-col gap-4 p-4 sm:flex-row sm:items-center">
        {album.coverPath
          ? <img src={resolveMediaUrl(album.coverPath) ?? ""} alt="" className="h-24 w-24 shrink-0 rounded-lg object-cover" />
          : <div className="flex h-24 w-24 shrink-0 items-center justify-center rounded-lg bg-surface-2 text-fg-subtle"><MusicIcon size={28} /></div>}
        <div className="min-w-0 flex-1">
          <h1 className="font-display text-2xl font-bold text-fg">{album.title}</h1>
          <p className="mt-0.5 text-sm text-fg-muted">{album.artists.join(", ")}</p>
          {album.description && <p className="mt-1 text-sm text-fg-subtle">{album.description}</p>}
          <p className="mt-1 text-xs text-fg-subtle">{album.trackCount} tracks · {fmtDuration(album.totalDuration)}</p>
        </div>
        <div className="flex shrink-0 flex-col gap-2">
          <Button size="sm" variant="secondary" onClick={() => setShowUpload(true)}>
            <Upload size={14} className="mr-1.5" /> Upload track
          </Button>
          <Button size="sm" variant="secondary" onClick={() => setShowAlbumEdit(true)}>Edit Album</Button>
        </div>
      </Card>

      <Modal open={showUpload} onClose={() => setShowUpload(false)} title="Upload Track">
        <TrackUploadFlow
          albums={album ? [{ albumId: album.albumId, title: album.title }] : []}
          defaultAlbumId={album?.albumId}
          onDone={() => { setShowUpload(false); invalidate(); }}
          onCancel={() => setShowUpload(false)}
        />
      </Modal>

      <Modal open={showAlbumEdit} onClose={() => setShowAlbumEdit(false)} title="Edit Album">
        <AlbumForm
          initial={album}
          onDone={() => { setShowAlbumEdit(false); invalidate(); }}
          onCancel={() => setShowAlbumEdit(false)}
        />
      </Modal>

      <Modal open={editingTrack != null} onClose={() => setEditingTrack(null)} title="Edit Track">
        {editingTrack && (
          <TrackEditForm
            track={editingTrack}
            onDone={() => { setEditingTrack(null); invalidate(); }}
            onCancel={() => setEditingTrack(null)}
          />
        )}
      </Modal>

      {/* Songs in the album — this is where tracks live and play from now */}
      <div className="mt-6 flex items-center justify-between">
        <h2 className="text-sm font-semibold uppercase tracking-wider text-fg-subtle">Tracks</h2>
        {album.tracks.length > 0 && (
          <button
            onClick={() => { play(album.tracks[0], album.tracks); }}
            className="inline-flex items-center gap-1.5 text-xs font-medium text-accent transition-colors hover:text-accent/80"
          >
            <Play size={13} fill="currentColor" /> Play all
          </button>
        )}
      </div>
      {album.tracks.length === 0 ? (
        <p className="mt-2 rounded-lg border border-border py-8 text-center text-sm text-fg-muted">No tracks in this album yet. Upload the first one.</p>
      ) : (
        <div className="mt-2 overflow-hidden rounded-lg border border-border">
          {album.tracks.map((t) => {
            const active = currentTrack?.trackId === t.trackId;
            return (
              <div key={t.trackId} className="flex items-center gap-3 border-b border-border/40 px-3 py-2.5 last:border-0 hover:bg-surface-2/40">
                <button
                  onClick={() => toggleTrack(t)}
                  aria-label={active && isPlaying ? `Pause ${t.title}` : `Play ${t.title}`}
                  title={active && isPlaying ? "Pause" : "Play"}
                  className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-full transition-colors ${
                    active ? "bg-accent text-accent-fg" : "bg-surface-2 text-fg-muted hover:bg-accent hover:text-accent-fg"
                  }`}
                >
                  {active && isPlaying ? <Pause size={13} fill="currentColor" /> : <Play size={13} fill="currentColor" className="ml-px" />}
                </button>
                <span className="w-6 shrink-0 text-center text-sm tabular-nums text-fg-subtle">{t.trackNumber}</span>
                <span className={`min-w-0 flex-1 truncate text-sm ${active ? "font-semibold text-accent" : "text-fg"}`}>{t.title}</span>
                {t.isFeatured && <span className="shrink-0 rounded bg-accent-soft px-1.5 py-0.5 text-[10px] font-medium text-accent">Featured</span>}
                <span className="shrink-0 text-xs tabular-nums text-fg-subtle">{fmtDuration(t.duration)}</span>
                <div className="flex shrink-0 gap-1.5">
                  <Button size="sm" variant="secondary" onClick={() => setEditingTrack(t)}>Edit</Button>
                  <Button size="sm" variant="danger" onClick={() => onDeleteTrack(t.trackId)}>Delete</Button>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
