"use client";

import { useParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { ArrowLeft, Play, Pause, Download, Music as MusicIcon } from "lucide-react";
import { musicApi, getDownloadUrl } from "@/lib/api/music";
import { resolveMediaUrl } from "@/lib/api/media";
import { useAudioStore } from "@/lib/stores/audio-store";
import { PageShell } from "@/components/ui/page-shell";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import type { MusicTrackDto } from "@/lib/types";

export default function AlbumPage() {
  const params = useParams<{ id: string }>();
  const { play, pause, resume, currentTrack, isPlaying } = useAudioStore();

  const { data: album, isPending } = useQuery({
    queryKey: ["album", params.id],
    enabled: !!params.id,
    queryFn: async () => {
      const res = await musicApi.getAlbum(Number(params.id));
      return res.success ? res.data : null;
    },
  });

  if (isPending) {
    return (
      <PageShell size="md">
        <Skeleton className="h-4 w-16" />
        <div className="mt-4 flex gap-6">
          <Skeleton className="h-40 w-40 rounded-xl" />
          <div className="flex-1 space-y-3 py-2">
            <Skeleton className="h-8 w-2/3" />
            <Skeleton className="h-4 w-1/3" />
            <Skeleton className="h-4 w-1/4" />
          </div>
        </div>
      </PageShell>
    );
  }

  if (!album) {
    return (
      <PageShell size="md">
        <EmptyState
          title="Album not found"
          description="This album may have been removed."
          action={<Link href="/music"><Button variant="secondary">Back to Music</Button></Link>}
        />
      </PageShell>
    );
  }

  const formatDuration = (s: number) => {
    const m = Math.floor(s / 60);
    const sec = Math.floor(s % 60);
    return `${m}:${sec.toString().padStart(2, "0")}`;
  };

  const playAll = () => {
    if (!album.tracks.length) return;
    // If a track from this album is already playing, treat the button as pause/resume.
    const playingThisAlbum = album.tracks.some((t) => t.trackId === currentTrack?.trackId);
    if (playingThisAlbum) {
      isPlaying ? pause() : resume();
    } else {
      play(album.tracks[0] as MusicTrackDto, album.tracks as MusicTrackDto[]);
    }
  };

  // Clicking a row: if it's the current track, toggle pause/resume; otherwise start it.
  const onRowClick = (track: MusicTrackDto) => {
    if (currentTrack?.trackId === track.trackId) {
      isPlaying ? pause() : resume();
    } else {
      play(track, album.tracks as MusicTrackDto[]);
    }
  };

  return (
    <PageShell size="md">
      <Link href="/music" className="inline-flex items-center gap-1.5 text-sm text-fg-muted transition-colors hover:text-fg">
        <ArrowLeft size={16} /> Music
      </Link>

      <div className="mt-4 flex flex-col gap-6 sm:flex-row sm:items-end">
        {album.coverPath ? (
          <img src={resolveMediaUrl(album.coverPath) ?? ""} alt="" className="h-44 w-44 rounded-xl object-cover shadow-[var(--shadow-lg)]" />
        ) : (
          <div className="flex h-44 w-44 items-center justify-center rounded-xl bg-surface-2 text-fg-subtle shadow-[var(--shadow-lg)]">
            <MusicIcon size={36} />
          </div>
        )}
        <div className="min-w-0">
          <h1 className="font-display text-3xl font-bold tracking-tight text-fg sm:text-4xl">{album.title}</h1>
          <p className="mt-1 text-sm text-fg-muted">{album.artists.join(", ")}</p>
          {album.description && <p className="mt-2 text-sm text-fg-muted">{album.description}</p>}
          <p className="mt-2 text-sm text-fg-subtle">{album.trackCount} tracks &middot; {formatDuration(album.totalDuration)}</p>
          {album.tracks.length > 0 && (() => {
            const playingThisAlbum = album.tracks.some((t) => t.trackId === currentTrack?.trackId);
            const showPause = playingThisAlbum && isPlaying;
            return (
              <Button onClick={playAll} className="mt-4">
                {showPause ? <Pause size={16} className="mr-1.5" /> : <Play size={16} className="mr-1.5" />}
                {showPause ? "Pause" : playingThisAlbum ? "Resume" : "Play album"}
              </Button>
            );
          })()}
        </div>
      </div>

      <div className="mt-8 space-y-0.5">
        {album.tracks.map((track) => {
          const active = currentTrack?.trackId === track.trackId;
          return (
            <div
              key={track.trackId}
              className={`group flex items-center gap-3 rounded-lg px-3 py-2.5 transition-colors hover:bg-surface-2 ${active ? "bg-surface-2" : ""}`}
            >
              <button
                onClick={() => onRowClick(track as MusicTrackDto)}
                className="flex min-w-0 flex-1 items-center gap-3 text-left"
                aria-label={active && isPlaying ? `Pause ${track.title}` : `Play ${track.title}`}
              >
                <span className="flex w-6 shrink-0 items-center justify-center text-sm text-fg-subtle">
                  {active && isPlaying ? (
                    <Pause size={14} className="text-accent" />
                  ) : (
                    <>
                      <span className="tabular-nums group-hover:hidden">{track.trackNumber}</span>
                      <Play size={14} className="hidden text-fg group-hover:block" />
                    </>
                  )}
                </span>
                <span className={`truncate text-sm ${active ? "font-medium text-accent" : "text-fg"}`}>{track.title}</span>
              </button>
              <span className="shrink-0 text-xs tabular-nums text-fg-subtle">{formatDuration(track.duration)}</span>
              <a
                href={getDownloadUrl(track.trackId)}
                className="shrink-0 rounded-md p-1.5 text-fg-subtle opacity-0 transition-all hover:bg-surface-3 hover:text-fg group-hover:opacity-100"
                aria-label={`Download ${track.title}`}
              >
                <Download size={15} />
              </a>
            </div>
          );
        })}
        {album.tracks.length === 0 && (
          <p className="py-8 text-center text-sm text-fg-muted">No tracks in this album.</p>
        )}
      </div>
    </PageShell>
  );
}
