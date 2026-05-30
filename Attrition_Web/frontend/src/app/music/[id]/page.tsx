"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { Play } from "lucide-react";
import { musicApi } from "@/lib/api/music";
import { resolveMediaUrl } from "@/lib/api/media";
import { useAudioStore } from "@/lib/stores/audio-store";
import { PageLoader } from "@/components/ui/spinner";
import type { AlbumDetailDto, MusicTrackDto } from "@/lib/types";

export default function AlbumPage() {
  const params = useParams<{ id: string }>();
  const [album, setAlbum] = useState<AlbumDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const { play, currentTrack, isPlaying } = useAudioStore();

  useEffect(() => {
    if (!params.id) return;
    let ignore = false;
    setLoading(true);
    musicApi.getAlbum(Number(params.id))
      .then((res) => {
        if (!ignore && res.success) setAlbum(res.data);
      })
      .finally(() => { if (!ignore) setLoading(false); });
    return () => { ignore = true; };
  }, [params.id]);

  if (loading) return <PageLoader />;
  if (!album) {
    return (
      <div className="mx-auto max-w-3xl px-4 py-12 text-center">
        <h1 className="font-display text-3xl font-bold text-fg">Album Not Found</h1>
        <Link href="/music" className="mt-4 inline-block text-accent hover:underline">Back to Music</Link>
      </div>
    );
  }

  const formatDuration = (s: number) => {
    const m = Math.floor(s / 60);
    const sec = Math.floor(s % 60);
    return `${m}:${sec.toString().padStart(2, "0")}`;
  };

  return (
    <div className="mx-auto max-w-3xl px-4 py-8">
      <Link href="/music" className="text-sm text-accent hover:underline">&larr; Music</Link>

      <div className="mt-4 flex gap-6">
        {album.coverPath ? (
          <img src={resolveMediaUrl(album.coverPath) ?? ""} alt="" className="h-40 w-40 rounded-lg object-cover shadow-lg" />
        ) : (
          <div className="flex h-40 w-40 items-center justify-center rounded-lg bg-surface-2 text-fg-subtle">No Cover</div>
        )}
        <div>
          <h1 className="font-display text-3xl font-bold text-fg">{album.title}</h1>
          <p className="mt-1 text-sm text-fg-muted">{album.artists.join(", ")}</p>
          {album.description && <p className="mt-2 text-fg-muted">{album.description}</p>}
          <p className="mt-2 text-sm text-fg-subtle">{album.trackCount} tracks &middot; {formatDuration(album.totalDuration)}</p>
        </div>
      </div>

      <div className="mt-8 space-y-1">
        {album.tracks.map((track) => {
          const active = currentTrack?.trackId === track.trackId;
          return (
            <button
              key={track.trackId}
              onClick={() => play(track, album.tracks as MusicTrackDto[])}
              className={`flex w-full items-center gap-3 rounded-lg px-4 py-3 text-left transition hover:bg-surface-2 ${active ? "bg-surface-2" : ""}`}
            >
              <span className="w-6 text-center text-sm text-fg-subtle">{track.trackNumber}</span>
              <div className="flex-1 min-w-0">
                <p className={`truncate text-sm ${active ? "font-medium text-accent" : "text-fg"}`}>{track.title}</p>
              </div>
              <span className="text-xs text-fg-subtle">{formatDuration(track.duration)}</span>
              {active && isPlaying && (
                <span className="text-xs text-accent">Playing</span>
              )}
              {!active && (
                <Play size={14} className="text-fg-subtle" />
              )}
            </button>
          );
        })}
        {album.tracks.length === 0 && <p className="py-8 text-center text-fg-muted">No tracks in this album.</p>}
      </div>
    </div>
  );
}
