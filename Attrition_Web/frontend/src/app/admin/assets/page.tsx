"use client";

import { useEffect, useState } from "react";
import { useAuth } from "@/lib/providers";
import { assetsApi } from "@/lib/api/assets";
import { resolveMediaUrl } from "@/lib/api/media";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { PageLoader } from "@/components/ui/spinner";
import type { AssetDto, PaginatedResponse, UpdateAssetReq } from "@/lib/types";

export default function AdminAssetsPage() {
  const { user } = useAuth();
  const [assets, setAssets] = useState<PaginatedResponse<AssetDto> | null>(null);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [showUpload, setShowUpload] = useState(false);
  const [editing, setEditing] = useState<AssetDto | null>(null);
  const totalPages = assets ? Math.ceil(assets.totalCount / assets.pageSize) : 0;

  const fetchAssets = () => {
    setLoading(true);
    assetsApi.adminList({ page, pageSize: 20 }).then((res) => {
      if (res.success) setAssets(res.data);
      setLoading(false);
    });
  };

  useEffect(() => {
    if (user?.role !== "Admin") return;
    fetchAssets();
  }, [user, page]);

  const handleDelete = async (id: string) => {
    if (!confirm("Delete this asset?")) return;
    await assetsApi.delete(id);
    fetchAssets();
  };

  if (!user || user.role !== "Admin") return null;

  return (
    <div className="mx-auto max-w-6xl">
      <div className="flex items-center justify-between">
        <h1 className="font-display text-3xl font-bold text-fg">Assets Management</h1>
        <Button onClick={() => setShowUpload(true)}>Upload Asset</Button>
      </div>

      {showUpload && <UploadForm onDone={() => { setShowUpload(false); fetchAssets(); }} onCancel={() => setShowUpload(false)} />}
      {editing && <EditForm asset={editing} onDone={() => { setEditing(null); fetchAssets(); }} onCancel={() => setEditing(null)} />}

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
  const [description, setDescription] = useState("");
  const [tags, setTags] = useState("");
  const [uploading, setUploading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!file) return;
    setUploading(true);
    try {
      await assetsApi.create(file, {
        assetType,
        title: title || undefined,
        description: description || undefined,
        tags: tags || undefined,
      });
      onDone();
    } catch {}
    setUploading(false);
  };

  return (
    <form onSubmit={handleSubmit} className="mt-4 card p-4 space-y-3">
      <div className="space-y-1">
        <label className="block text-sm font-medium text-fg-muted">File</label>
        <input type="file" onChange={(e) => setFile(e.target.files?.[0] ?? null)} className="text-sm text-fg" />
      </div>
      <Input label="Title" value={title} onChange={(e) => setTitle(e.target.value)} />
      <Input label="Asset Type" value={assetType} onChange={(e) => setAssetType(e.target.value)} />
      <Input label="Description" value={description} onChange={(e) => setDescription(e.target.value)} />
      <Input label="Tags (comma-separated)" value={tags} onChange={(e) => setTags(e.target.value)} />
      <div className="flex gap-2">
        <Button type="submit" loading={uploading} disabled={!file}>Upload</Button>
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
  const [saving, setSaving] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    try {
      const data: UpdateAssetReq = {
        title: title || undefined,
        description: description || undefined,
        assetType: assetType || undefined,
        tags: tags || undefined,
      };
      await assetsApi.update(asset.id, data);
      onDone();
    } catch {}
    setSaving(false);
  };

  return (
    <form onSubmit={handleSubmit} className="mt-4 card p-4 space-y-3">
      <h3 className="text-sm font-medium text-fg">Edit: {asset.fileName}</h3>
      <Input label="Title" value={title} onChange={(e) => setTitle(e.target.value)} />
      <Input label="Asset Type" value={assetType} onChange={(e) => setAssetType(e.target.value)} />
      <Input label="Description" value={description} onChange={(e) => setDescription(e.target.value)} />
      <Input label="Tags (comma-separated)" value={tags} onChange={(e) => setTags(e.target.value)} />
      <div className="flex gap-2">
        <Button type="submit" loading={saving}>Save</Button>
        <Button type="button" variant="secondary" onClick={onCancel}>Cancel</Button>
      </div>
    </form>
  );
}
