"use client";

import { Suspense, useMemo, useState } from "react";
import Link from "next/link";
import { useQuery, keepPreviousData } from "@tanstack/react-query";
import { DoorOpen, Users, MapPin, Clock, Trophy, ChevronRight } from "lucide-react";
import { charactersApi } from "@/lib/api/characters";
import { useAuth } from "@/lib/providers";
import { PageLoader } from "@/components/ui/spinner";
import { AdminPageHeader, AdminFilterBar } from "@/components/admin/admin-table";
import { Card } from "@/components/ui/card";
import { Pagination } from "@/components/ui/pagination";
import { RelativeTime } from "@/components/ui/relative-time";
import { useDebouncedValue } from "@/lib/hooks/use-debounced-value";
import { formatPlaytime } from "@/lib/format-duration";
import { qk } from "@/lib/query-keys";
import { useUrlPage } from "@/lib/hooks/use-url-pagination";

const PARTY = [
  { value: "all", label: "All rooms" },
  { value: "coop", label: "Co-op only" },
  { value: "solo", label: "Single player" },
];

function AdminRoomsList() {
  const { user } = useAuth();
  const [page, setPage] = useUrlPage();
  const [searchInput, setSearchInput] = useState("");
  const [party, setParty] = useState("all");
  const search = useDebouncedValue(searchInput.trim().toLowerCase(), 250);

  const { data, isPending: loading } = useQuery({
    queryKey: qk.rooms.adminList(page),
    enabled: user?.role === "Admin",
    placeholderData: keepPreviousData,
    queryFn: async () => {
      const res = await charactersApi.getAdminRooms({ page, pageSize: 20 });
      return res.success ? res.data : null;
    },
  });

  const rooms = data?.items ?? [];
  const totalCount = data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / (data?.pageSize ?? 20)));

  // Filters apply to the visible page — the backend has no room search, and pretending otherwise
  // would misrepresent what was searched.
  const filtered = useMemo(() => rooms.filter((r) => {
    if (party === "coop" && r.playerCount < 2) return false;
    if (party === "solo" && r.playerCount > 1) return false;
    if (search) {
      const hay = `${r.roomCode} ${r.name} ${r.ownerUsername ?? ""} ${r.currentScene ?? ""}`.toLowerCase();
      if (!hay.includes(search)) return false;
    }
    return true;
  }), [rooms, party, search]);

  if (!user || user.role !== "Admin") return null;
  if (loading && !data) return <PageLoader />;

  return (
    <div>
      <AdminPageHeader title="Co-op Rooms" />
      <p className="mt-1 text-sm text-fg-muted">
        Who played with whom, where, and how far the shared world got ({totalCount} room{totalCount === 1 ? "" : "s"}).
      </p>

      <AdminFilterBar
        search={searchInput}
        onSearch={setSearchInput}
        searchPlaceholder="Search this page by code, name, host or scene..."
        filters={[{ value: party, onChange: setParty, ariaLabel: "Filter by party size", options: PARTY }]}
      />

      {filtered.length === 0 ? (
        <Card className="mt-4 p-6 text-center text-sm text-fg-muted">No rooms match that filter.</Card>
      ) : (
        <div className="mt-4 space-y-2">
          {filtered.map((r) => (
            <Link
              key={r.id}
              href={`/admin/rooms/${r.id}`}
              className="group block rounded-card focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent"
            >
              <Card className="flex items-center gap-3 p-4 transition-colors group-hover:bg-surface-2">
                <span className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-lg ${r.playerCount > 1 ? "bg-accent-soft text-accent" : "bg-surface-3 text-fg-muted"}`}>
                  <DoorOpen size={17} aria-hidden />
                </span>

                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="font-mono text-sm font-semibold text-fg group-hover:text-accent">
                      {r.roomCode}
                    </span>
                    <span className="truncate text-sm text-fg-muted">{r.name}</span>
                    {r.playerCount > 1 ? (
                      <span className="inline-flex items-center gap-1 rounded-full bg-info/10 px-2 py-0.5 text-[11px] font-medium text-info">
                        <Users size={10} aria-hidden /> {r.playerCount} players
                      </span>
                    ) : (
                      <span className="rounded-full bg-surface-3 px-2 py-0.5 text-[11px] text-fg-muted">Solo</span>
                    )}
                  </div>

                  <div className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-0.5 text-xs text-fg-muted">
                    <span>
                      Host: <span className="text-fg">{r.ownerUsername ?? "unknown"}</span>
                    </span>
                    {r.currentScene && (
                      <span className="flex items-center gap-1">
                        <MapPin size={11} aria-hidden /> {r.currentScene}
                      </span>
                    )}
                    {r.worldStateCount > 0 && (
                      <span className="flex items-center gap-1">
                        <Trophy size={11} aria-hidden /> {r.worldStateCount} world flag{r.worldStateCount === 1 ? "" : "s"}
                      </span>
                    )}
                    {r.playTimeSeconds > 0 && <span>{formatPlaytime(r.playTimeSeconds)}</span>}
                    <span className="flex items-center gap-1 text-fg-subtle">
                      <Clock size={11} aria-hidden /> <RelativeTime iso={r.lastPlayedAt} />
                    </span>
                  </div>
                </div>

                <ChevronRight size={16} className="shrink-0 text-fg-subtle group-hover:text-accent" aria-hidden />
              </Card>
            </Link>
          ))}
        </div>
      )}

      {totalPages > 1 && <Pagination page={page} totalPages={totalPages} onChange={setPage} compact />}
    </div>
  );
}

export default function AdminRoomsPage() {
  return (
    <Suspense fallback={null}>
      <AdminRoomsList />
    </Suspense>
  );
}
