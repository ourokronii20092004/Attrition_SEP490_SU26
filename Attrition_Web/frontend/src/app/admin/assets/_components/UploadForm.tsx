"use client";

import { useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { assetsApi } from "@/lib/api/assets";
import { parseApiError } from "@/lib/api/parse-error";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";

// Asset types and the extensions the backend accepts for each.
export const ASSET_TYPES = ["image", "document", "lore"] as const;
const IMAGE_EXTS = ["jpg", "jpeg", "png", "gif", "webp"];
const DOC_EXTS = ["pdf", "doc", "docx", "txt", "md"];

function extOf(name: string) {
  const i = name.lastIndexOf(".");
  return i >= 0 ? name.slice(i + 1).toLowerCase() : "";
}
/** Guess the best asset type from a file's extension. */
function detectType(name: string): string {
  const ext = extOf(name);
  if (IMAGE_EXTS.includes(ext)) return "image";
  if (DOC_EXTS.includes(ext)) return "document";
  return "image";
}
/** True when the chosen type can legitimately hold this file's extension. */
function typeAllowsExt(type: string, ext: string): boolean {
  if (type === "image") return IMAGE_EXTS.includes(ext);
  return IMAGE_EXTS.includes(ext) || DOC_EXTS.includes(ext); // document/lore accept both
}

export function UploadForm({ onDone, onCancel }: { onDone: () => void; onCancel: () => void }) {
  const [file, setFile] = useState<File | null>(null);
  const [title, setTitle] = useState("");
  const [assetType, setAssetType] = useState("image");
  const [typeTouched, setTypeTouched] = useState(false);
  const [description, setDescription] = useState("");
  const [tags, setTags] = useState("");
  const [error, setError] = useState("");

  const ext = file ? extOf(file.name) : "";
  const mismatch = !!file && !typeAllowsExt(assetType, ext);

  const onPickFile = (f: File | null) => {
    setFile(f);
    setError("");
    // Auto-choose the asset type from the file unless the admin already overrode it.
    if (f && !typeTouched) setAssetType(detectType(f.name));
  };

  const uploadMutation = useMutation({
    mutationFn: async (f: File) => assetsApi.create(f, {
      assetType,
      title: title || undefined,
      description: description || undefined,
      tags: tags || undefined,
    }),
    onSuccess: (res) => {
      if (res.success) onDone();
      else setError(res.error || "Upload failed.");
    },
    onError: (err) => {
      setError(parseApiError(err, "Upload failed."));
    },
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!file) return;
    if (mismatch) {
      setError(`A "${assetType}" asset can't be a .${ext} file. Pick a matching type or file.`);
      return;
    }
    setError("");
    uploadMutation.mutate(file);
  };

  return (
    <form onSubmit={handleSubmit} className="mt-4 card p-4 space-y-3">
      <div className="space-y-1">
        <label className="block text-sm font-medium text-fg-muted">File</label>
        <input type="file" onChange={(e) => onPickFile(e.target.files?.[0] ?? null)} className="text-sm text-fg" />
        {file && <p className="text-xs text-fg-subtle">Detected: .{ext || "?"} → suggested type &ldquo;{detectType(file.name)}&rdquo;</p>}
      </div>
      <Input label="Title" value={title} onChange={(e) => setTitle(e.target.value)} />
      <Select label="Asset Type" value={assetType} onChange={(e) => { setAssetType(e.target.value); setTypeTouched(true); }}>
        {ASSET_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
      </Select>
      {mismatch && (
        <p className="rounded-md border border-warning/30 bg-warning/10 px-3 py-2 text-xs text-warning">
          Warning: a .{ext} file doesn&apos;t fit type &ldquo;{assetType}&rdquo;. Images: {IMAGE_EXTS.join(", ")}. Documents: {DOC_EXTS.join(", ")}.
        </p>
      )}
      <Input label="Description" value={description} onChange={(e) => setDescription(e.target.value)} />
      <Input label="Tags (comma-separated)" value={tags} onChange={(e) => setTags(e.target.value)} />
      {error && <p className="text-sm text-danger">{error}</p>}
      <div className="flex gap-2">
        <Button type="submit" loading={uploadMutation.isPending} disabled={!file || mismatch}>Upload</Button>
        <Button type="button" variant="secondary" onClick={onCancel}>Cancel</Button>
      </div>
    </form>
  );
}
