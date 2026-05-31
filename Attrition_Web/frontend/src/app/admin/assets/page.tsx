"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@/lib/providers";
import { assetsApi } from "@/lib/api/assets";
import { resolveMediaUrl } from "@/lib/api/media";
import { parseApiError } from "@/lib/api/parse-error";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { PageLoader } from "@/components/ui/spinner";
import type { AssetDto, UpdateAssetReq } from "@/lib/types";

// Asset types and the extensions the backend accepts for each.
const ASSET_TYPES = ["image", "document", "lore"] as const;
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

export default function AdminAssetsPage() {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [showUpload, setShowUpload] = useState(false);
  const [editing, setEditing] = useState<AssetDto | null>(null);

  const { data: assets, isPending: loading } = useQuery({
    queryKey: ["admin", "assets", page],
    enabled: user?.role === "Admin",
    queryFn: async () => {
      const res = await assetsApi.adminList({ page, pageSize: 20 });
      return res.success ? res.data : null;
    },
  });

  const totalPages = assets ? Math.ceil(assets.totalCount / assets.pageSize) : 0;

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ["admin", "assets"] });

  const deleteMutation = useMutation({
    mutationFn: async (id: string) => { await assetsApi.delete(id); },
    onSuccess: invalidate,
  });

  const handleDelete = (id: string) => {
    if (!confirm("Delete this asset?")) return;
    deleteMutation.mutate(id);
  };

  if (!user || user.role !== "Admin") return null;

  return (
    <div className="mx-auto max-w-6xl">
      <div className="flex items-center justify-between">
        <h1 className="font-display text-3xl font-bold text-fg">Assets Management</h1>
        <Button onClick={() => setShowUpload(true)}>Upload Asset</Button>
      </div>

      {showUpload && <UploadForm onDone={() => { setShowUpload(false); invalidate(); }} onCancel={() => setShowUpload(false)} />}
      {editing && <EditForm asset={editing} onDone={() => { setEditing(null); invalidate(); }} onCancel={() => setEditing(null)} />}

      {loading ? (
        <PageLoader />
      ) : (
        <div className="mt-6 grid gap-4 grid-cols-2 sm:grid-cols-3 lg:grid-cols-4">
          {assets?.items.map((asset) => (
            <div key={asset.id} className="card overflow-hidden">
              <img src={resolveMediaUrl(asset.filePath) ?? ""} alt={asset.title ?? asset.fileName} className="aspect-square w-full object-cover" />
              <div className="p-3">
                <p className="truncate text-sm font-medium text-fg">{asset.title ?? asset.fileName}</p>
                <p className="text-xs text-fg-muted">{asset.assetType}</p>
                <div className="mt-2 flex gap-1">
                  <Button size="sm" variant="secondary" onClick={() => setEditing(asset)}>Edit</Button>
                  <Button size="sm" variant="danger" onClick={() => handleDelete(asset.id)}>Delete</Button>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      {totalPages > 1 && (
        <div className="mt-6 flex items-center justify-center gap-2">
          <button onClick={() => setPage((p) => Math.max(1, p - 1))} disabled={page <= 1} className="rounded-md border border-border px-3 py-1 text-sm text-fg-muted disabled:opacity-50">Prev</button>
          <span className="text-sm text-fg-muted">{page} / {totalPages}</span>
          <button onClick={() => setPage((p) => Math.min(totalPages, p + 1))} disabled={page >= totalPages} className="rounded-md border border-border px-3 py-1 text-sm text-fg-muted disabled:opacity-50">Next</button>
        </div>
      )}
    </div>
  );
}

function UploadForm({ onDone, onCancel }: { onDone: () => void; onCancel: () => void }) {
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

function EditForm({ asset, onDone, onCancel }: { asset: AssetDto; onDone: () => void; onCancel: () => void }) {
  const [title, setTitle] = useState(asset.title ?? "");
  const [description, setDescription] = useState(asset.description ?? "");
  const [assetType, setAssetType] = useState(asset.assetType);
  const [tags, setTags] = useState(asset.tags ?? "");
  const [error, setError] = useState<string | null>(null);

  const updateMutation = useMutation({
    mutationFn: async () => {
      const data: UpdateAssetReq = {
        title: title || undefined,
        description: description || undefined,
        assetType: assetType || undefined,
        tags: tags || undefined,
      };
      await assetsApi.update(asset.id, data);
    },
    onSuccess: () => {
      onDone();
    },
    onError: (err) => {
      setError(err instanceof Error ? err.message : "Failed to save changes. Please try again.");
    },
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    updateMutation.mutate();
  };

  return (
    <form onSubmit={handleSubmit} className="mt-4 card p-4 space-y-3">
      <h3 className="text-sm font-medium text-fg">Edit: {asset.fileName}</h3>
      {error && <p className="rounded-md bg-danger/10 px-3 py-2 text-sm text-danger">{error}</p>}
      <Input label="Title" value={title} onChange={(e) => setTitle(e.target.value)} />
      <Select label="Asset Type" value={assetType} onChange={(e) => setAssetType(e.target.value)}>
        {ASSET_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
      </Select>
      <Input label="Description" value={description} onChange={(e) => setDescription(e.target.value)} />
      <Input label="Tags (comma-separated)" value={tags} onChange={(e) => setTags(e.target.value)} />
      <div className="flex gap-2">
        <Button type="submit" loading={updateMutation.isPending}>Save</Button>
        <Button type="button" variant="secondary" onClick={onCancel}>Cancel</Button>
      </div>
    </form>
  );
}
