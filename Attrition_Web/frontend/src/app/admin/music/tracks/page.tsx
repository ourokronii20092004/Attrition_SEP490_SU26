"use client";

import { Suspense, useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useAuth, useConfirm } from "@/lib/providers";
import { musicApi } from "@/lib/api/music";
import { Button } from "@/components/ui/button";
import { Modal } from "@/components/ui/modal";
import { PageLoader } from "@/components/ui/spinner";
import { AdminPageHeader, AdminFilterBar, AdminTable, AdminRow } from "@/components/admin/admin-table";
import { Pagination } from "@/components/ui/pagination";
import { useDebouncedValue } from "@/lib/hooks/use-debounced-value";
import { qk } from "@/lib/query-keys";
import { useUrlPagination } from "@/lib/hooks/use-url-pagination";
import { TrackUploadFlow } from "../track-upload-flow";

const fmtDuration = (s: number) => {
  const m = Math.floor(s / 60);
  const sec = Math.floor(s % 60);
  return `${m}:${sec.toString().padStart(2, "0")}`;
};

function AdminTracksList() {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const confirm = useConfirm();
  const [showUpload, setShowUpload] = useState(false);
  const [searchInput, setSearchInput] = useState("");
  const [albumFilter, setAlbumFilter] = useState("all");
  const [featuredFilter, setFeaturedFilter] = useState("all");
  const search = useDebouncedValue(searchInput.trim().toLowerCase(), 200);

  const { data: albums = [] } = useQuery({
    queryKey: qk.admin.music.albums(),
    enabled: user?.role === "Admin",
    queryFn: async () => {
      const res = await musicApi.getAlbums();
      return res.success ? res.data : [];
    },
  });

  const { data: tracks = [], isPending } = useQuery({
    queryKey: qk.admin.music.tracks(),
    enabled: user?.role === "Admin",
    queryFn: async () => {
      const res = await musicApi.getTracks();
      return res.success ? res.data : [];
    },
  });

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: qk.admin.music.tracks() });
    queryClient.invalidateQueries({ queryKey: qk.admin.music.albums() });
  };

  const deleteMutation = useMutation({
    mutationFn: (id: number) => musicApi.deleteTrack(id),
    onSuccess: invalidate,
  });

  const handleDelete = async (id: number) => {
    if (!(await confirm({ message: "Delete this track?", danger: true, confirmLabel: "Delete" }))) return;
    deleteMutation.mutate(id);
  };

  const filtered = tracks.filter((t) => {
    if (search && !t.title.toLowerCase().includes(search) && !t.artists.join(" ").toLowerCase().includes(search)) return false;
    if (albumFilter !== "all" && String(t.albumId) !== albumFilter) return false;
    if (featuredFilter === "featured" && !t.isFeatured) return false;
    if (featuredFilter === "normal" && t.isFeatured) return false;
    return true;
  });
  const { page, setPage, totalPages, paged } = useUrlPagination(filtered, 20);

  if (!user || user.role !== "Admin") return null;

  return (
    <div>
      <AdminPageHeader title="Tracks" addLabel="Upload Track" onAdd={() => setShowUpload(true)} />
      <AdminFilterBar
        search={searchInput}
        onSearch={setSearchInput}
        searchPlaceholder="Search tracks or artists…"
        filters={[
          {
            value: albumFilter, onChange: setAlbumFilter, ariaLabel: "Filter by album",
            options: [{ value: "all", label: "All albums" }, ...albums.map((a) => ({ value: String(a.albumId), label: a.title }))],
          },
          {
            value: featuredFilter, onChange: setFeaturedFilter, ariaLabel: "Filter by featured",
            options: [{ value: "all", label: "All tracks" }, { value: "featured", label: "Featured only" }, { value: "normal", label: "Not featured" }],
          },
        ]}
      />

      <Modal open={showUpload} onClose={() => setShowUpload(false)} title="Upload Track" size="lg">
        <TrackUploadFlow albums={albums} onDone={() => { setShowUpload(false); invalidate(); }} onCancel={() => setShowUpload(false)} />
      </Modal>

      {isPending ? (
        <PageLoader />
      ) : (
        <AdminTable
          columns={[
            { key: "title", label: "Title" },
            { key: "album", label: "Album" },
            { key: "num", label: "#" },
            { key: "plays", label: "Plays" },
            { key: "dur", label: "Length" },
            { key: "actions", label: "Actions", align: "right" },
          ]}
          empty={filtered.length === 0}
        >
          {paged.map((t) => (
            <AdminRow key={t.trackId}>
              <td className="px-3 py-2">
                <span className="font-medium text-fg">{t.title}</span>
                {t.isFeatured && <span className="ml-2 rounded bg-accent-soft px-1.5 py-0.5 text-[10px] font-medium text-accent">Featured</span>}
              </td>
              <td className="px-3 py-2 text-fg-muted">{t.albumTitle ?? "—"}</td>
              <td className="px-3 py-2 text-fg-muted tabular-nums">{t.trackNumber}</td>
              <td className="px-3 py-2 text-fg-muted tabular-nums">{t.playCount}</td>
              <td className="px-3 py-2 text-fg-muted tabular-nums">{fmtDuration(t.duration)}</td>
              <td className="px-3 py-2 text-right">
                <Button size="sm" variant="danger" onClick={() => handleDelete(t.trackId)}>Delete</Button>
              </td>
            </AdminRow>
          ))}
        </AdminTable>
      )}
      <Pagination page={page} totalPages={totalPages} onChange={setPage} compact />
    </div>
  );
}

export default function AdminTracksPage() {
  return (
    <Suspense fallback={null}>
      <AdminTracksList />
    </Suspense>
  );
}
