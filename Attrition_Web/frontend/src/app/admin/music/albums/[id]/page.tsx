"use client";

import { useParams } from "next/navigation";
import Link from "next/link";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, Music as MusicIcon } from "lucide-react";
import { useAuth, useConfirm } from "@/lib/providers";
import { musicApi } from "@/lib/api/music";
import { resolveMediaUrl } from "@/lib/api/media";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { PageLoader } from "@/components/ui/spinner";
import { EmptyState } from "@/components/ui/empty-state";
import { qk } from "@/lib/query-keys";

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

  const { data: album, isPending } = useQuery({
    queryKey: qk.admin.music.album(params.id),
    enabled: user?.role === "Admin" && !!params.id,
    queryFn: async () => {
      const res = await musicApi.getAlbum(Number(params.id));
      return res.success ? res.data : null;
    },
  });

  const deleteTrack = useMutation({
    mutationFn: (trackId: number) => musicApi.deleteTrack(trackId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: qk.admin.music.album(params.id) }),
  });

  const onDeleteTrack = async (trackId: number) => {
    if (!(await confirm({ message: "Delete this track?", danger: true, confirmLabel: "Delete" }))) return;
    deleteTrack.mutate(trackId);
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
        <ArrowLeft size={16} /> Albums
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
      </Card>

      {/* Songs in the album, listed underneath the album details */}
      <h2 className="mt-6 text-sm font-semibold uppercase tracking-wider text-fg-subtle">Tracks</h2>
      {album.tracks.length === 0 ? (
        <p className="mt-2 rounded-lg border border-border py-8 text-center text-sm text-fg-muted">No tracks in this album yet.</p>
      ) : (
        <div className="mt-2 overflow-hidden rounded-lg border border-border">
          {album.tracks.map((t) => (
            <div key={t.trackId} className="flex items-center gap-3 border-b border-border/40 px-3 py-2.5 last:border-0 hover:bg-surface-2/40">
              <span className="w-6 shrink-0 text-center text-sm tabular-nums text-fg-subtle">{t.trackNumber}</span>
              <span className="min-w-0 flex-1 truncate text-sm text-fg">{t.title}</span>
              {t.isFeatured && <span className="shrink-0 rounded bg-accent-soft px-1.5 py-0.5 text-[10px] font-medium text-accent">Featured</span>}
              <span className="shrink-0 text-xs tabular-nums text-fg-subtle">{fmtDuration(t.duration)}</span>
              <Button size="sm" variant="danger" onClick={() => onDeleteTrack(t.trackId)}>Delete</Button>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
