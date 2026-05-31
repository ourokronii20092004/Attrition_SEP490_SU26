"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useAuth, useConfirm } from "@/lib/providers";
import { assetsApi } from "@/lib/api/assets";
import { resolveMediaUrl } from "@/lib/api/media";
import { Button } from "@/components/ui/button";
import { Modal } from "@/components/ui/modal";
import { PageLoader } from "@/components/ui/spinner";
import { qk } from "@/lib/query-keys";
import type { AssetDto } from "@/lib/types";
import { UploadForm } from "./_components/UploadForm";
import { EditForm } from "./_components/EditForm";

export default function AdminAssetsPage() {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const confirm = useConfirm();
  const [page, setPage] = useState(1);
  const [showUpload, setShowUpload] = useState(false);
  const [editing, setEditing] = useState<AssetDto | null>(null);

  const { data: assets, isPending: loading } = useQuery({
    queryKey: qk.admin.assets(page),
    enabled: user?.role === "Admin",
    queryFn: async () => {
      const res = await assetsApi.adminList({ page, pageSize: 20 });
      return res.success ? res.data : null;
    },
  });

  const totalPages = assets ? Math.ceil(assets.totalCount / assets.pageSize) : 0;

  const invalidate = () => queryClient.invalidateQueries({ queryKey: qk.admin.assets() });

  const deleteMutation = useMutation({
    mutationFn: async (id: string) => { await assetsApi.delete(id); },
    onSuccess: invalidate,
  });

  const handleDelete = async (id: string) => {
    if (!(await confirm({ message: "Delete this asset?", danger: true, confirmLabel: "Delete" }))) return;
    deleteMutation.mutate(id);
  };

  if (!user || user.role !== "Admin") return null;

  return (
    <div className="mx-auto max-w-6xl">
      <div className="flex items-center justify-between">
        <h1 className="font-display text-3xl font-bold text-fg">Assets Management</h1>
        <Button onClick={() => setShowUpload(true)}>Upload Asset</Button>
      </div>

      <Modal open={showUpload} onClose={() => setShowUpload(false)} title="Upload Asset">
        <UploadForm onDone={() => { setShowUpload(false); invalidate(); }} onCancel={() => setShowUpload(false)} />
      </Modal>
      <Modal open={editing != null} onClose={() => setEditing(null)} title="Edit Asset">
        {editing && <EditForm asset={editing} onDone={() => { setEditing(null); invalidate(); }} onCancel={() => setEditing(null)} />}
      </Modal>

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
