"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useAuth, useConfirm } from "@/lib/providers";
import { musicApi } from "@/lib/api/music";
import { resolveMediaUrl } from "@/lib/api/media";
import { Button } from "@/components/ui/button";
import { Modal } from "@/components/ui/modal";
import { PageLoader } from "@/components/ui/spinner";
import { AdminPageHeader, AdminFilterBar, AdminTable, AdminRow } from "@/components/admin/admin-table";
import { useDebouncedValue } from "@/lib/hooks/use-debounced-value";
import { qk } from "@/lib/query-keys";
import { AlbumForm } from "../album-form";

export default function AdminAlbumsPage() {
  const { user } = useAuth();
  const router = useRouter();
  const queryClient = useQueryClient();
  const confirm = useConfirm();
  const [showForm, setShowForm] = useState(false);
  const [formDirty, setFormDirty] = useState(false);
  const [searchInput, setSearchInput] = useState("");
  const search = useDebouncedValue(searchInput.trim().toLowerCase(), 200);

  const { data: albums = [], isPending } = useQuery({
    queryKey: qk.admin.music.albums(),
    enabled: user?.role === "Admin",
    queryFn: async () => {
      const res = await musicApi.getAlbums();
      return res.success ? res.data : [];
    },
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: qk.admin.music.albums() });

  const deleteMutation = useMutation({
    mutationFn: (id: number) => musicApi.deleteAlbum(id),
    onSuccess: invalidate,
  });

  const handleDelete = async (id: number) => {
    if (!(await confirm({ message: "Delete this album?", danger: true, confirmLabel: "Delete" }))) return;
    deleteMutation.mutate(id);
  };

  if (!user || user.role !== "Admin") return null;

  const filtered = search
    ? albums.filter((a) => a.title.toLowerCase().includes(search) || a.artists.join(" ").toLowerCase().includes(search))
    : albums;

  return (
    <div>
      <AdminPageHeader title="Albums" addLabel="New Album" onAdd={() => setShowForm(true)} />
      <AdminFilterBar search={searchInput} onSearch={setSearchInput} searchPlaceholder="Search albums or artists…" />

      <Modal open={showForm} onClose={() => setShowForm(false)} title="New Album" dirty={formDirty}>
        <AlbumForm
          onDirtyChange={setFormDirty}
          onDone={() => { setFormDirty(false); setShowForm(false); invalidate(); }}
          onCancel={() => { setFormDirty(false); setShowForm(false); }}
        />
      </Modal>

      {isPending ? (
        <PageLoader />
      ) : (
        <AdminTable
          columns={[
            { key: "album", label: "Album" },
            { key: "artists", label: "Artists" },
            { key: "tracks", label: "Tracks" },
            { key: "actions", label: "Actions", align: "right" },
          ]}
          empty={filtered.length === 0}
        >
          {filtered.map((album) => (
            <AdminRow key={album.albumId} onClick={() => router.push(`/admin/music/albums/${album.albumId}`)}>
              <td className="px-3 py-2">
                <div className="flex items-center gap-3">
                  {album.coverPath
                    ? <img src={resolveMediaUrl(album.coverPath) ?? ""} alt="" className="h-9 w-9 shrink-0 rounded object-cover" />
                    : <div className="h-9 w-9 shrink-0 rounded bg-surface-2" />}
                  <span className="font-medium text-fg">{album.title}</span>
                </div>
              </td>
              <td className="px-3 py-2 text-fg-muted">{album.artists.join(", ")}</td>
              <td className="px-3 py-2 text-fg-muted tabular-nums">{album.trackCount}</td>
              <td className="px-3 py-2 text-right">
                <Button size="sm" variant="danger" onClick={(e) => { e.stopPropagation(); handleDelete(album.albumId); }}>Delete</Button>
              </td>
            </AdminRow>
          ))}
        </AdminTable>
      )}
    </div>
  );
}
