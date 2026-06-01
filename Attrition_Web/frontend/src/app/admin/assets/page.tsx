"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient, keepPreviousData } from "@tanstack/react-query";
import { useAuth, useConfirm } from "@/lib/providers";
import { assetsApi } from "@/lib/api/assets";
import { resolveMediaUrl } from "@/lib/api/media";
import { Button } from "@/components/ui/button";
import { Modal } from "@/components/ui/modal";
import { PageLoader } from "@/components/ui/spinner";
import { AdminPageHeader, AdminFilterBar, AdminTable, AdminRow } from "@/components/admin/admin-table";
import { useDebouncedValue } from "@/lib/hooks/use-debounced-value";
import { qk } from "@/lib/query-keys";
import type { AssetDto } from "@/lib/types";
import { UploadForm } from "./_components/UploadForm";
import { EditForm } from "./_components/EditForm";

const ASSET_TYPES = ["image", "document", "lore", "concept", "sprite"];

export default function AdminAssetsPage() {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const confirm = useConfirm();
  const [page, setPage] = useState(1);
  const [showUpload, setShowUpload] = useState(false);
  const [editing, setEditing] = useState<AssetDto | null>(null);
  const [searchInput, setSearchInput] = useState("");
  const [typeFilter, setTypeFilter] = useState("all");
  const search = useDebouncedValue(searchInput.trim(), 300);

  const { data: assets, isPending: loading } = useQuery({
    queryKey: qk.admin.assets({ page, search, typeFilter }),
    enabled: user?.role === "Admin",
    placeholderData: keepPreviousData,
    queryFn: async () => {
      const res = await assetsApi.adminList({
        page, pageSize: 20,
        assetType: typeFilter === "all" ? undefined : typeFilter,
        search: search || undefined,
      });
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
    <div>
      <AdminPageHeader title="Assets" addLabel="Upload Asset" onAdd={() => setShowUpload(true)} />
      <AdminFilterBar
        search={searchInput}
        onSearch={(v) => { setSearchInput(v); setPage(1); }}
        searchPlaceholder="Search by file name…"
        filters={[
          {
            value: typeFilter, onChange: (v) => { setTypeFilter(v); setPage(1); }, ariaLabel: "Filter by type",
            options: [{ value: "all", label: "All types" }, ...ASSET_TYPES.map((t) => ({ value: t, label: t }))],
          },
        ]}
      />

      <Modal open={showUpload} onClose={() => setShowUpload(false)} title="Upload Asset">
        <UploadForm onDone={() => { setShowUpload(false); invalidate(); }} onCancel={() => setShowUpload(false)} />
      </Modal>
      <Modal open={editing != null} onClose={() => setEditing(null)} title="Edit Asset">
        {editing && <EditForm asset={editing} onDone={() => { setEditing(null); invalidate(); }} onCancel={() => setEditing(null)} />}
      </Modal>

      {loading && !assets ? (
        <PageLoader />
      ) : (
        <AdminTable
          columns={[
            { key: "asset", label: "Asset" },
            { key: "type", label: "Type" },
            { key: "uploader", label: "Uploaded by" },
            { key: "actions", label: "Actions", align: "right" },
          ]}
          empty={!assets || assets.items.length === 0}
        >
          {assets?.items.map((asset) => (
            <AdminRow key={asset.id} onClick={() => setEditing(asset)}>
              <td className="px-3 py-2">
                <div className="flex items-center gap-3">
                  <img src={resolveMediaUrl(asset.filePath) ?? ""} alt="" className="h-9 w-9 shrink-0 rounded object-cover" />
                  <span className="truncate font-medium text-fg">{asset.title ?? asset.fileName}</span>
                </div>
              </td>
              <td className="px-3 py-2 text-fg-muted">{asset.assetType}</td>
              <td className="px-3 py-2 text-fg-muted">{asset.uploadedBy ?? "—"}</td>
              <td className="px-3 py-2 text-right">
                <div className="flex justify-end gap-2">
                  <Button size="sm" variant="secondary" onClick={(e) => { e.stopPropagation(); setEditing(asset); }}>Edit</Button>
                  <Button size="sm" variant="danger" onClick={(e) => { e.stopPropagation(); handleDelete(asset.id); }}>Delete</Button>
                </div>
              </td>
            </AdminRow>
          ))}
        </AdminTable>
      )}

      {totalPages > 1 && (
        <div className="mt-4 flex items-center justify-center gap-2">
          <button onClick={() => setPage((p) => Math.max(1, p - 1))} disabled={page <= 1} className="rounded-md border border-border px-3 py-1 text-sm text-fg-muted disabled:opacity-50">Prev</button>
          <span className="text-sm text-fg-muted">{page} / {totalPages}</span>
          <button onClick={() => setPage((p) => Math.min(totalPages, p + 1))} disabled={page >= totalPages} className="rounded-md border border-border px-3 py-1 text-sm text-fg-muted disabled:opacity-50">Next</button>
        </div>
      )}
    </div>
  );
}
