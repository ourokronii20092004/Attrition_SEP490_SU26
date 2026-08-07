"use client";

import { Suspense, useState } from "react";
import { useQuery, useMutation, useQueryClient, keepPreviousData } from "@tanstack/react-query";
import { useAuth, useConfirm } from "@/lib/providers";
import { assetsApi } from "@/lib/api/assets";
import { resolveMediaUrl } from "@/lib/api/media";
import { Button } from "@/components/ui/button";
import { Modal } from "@/components/ui/modal";
import {
  AdminPageHeader, AdminFilterBar, AdminTable, AdminRow, AdminSelectCell, AdminBulkBar,
} from "@/components/admin/admin-table";
import { Pagination } from "@/components/ui/pagination";
import { useDebouncedValue } from "@/lib/hooks/use-debounced-value";
import { qk } from "@/lib/query-keys";
import type { AssetDto } from "@/lib/types";
import { useUrlPage } from "@/lib/hooks/use-url-pagination";
import { UploadForm } from "./_components/UploadForm";
import { EditForm } from "./_components/EditForm";

const ASSET_TYPES = ["image", "document", "lore", "concept", "sprite"];

function AdminAssetsView() {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const confirm = useConfirm();
  const [page, setPage] = useUrlPage();
  const [showUpload, setShowUpload] = useState(false);
  const [editing, setEditing] = useState<AssetDto | null>(null);
  const [searchInput, setSearchInput] = useState("");
  const [typeFilter, setTypeFilter] = useState("all");
  const [selected, setSelected] = useState<Set<string>>(new Set());
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

  const handleBulkDelete = async () => {
    const ids = [...selected];
    if (ids.length === 0) return;
    const ok = await confirm({
      title: `Delete ${ids.length} asset${ids.length === 1 ? "" : "s"}?`,
      message: "This permanently removes the selected assets. It can't be undone.",
      danger: true,
      confirmLabel: `Delete ${ids.length}`,
    });
    if (!ok) return;
    await Promise.allSettled(ids.map((id) => assetsApi.delete(id)));
    setSelected(new Set());
    invalidate();
  };

  if (!user || user.role !== "Admin") return null;

  const rows = assets?.items ?? [];
  const selection = {
    selected,
    onChange: setSelected,
    pageIds: rows.map((a) => a.id),
  };

  return (
    <div>
      <AdminPageHeader title="Assets" addLabel="Upload Asset" onAdd={() => setShowUpload(true)}>
        <AdminBulkBar count={selected.size} onClear={() => setSelected(new Set())}>
          <Button size="sm" variant="danger" onClick={handleBulkDelete}>Delete</Button>
        </AdminBulkBar>
      </AdminPageHeader>
      <AdminFilterBar
        search={searchInput}
        onSearch={(v) => setSearchInput(v)}
        searchPlaceholder="Search by file name…"
        filters={[
          {
            value: typeFilter, onChange: (v) => setTypeFilter(v), ariaLabel: "Filter by type",
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

      <AdminTable
        columns={[
          { key: "asset", label: "Asset" },
          { key: "type", label: "Type" },
          { key: "uploader", label: "Uploaded by" },
          { key: "actions", label: "Actions", align: "right" },
        ]}
        selection={selection}
        loading={loading && !assets}
        empty={rows.length === 0}
        emptyLabel={search || typeFilter !== "all" ? "No assets match these filters." : "No assets yet."}
        emptyHint={search || typeFilter !== "all" ? "Try a different search or type." : "Use Upload Asset to add the first one."}
      >
        {rows.map((asset) => (
          <AdminRow key={asset.id} onClick={() => setEditing(asset)} selected={selected.has(asset.id)}>
            <AdminSelectCell id={asset.id} selection={selection} />
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

      {totalPages > 1 && (
        <Pagination page={page} totalPages={totalPages} onChange={setPage} compact />
      )}
    </div>
  );
}

export default function AdminAssetsPage() {
  return (
    <Suspense fallback={null}>
      <AdminAssetsView />
    </Suspense>
  );
}
