"use client";

import { useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { musicApi } from "@/lib/api/music";
import { parseApiError } from "@/lib/api/parse-error";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Toggle } from "@/components/ui/toggle";
import type { MusicTrackDto } from "@/lib/types";

/**
 * Edit a track's metadata (title, artists, track number, genre, featured).
 * Audio replacement is intentionally out of scope — the file is immutable once uploaded.
 */
export function TrackEditForm({ track, onDone, onCancel }: {
  track: MusicTrackDto; onDone: () => void; onCancel: () => void;
}) {
  const [title, setTitle] = useState(track.title);
  const [artists, setArtists] = useState(track.artists.join(", "));
  const [trackNumber, setTrackNumber] = useState(String(track.trackNumber));
  const [genre, setGenre] = useState(track.genre ?? "");
  const [isFeatured, setIsFeatured] = useState(track.isFeatured);
  const [error, setError] = useState<string | null>(null);

  const mutation = useMutation({
    mutationFn: async () => {
      await musicApi.updateTrack(track.trackId, {
        title,
        artists: artists.split(",").map((s) => s.trim()).filter(Boolean),
        trackNumber: Number(trackNumber),
        genre: genre || undefined,
        isFeatured,
      });
    },
    onSuccess: onDone,
    onError: (err) => setError(parseApiError(err, "Failed to update the track.")),
  });

  return (
    <div className="space-y-3">
      {error && <p className="rounded-md bg-danger/10 px-3 py-2 text-sm text-danger">{error}</p>}
      <Input label="Title" value={title} onChange={(e) => setTitle(e.target.value)} />
      <Input label="Artists (comma-separated)" value={artists} onChange={(e) => setArtists(e.target.value)} />
      <div className="grid grid-cols-2 gap-3">
        <Input label="Track number" type="number" min={1} value={trackNumber} onChange={(e) => setTrackNumber(e.target.value)} />
        <Input label="Genre" value={genre} onChange={(e) => setGenre(e.target.value)} />
      </div>
      <Toggle checked={isFeatured} onChange={setIsFeatured} label="Featured" description="Featured tracks are highlighted in the music library." />
      <div className="flex gap-2">
        <Button onClick={() => mutation.mutate()} loading={mutation.isPending} disabled={!title.trim()}>Save Changes</Button>
        <Button variant="secondary" onClick={onCancel}>Cancel</Button>
      </div>
    </div>
  );
}
