"use client";

import { useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { ArrowLeft, Play, Pause, Heart, Headphones, Music as MusicIcon } from "lucide-react";
import { musicApi } from "@/lib/api/music";
import { resolveMediaUrl } from "@/lib/api/media";
import { useAudioStore } from "@/lib/stores/audio-store";
import { useFavorites } from "@/components/player/use-favorites";
import { useAuth } from "@/lib/providers";
import { PageShell } from "@/components/ui/page-shell";
import { Button } from "@/components/ui/button";
import { SkeletonList } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { qk } from "@/lib/query-keys";
import type { MusicTrackDto, FavoriteTrackDto } from "@/lib/types";

const formatDuration = (s: number) => {
  const m = Math.floor(s / 60);
  const sec = Math.floor(s % 60);
  return `${m}:${sec.toString().padStart(2, "0")}`;
};
const formatPlays = (n: number) => (n >= 1000 ? `${(n / 1000).toFixed(1)}k` : String(n));

// A FavoriteTrackDto carries everything the player needs; map to the MusicTrackDto shape.
function toPlayable(f: FavoriteTrackDto): MusicTrackDto {
  return {
    trackId: f.trackId, albumId: f.albumId, title: f.title, slug: f.slug,
    trackNumber: f.trackNumber, artists: f.artists, duration: f.duration, genre: f.genre,
    coverPath: f.coverPath, playCount: f.playCount, isFeatured: false, fileSize: 0,
    albumTitle: f.albumTitle, albumCoverPath: f.albumCoverPath,
  };
}

export default function FavoritesPage() {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const { play, pause, resume, currentTrack, isPlaying } = useAudioStore();
  const { toggle } = useFavorites();

  const { data: favorites = [], isPending } = useQuery({
    queryKey: qk.music.favorites(),
    enabled: !!user,
    queryFn: async () => {
      const res = await musicApi.getFavorites();
      return res.success ? res.data ?? [] : [];
    },
  });

  // Unfavorite from this page: flip via the shared hook, then refresh the list so the row leaves.
  const unfavorite = async (trackId: number) => {
    await toggle(trackId);
    queryClient.invalidateQueries({ queryKey: qk.music.favorites() });
  };

  if (!user) {
    return (
      <PageShell>
        <EmptyState
          icon={Heart}
          title="Sign in to see your favorites"
          description="Favorite tracks you love and they'll collect here."
          action={<Link href="/login"><Button variant="secondary">Sign in</Button></Link>}
        />
      </PageShell>
    );
  }

  const playable = favorites.map(toPlayable);
  const onRowClick = (track: MusicTrackDto) => {
    if (currentTrack?.trackId === track.trackId) {
      isPlaying ? pause() : resume();
    } else {
      play(track, playable);
    }
  };

  return (
    <PageShell>
      <Link href="/music" className="inline-flex items-center gap-1.5 text-sm text-fg-muted transition-colors hover:text-fg">
        <ArrowLeft size={16} /> Music
      </Link>
      <h1 className="mt-4 font-display text-3xl font-bold tracking-tight text-fg sm:text-4xl">My Favorites</h1>
      <p className="mt-1 text-sm text-fg-muted">{favorites.length} {favorites.length === 1 ? "track" : "tracks"}</p>

      {isPending ? (
        <SkeletonList rows={6} className="mt-6" />
      ) : favorites.length === 0 ? (
        <EmptyState
          icon={MusicIcon}
          title="No favorites yet"
          description="Tap the heart on any track to save it here."
          className="mt-6"
          action={<Link href="/music"><Button variant="secondary">Browse music</Button></Link>}
        />
      ) : (
        <div className="mt-6 space-y-0.5">
          {favorites.map((f) => {
            const track = toPlayable(f);
            const active = currentTrack?.trackId === f.trackId;
            return (
              <div
                key={f.trackId}
                className={`group flex items-center gap-3 rounded-lg px-3 py-2.5 transition-colors hover:bg-surface-2 ${active ? "bg-surface-2" : ""}`}
              >
                <button onClick={() => onRowClick(track)} className="flex min-w-0 flex-1 items-center gap-3 text-left"
                  aria-label={active && isPlaying ? `Pause ${f.title}` : `Play ${f.title}`}>
                  {f.coverPath ? (
                    <img src={resolveMediaUrl(f.coverPath) ?? ""} alt="" className="h-10 w-10 shrink-0 rounded object-cover" />
                  ) : (
                    <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded bg-surface-2 text-fg-subtle">
                      {active && isPlaying ? <Pause size={15} className="text-accent" /> : <Play size={15} />}
                    </span>
                  )}
                  <span className="min-w-0">
                    <span className={`block truncate text-sm ${active ? "font-medium text-accent" : "text-fg"}`}>{f.title}</span>
                    <span className="block truncate text-xs text-fg-muted">{f.artists.join(", ")} &middot; {f.albumTitle}</span>
                  </span>
                </button>
                <span className="hidden shrink-0 items-center gap-1 text-xs tabular-nums text-fg-subtle sm:inline-flex" title={`${f.playCount} plays`}>
                  <Headphones size={12} /> {formatPlays(f.playCount)}
                </span>
                <button onClick={() => unfavorite(f.trackId)} className="shrink-0 rounded-md p-1.5 text-accent transition-colors hover:bg-surface-3"
                  aria-label={`Unfavorite ${f.title}`} aria-pressed="true">
                  <Heart size={15} className="fill-current" />
                </button>
                <span className="shrink-0 text-xs tabular-nums text-fg-subtle">{formatDuration(f.duration)}</span>
              </div>
            );
          })}
        </div>
      )}
    </PageShell>
  );
}
