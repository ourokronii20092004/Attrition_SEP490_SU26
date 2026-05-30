"use client";

import { useState } from "react";
import { musicApi } from "@/lib/api/music";
import { resolveMediaUrl } from "@/lib/api/media";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { Toggle } from "@/components/ui/toggle";
import type { MusicAlbumDto, ScanTrackResponse } from "@/lib/types";

interface Props {
  albums: MusicAlbumDto[];
  onDone: () => void;
  onCancel: () => void;
}

/**
 * Two-step admin track upload: (1) pick a file → we scan its ID3 tags; (2) confirm/edit the
 * detected metadata, then upload. If the file has an album tag, the backend auto-creates or
 * matches the album; the admin can still override by picking an existing album.
 */
export function TrackUploadFlow({ albums, onDone, onCancel }: Props) {
  const [step, setStep] = useState<"pick" | "confirm">("pick");
  const [file, setFile] = useState<File | null>(null);
  const [scanning, setScanning] = useState(false);
  const [scan, setScan] = useState<ScanTrackResponse | null>(null);
  const [error, setError] = useState("");

  // Editable confirm-step fields, seeded from the scan.
  const [title, setTitle] = useState("");
  const [artists, setArtists] = useState("");
  const [genre, setGenre] = useState("");
  const [trackNumber, setTrackNumber] = useState(1);
  const [albumMode, setAlbumMode] = useState<"auto" | "existing">("auto");
  const [albumId, setAlbumId] = useState<number>(albums[0]?.albumId ?? 0);
  const [isFeatured, setIsFeatured] = useState(false);
  const [uploading, setUploading] = useState(false);

  const handleScan = async (f: File) => {
    setFile(f);
    setScanning(true);
    setError("");
    try {
      const res = await musicApi.scanTrack(f);
      if (res.success && res.data) {
        const d = res.data;
        setScan(d);
        setTitle(d.title);
        setArtists(d.artists.join(", "));
        setGenre(d.genre ?? "");
        setTrackNumber(d.trackNumber || 1);
        // No album tag and albums exist → default to picking one; otherwise let the backend auto-create.
        setAlbumMode(d.album ? "auto" : albums.length ? "existing" : "auto");
        setStep("confirm");
      } else {
        setError(res.error || "Could not scan this file.");
      }
    } catch {
      setError("Could not scan this file.");
    } finally {
      setScanning(false);
    }
  };

  const handleUpload = async () => {
    if (!file) return;
    setUploading(true);
    setError("");
    try {
      const form = new FormData();
      // Reuse the already-scanned temp file so we don't upload bytes twice.
      if (scan?.tempFileKey) form.append("tempFileKey", scan.tempFileKey);
      else form.append("file", file);
      form.append("title", title);
      artists.split(",").map((a) => a.trim()).filter(Boolean).forEach((a) => form.append("artists", a));
      if (genre) form.append("genre", genre);
      form.append("trackNumber", String(trackNumber));
      form.append("isFeatured", String(isFeatured));
      if (albumMode === "existing" && albumId) form.append("albumId", String(albumId));
      if (scan?.tempCoverPath) form.append("tempCoverPath", scan.tempCoverPath);

      const res = await musicApi.uploadTrack(form);
      if (res.success) onDone();
      else setError(res.error || "Upload failed.");
    } catch {
      setError("Upload failed.");
    } finally {
      setUploading(false);
    }
  };

  if (step === "pick") {
    return (
      <div className="card mt-4 space-y-3 p-4">
        <label className="block text-sm font-medium text-fg-muted">Audio file</label>
        <input
          type="file"
          accept="audio/*"
          onChange={(e) => { const f = e.target.files?.[0]; if (f) handleScan(f); }}
          className="text-sm text-fg"
        />
        <p className="text-xs text-fg-subtle">We&apos;ll read the embedded tags so you can confirm before uploading.</p>
        {scanning && <p className="text-sm text-accent">Scanning metadata…</p>}
        {error && <p className="text-sm text-danger">{error}</p>}
        <Button type="button" variant="secondary" onClick={onCancel}>Cancel</Button>
      </div>
    );
  }

  return (
    <div className="card mt-4 space-y-3 p-4">
      <div className="flex items-center gap-3">
        {scan?.tempCoverPath
          ? <img src={resolveMediaUrl(scan.tempCoverPath) ?? ""} alt="" className="h-16 w-16 rounded-lg object-cover" />
          : <div className="flex h-16 w-16 items-center justify-center rounded-lg bg-surface-2 text-xs text-fg-subtle">No art</div>}
        <div className="text-sm text-fg-muted">
          <p>Detected from file{scan?.album ? `: album “${scan.album}”` : " — no album tag"}.</p>
          <p className="text-xs text-fg-subtle">Adjust anything below, then upload.</p>
        </div>
      </div>

      <Input label="Title" value={title} onChange={(e) => setTitle(e.target.value)} />
      <Input label="Artists (comma-separated)" value={artists} onChange={(e) => setArtists(e.target.value)} />
      <div className="grid grid-cols-2 gap-3">
        <Input label="Genre" value={genre} onChange={(e) => setGenre(e.target.value)} />
        <Input label="Track number" type="number" value={trackNumber} onChange={(e) => setTrackNumber(+e.target.value)} />
      </div>

      <Select label="Album" value={albumMode} onChange={(e) => setAlbumMode(e.target.value as "auto" | "existing")}>
        <option value="auto">{scan?.album ? `Auto (from tag: ${scan.album})` : "Auto-create from tag"}</option>
        {albums.length > 0 && <option value="existing">Pick an existing album</option>}
      </Select>
      {albumMode === "existing" && (
        <Select label="Existing album" value={albumId} onChange={(e) => setAlbumId(+e.target.value)}>
          {albums.map((a) => <option key={a.albumId} value={a.albumId}>{a.title}</option>)}
        </Select>
      )}

      <Toggle checked={isFeatured} onChange={setIsFeatured} label="Feature this track" />

      {error && <p className="text-sm text-danger">{error}</p>}
      <div className="flex gap-2">
        <Button onClick={handleUpload} loading={uploading} disabled={!title.trim()}>Upload</Button>
        <Button variant="secondary" onClick={() => setStep("pick")}>Back</Button>
        <Button variant="secondary" onClick={onCancel}>Cancel</Button>
      </div>
    </div>
  );
}
