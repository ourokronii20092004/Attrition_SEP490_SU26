"use client";

import { Suspense, useState } from "react";
import { useRouter } from "next/navigation";
import { useQuery, keepPreviousData } from "@tanstack/react-query";
import { Heart, MapPin, Clock, Gamepad2 } from "lucide-react";
import { charactersApi } from "@/lib/api/characters";
import { useAuth } from "@/lib/providers";
import { PageLoader } from "@/components/ui/spinner";
import { AdminPageHeader, AdminFilterBar, AdminTable, AdminRow } from "@/components/admin/admin-table";
import { Pagination } from "@/components/ui/pagination";
import { useDebouncedValue } from "@/lib/hooks/use-debounced-value";
import { formatDate } from "@/lib/format-date";
import { qk } from "@/lib/query-keys";
import type { AdminCharacterDto } from "@/lib/types";
import { useUrlPage } from "@/lib/hooks/use-url-pagination";

const STATUSES = [
  { value: "all", label: "All statuses" },
  { value: "alive", label: "Alive" },
  { value: "dead", label: "Dead" },
];

function AdminCharactersList() {
  const { user } = useAuth();
  const router = useRouter();
  const [page, setPage] = useUrlPage();
  const [searchInput, setSearchInput] = useState("");
  const [archetype, setArchetype] = useState("all");
  const [status, setStatus] = useState("all");
  const search = useDebouncedValue(searchInput.trim().toLowerCase(), 250);

  const { data, isPending: loading } = useQuery({
    queryKey: qk.admin.characters(page),
    enabled: user?.role === "Admin",
    placeholderData: keepPreviousData,
    queryFn: async () => {
      const res = await charactersApi.getAll({ page, pageSize: 30 });
      return res.success ? res.data : null;
    },
  });

  const characters = data?.items ?? [];
  const totalPages = data?.totalPages ?? 1;
  const totalCount = data?.totalCount ?? 0;

  if (!user || user.role !== "Admin") return null;
  if (loading && !data) return <PageLoader />;

  // Archetypes present on the current page, for the dropdown.
  const archetypes = Array.from(new Set(characters.map((c) => c.archetype))).sort();

  // Filters apply to the current page (backend has no character search/filter yet).
  const filtered = characters.filter((c) => {
    if (archetype !== "all" && c.archetype !== archetype) return false;
    if (status === "alive" && !(c.latestSnapshot?.isAlive ?? false)) return false;
    if (status === "dead" && (c.latestSnapshot?.isAlive ?? true)) return false;
    if (search && !c.name.toLowerCase().includes(search) && !(c.ownerUsername ?? c.ownerId).toLowerCase().includes(search)) return false;
    return true;
  });

  return (
    <div>
      <AdminPageHeader title="Characters" />
      <p className="mt-1 text-sm text-fg-muted">All players&apos; characters across the game ({totalCount}).</p>
      <AdminFilterBar
        search={searchInput}
        onSearch={setSearchInput}
        searchPlaceholder="Search this page by character or owner…"
        filters={[
          { value: status, onChange: setStatus, ariaLabel: "Filter by status", options: STATUSES },
          {
            value: archetype, onChange: setArchetype, ariaLabel: "Filter by archetype",
            options: [{ value: "all", label: "All archetypes" }, ...archetypes.map((a) => ({ value: a, label: a }))],
          },
        ]}
      />

      <AdminTable
        columns={[
          { key: "name", label: "Character" },
          { key: "owner", label: "Owner" },
          { key: "status", label: "Status" },
          { key: "stats", label: "Snapshot" },
          { key: "updated", label: "Updated", align: "right" },
        ]}
        empty={filtered.length === 0}
      >
        {filtered.map((c) => (
          <AdminRow key={c.id} onClick={() => router.push(`/admin/characters/${c.id}`)}>
            <td className="px-3 py-2">
              <div className="flex items-center gap-2.5">
                <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-accent-soft text-accent">
                  <Gamepad2 size={15} />
                </span>
                <div className="min-w-0">
                  <p className="truncate font-medium text-fg">{c.name}</p>
                  <p className="truncate text-xs text-fg-subtle">{c.archetype}</p>
                </div>
              </div>
            </td>
            <td className="px-3 py-2 text-fg-muted">{c.ownerUsername ?? <span className="text-fg-subtle">{c.ownerId.slice(0, 8)}…</span>}</td>
            <td className="px-3 py-2">
              {c.latestSnapshot ? (
                <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${c.latestSnapshot.isAlive ? "bg-success/10 text-success" : "bg-danger/10 text-danger"}`}>
                  {c.latestSnapshot.isAlive ? "Alive" : "Dead"}
                </span>
              ) : (
                <span className="text-xs text-fg-subtle">No data</span>
              )}
            </td>
            <td className="px-3 py-2">
              {c.latestSnapshot ? (
                <div className="flex flex-wrap items-center gap-x-3 gap-y-0.5 text-xs text-fg-muted">
                  <span className="font-medium text-fg">Lv.{c.latestSnapshot.level}</span>
                  <span className="flex items-center gap-1"><Heart size={11} /> {c.latestSnapshot.hp}/{c.latestSnapshot.maxHp}</span>
                  {c.latestSnapshot.roomCode && <span className="flex items-center gap-1"><MapPin size={11} /> {c.latestSnapshot.roomCode}</span>}
                </div>
              ) : (
                <span className="text-xs text-fg-subtle">—</span>
              )}
            </td>
            <td className="px-3 py-2 text-right text-xs text-fg-subtle">
              <span className="inline-flex items-center gap-1"><Clock size={11} /> {formatDate(c.updatedAt)}</span>
            </td>
          </AdminRow>
        ))}
      </AdminTable>

      {totalPages > 1 && <Pagination page={page} totalPages={totalPages} onChange={setPage} compact />}
    </div>
  );
}

export default function AdminCharactersPage() {
  return (
    <Suspense fallback={null}>
      <AdminCharactersList />
    </Suspense>
  );
}
