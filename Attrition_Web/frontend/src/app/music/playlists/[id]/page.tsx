"use client";

import { useParams, useRouter } from "next/navigation";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { ArrowLeft, Play, Pause, Trash2, ChevronUp, ChevronDown, Music as MusicIcon, ListMusic } from "lucide-react";
import { musicApi } from "@/lib/api/music";
import { resolveMediaUrl } from "@/lib/api/media";
import { useAudioStore } from "@/lib/stores/audio-store";
import { useAuth, useToast, useConfirm } from "@/lib/providers";
import { PageShell } from "@/components/ui/page-shell";
import { Button } from "@/components/ui/button";
import { SkeletonList } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { formatDuration } from "@/lib/format-duration";
import { qk } from "@/lib/query-keys";
import type { MusicTrackDto } from "@/lib/types";
import { useLoginHref } from "@/lib/hooks/use-login-href";

export default function PlaylistDetailPage() {
  const loginHref = useLoginHref();
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const { user } = useAuth();
  const { toast } = useToast();
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const { play, pause, resume, currentTrack, isPlaying } = useAudioStore();

  const { data: playlist, isPending } = useQuery({
    queryKey: qk.music.playlist(params.id),
    enabled: !!params.id && !!user,
    queryFn: async () => {
      const res = await musicApi.getPlaylist(params.id);
      return res.success ? res.data : null;
    },
  });

  const tracks = (playlist?.tracks ?? []) as MusicTrackDto[];

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: qk.music.playlist(params.id) });
    queryClient.invalidateQueries({ queryKey: qk.music.playlists() });
  };

  const onRowClick = (track: MusicTrackDto) => {
    if (currentTrack?.trackId === track.trackId) {
      isPlaying ? pause() : resume();
    } else {
      play(track, tracks);
    }
  };

  const playAll = () => {
    if (!tracks.length) return;
    const playingThis = tracks.some((t) => t.trackId === currentTrack?.trackId);
    if (playingThis) { isPlaying ? pause() : resume(); }
    else play(tracks[0], tracks);
  };

  const move = async (index: number, dir: -1 | 1) => {
    const j = index + dir;
    if (j < 0 || j >= tracks.length) return;
    const order = tracks.map((t) => t.trackId);
    [order[index], order[j]] = [order[j], order[index]];
    const res = await musicApi.reorderPlaylist(params.id, order);
    if (!res.success) toast("Couldn't reorder the playlist.", "error");
    invalidate();
  };

  const removeTrack = async (trackId: number, title: string) => {
    const res = await musicApi.removeTrackFromPlaylist(params.id, trackId);
    if (res.success) { toast(`Removed "${title}".`, "success"); invalidate(); }
    else toast(res.error || "Couldn't remove the track.", "error");
  };

  const deletePlaylist = async () => {
    if (!playlist) return;
    const ok = await confirm({
      title: "Delete playlist?",
      message: `"${playlist.title}" will be permanently removed. This can't be undone.`,
      confirmLabel: "Delete",
      danger: true,
    });
    if (!ok) return;
    const res = await musicApi.deletePlaylist(params.id);
    if (res.success) { toast("Playlist deleted.", "success"); router.push("/music/playlists"); }
    else toast(res.error || "Couldn't delete the playlist.", "error");
  };

  if (!user) {
    return (
      <PageShell>
        <EmptyState icon={ListMusic} title="Sign in to view playlists"
          action={<Link href={loginHref}><Button variant="secondary">Sign in</Button></Link>} />
      </PageShell>
    );
  }

  if (isPending) {
    return (
      <PageShell>
        <Skeletonish />
      </PageShell>
    );
  }

  if (!playlist) {
    return (
      <PageShell>
        <EmptyState icon={ListMusic} title="Playlist not found"
          description="This playlist doesn't exist or isn't yours."
          action={<Link href="/music/playlists"><Button variant="secondary">Back to playlists</Button></Link>} />
      </PageShell>
    );
  }

  const totalDuration = tracks.reduce((sum, t) => sum + t.duration, 0);

  return (
    <PageShell>
      <Link href="/music/playlists" className="inline-flex items-center gap-1.5 text-sm text-fg-muted transition-colors hover:text-fg">
        <ArrowLeft size={16} /> Playlists
      </Link>

      <div className="mt-4 flex items-end justify-between gap-4">
        <div className="min-w-0">
          <h1 className="truncate font-display text-3xl font-bold tracking-tight text-fg sm:text-4xl">{playlist.title}</h1>
          <p className="mt-1 text-sm text-fg-muted">
            {tracks.length} {tracks.length === 1 ? "track" : "tracks"}
            {tracks.length > 0 && <> &middot; {formatDuration(totalDuration)}</>}
          </p>
        </div>
        <button onClick={deletePlaylist}
          className="shrink-0 rounded-md p-2 text-fg-subtle transition-colors hover:bg-surface-2 hover:text-danger"
          aria-label="Delete playlist">
          <Trash2 size={18} />
        </button>
      </div>

      {tracks.length === 0 ? (
        <EmptyState
          icon={MusicIcon}
          title="This playlist is empty"
          description="Add tracks from any album or the player using the “Add to playlist” button."
          className="mt-6"
          action={<Link href="/music"><Button variant="secondary">Browse music</Button></Link>}
        />
      ) : (
        <>
          {(() => {
            const playingThis = tracks.some((t) => t.trackId === currentTrack?.trackId);
            const showPause = playingThis && isPlaying;
            return (
              <Button onClick={playAll} className="mt-5">
                {showPause ? <Pause size={16} className="mr-1.5" /> : <Play size={16} className="mr-1.5" />}
                {showPause ? "Pause" : playingThis ? "Resume" : "Play all"}
              </Button>
            );
          })()}

          <div className="mt-6 space-y-0.5">
            {tracks.map((track, i) => {
              const active = currentTrack?.trackId === track.trackId;
              return (
                <div key={track.trackId}
                  className={`group flex items-center gap-3 rounded-lg px-3 py-2.5 transition-colors hover:bg-surface-2 ${active ? "bg-surface-2" : ""}`}>
                  <button onClick={() => onRowClick(track)} className="flex min-w-0 flex-1 items-center gap-3 text-left"
                    aria-label={active && isPlaying ? `Pause ${track.title}` : `Play ${track.title}`}>
                    {track.coverPath || track.albumCoverPath ? (
                      <img src={resolveMediaUrl(track.coverPath ?? track.albumCoverPath) ?? ""} alt="" className="h-10 w-10 shrink-0 rounded object-cover" />
                    ) : (
                      <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded bg-surface-2 text-fg-subtle">
                        {active && isPlaying ? <Pause size={15} className="text-accent" /> : <Play size={15} />}
                      </span>
                    )}
                    <span className="min-w-0">
                      <span className={`block truncate text-sm ${active ? "font-medium text-accent" : "text-fg"}`}>{track.title}</span>
                      <span className="block truncate text-xs text-fg-muted">{track.artists.join(", ")}{track.albumTitle ? ` · ${track.albumTitle}` : ""}</span>
                    </span>
                  </button>

                  {/* Reorder */}
                  <span className="flex shrink-0 flex-col">
                    <button onClick={() => move(i, -1)} disabled={i === 0}
                      className="rounded p-0.5 text-fg-subtle transition-colors hover:text-fg disabled:opacity-30" aria-label={`Move ${track.title} up`}>
                      <ChevronUp size={14} />
                    </button>
                    <button onClick={() => move(i, 1)} disabled={i === tracks.length - 1}
                      className="rounded p-0.5 text-fg-subtle transition-colors hover:text-fg disabled:opacity-30" aria-label={`Move ${track.title} down`}>
                      <ChevronDown size={14} />
                    </button>
                  </span>

                  <span className="hidden shrink-0 text-xs tabular-nums text-fg-subtle sm:inline">{formatDuration(track.duration)}</span>
                  <button onClick={() => removeTrack(track.trackId, track.title)}
                    className="shrink-0 rounded-md p-1.5 text-fg-subtle opacity-0 transition-all hover:bg-surface-3 hover:text-danger group-hover:opacity-100"
                    aria-label={`Remove ${track.title}`}>
                    <Trash2 size={15} />
                  </button>
                </div>
              );
            })}
          </div>
        </>
      )}
    </PageShell>
  );
}

function Skeletonish() {
  return (
    <>
      <div className="h-4 w-16 rounded bg-surface-2" />
      <div className="mt-4 h-9 w-1/2 rounded bg-surface-2" />
      <SkeletonList rows={6} className="mt-6" />
    </>
  );
}
