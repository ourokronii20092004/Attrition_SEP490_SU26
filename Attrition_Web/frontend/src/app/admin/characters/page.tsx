"use client";

import { Suspense, useMemo, useState } from "react";
import Link from "next/link";
import { useQuery, keepPreviousData } from "@tanstack/react-query";
import { Heart, MapPin, Clock, User, ChevronRight, Skull, Shield, Swords, Sparkles, Wand2, Crosshair } from "lucide-react";
import { charactersApi } from "@/lib/api/characters";
import { useAuth } from "@/lib/providers";
import { PageLoader } from "@/components/ui/spinner";
import { AdminPageHeader, AdminFilterBar } from "@/components/admin/admin-table";
import { Card } from "@/components/ui/card";
import { Pagination } from "@/components/ui/pagination";
import { CopyButton } from "@/components/admin/copy-button";
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

/** Map the free-text archetype to a recognizable glyph; unknown values fall back to a generic one. */
function ArchetypeIcon({ archetype }: { archetype: string }) {
  const key = archetype.toLowerCase();
  let Icon = Swords;
  if (key.includes("shield") || key.includes("guard") || key.includes("tank")) Icon = Shield;
  else if (key.includes("mage") || key.includes("wizard") || key.includes("sorcer") || key.includes("wand")) Icon = Wand2;
  else if (key.includes("rogue") || key.includes("assassin") || key.includes("ranger") || key.includes("hunt") || key.includes("archer")) Icon = Crosshair;
  else if (key.includes("spark") || key.includes("wanderer") || key.includes("default")) Icon = Sparkles;
  return <Icon size={14} aria-hidden />;
}

interface OwnerGroup {
  ownerId: string;
  ownerUsername: string | null;
  characters: AdminCharacterDto[];
}

/**
 * Group a page of characters by the player who owns them.
 *
 * Owners are ordered by most recent activity rather than alphabetically: an admin opening this page
 * is usually looking at whoever just played, not scanning A-Z.
 */
function groupByOwner(characters: AdminCharacterDto[]): OwnerGroup[] {
  const map = new Map<string, OwnerGroup>();
  for (const c of characters) {
    const g = map.get(c.ownerId) ?? { ownerId: c.ownerId, ownerUsername: c.ownerUsername, characters: [] };
    g.characters.push(c);
    // Any row can supply the username; the first non-null wins.
    if (!g.ownerUsername && c.ownerUsername) g.ownerUsername = c.ownerUsername;
    map.set(c.ownerId, g);
  }
  const groups = [...map.values()];
  for (const g of groups) g.characters.sort((a, b) => b.updatedAt.localeCompare(a.updatedAt));
  return groups.sort((a, b) => b.characters[0].updatedAt.localeCompare(a.characters[0].updatedAt));
}

function AdminCharactersList() {
  const { user } = useAuth();
  const [page, setPage] = useUrlPage();
  const [searchInput, setSearchInput] = useState("");
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

  // Filters apply to the current page: the backend has no character search yet, so claiming to
  // search everything would be a lie.
  const groups = useMemo(() => {
    const filtered = characters.filter((c) => {
      if (status === "alive" && !(c.latestSnapshot?.isAlive ?? false)) return false;
      if (status === "dead" && (c.latestSnapshot?.isAlive ?? true)) return false;
      if (search) {
        const owner = (c.ownerUsername ?? c.ownerId).toLowerCase();
        if (!c.name.toLowerCase().includes(search) && !owner.includes(search)) return false;
      }
      return true;
    });
    return groupByOwner(filtered);
  }, [characters, status, search]);

  if (!user || user.role !== "Admin") return null;
  if (loading && !data) return <PageLoader />;

  return (
    <div>
      <AdminPageHeader title="Characters" />
      <p className="mt-1 text-sm text-fg-muted">
        Grouped by the player who owns them ({totalCount} character{totalCount === 1 ? "" : "s"}).
      </p>

      <AdminFilterBar
        search={searchInput}
        onSearch={setSearchInput}
        searchPlaceholder="Search this page by character or owner..."
        filters={[{ value: status, onChange: setStatus, ariaLabel: "Filter by status", options: STATUSES }]}
      />

      {groups.length === 0 ? (
        <Card className="mt-4 p-6 text-center text-sm text-fg-muted">No characters match that filter.</Card>
      ) : (
        <div className="mt-4 space-y-4">
          {groups.map((g) => (
            <Card key={g.ownerId} className="overflow-hidden p-0">
              <div className="flex items-center gap-2.5 border-b border-border bg-surface-2/50 px-4 py-2.5">
                <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-lg bg-accent-soft text-accent">
                  <User size={14} aria-hidden />
                </span>
                <div className="min-w-0 flex-1">
                  {g.ownerUsername ? (
                    <Link
                      href={`/admin/users/${g.ownerId}`}
                      className="truncate font-medium text-fg transition-colors hover:text-accent"
                    >
                      {g.ownerUsername}
                    </Link>
                  ) : (
                    <span className="truncate font-medium text-fg-muted">Unknown player</span>
                  )}
                </div>
                <span className="shrink-0 text-xs text-fg-subtle">
                  {g.characters.length} character{g.characters.length === 1 ? "" : "s"}
                </span>
                <CopyButton value={g.ownerId} label="Owner ID" />
              </div>

              <ul className="divide-y divide-border/60">
                {g.characters.map((c) => (
                  <li key={c.id}>
                    <Link
                      href={`/admin/characters/${c.id}`}
                      className="group flex items-center gap-3 px-4 py-2.5 transition-colors hover:bg-surface-2"
                    >
                      <span className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-lg ${c.latestSnapshot && !c.latestSnapshot.isAlive ? "bg-danger/10 text-danger" : "bg-accent-soft text-accent"}`}>
                        <ArchetypeIcon archetype={c.archetype} />
                      </span>

                      <div className="min-w-0 flex-1">
                        <div className="flex flex-wrap items-center gap-2">
                          <p className="truncate font-medium text-fg group-hover:text-accent">{c.name}</p>
                          <span className="rounded-full bg-surface-3 px-2 py-0.5 text-[11px] text-fg-muted">
                            {c.archetype}
                          </span>
                          {c.latestSnapshot && (
                            <span
                              className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[11px] font-medium ${
                                c.latestSnapshot.isAlive ? "bg-success/10 text-success" : "bg-danger/10 text-danger"
                              }`}
                            >
                              {c.latestSnapshot.isAlive ? "Alive" : <><Skull size={10} aria-hidden /> Dead</>}
                            </span>
                          )}
                        </div>

                        {c.latestSnapshot ? (
                          <div className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-0.5 text-xs text-fg-muted">
                            <span className="font-medium text-fg">Lv.{c.latestSnapshot.level}</span>
                            {/* HP bar — instant read on how close to death this character is */}
                            <span className="flex items-center gap-1.5">
                              <Heart size={11} aria-hidden />
                              <span className="inline-flex h-1.5 w-16 items-center overflow-hidden rounded-full bg-surface-3">
                                <span
                                  className={`h-full rounded-full ${c.latestSnapshot.isAlive ? "bg-success" : "bg-danger"}`}
                                  style={{ width: `${Math.max(0, Math.min(100, (c.latestSnapshot.hp / Math.max(1, c.latestSnapshot.maxHp)) * 100))}%` }}
                                />
                              </span>
                              <span className="tabular-nums text-fg-subtle">{c.latestSnapshot.hp}/{c.latestSnapshot.maxHp}</span>
                            </span>
                            {c.latestSnapshot.roomCode && (
                              <span className="flex items-center gap-1">
                                <MapPin size={11} aria-hidden /> {c.latestSnapshot.roomCode}
                              </span>
                            )}
                            <span className="flex items-center gap-1 text-fg-subtle">
                              <Clock size={11} aria-hidden /> {formatDate(c.updatedAt)}
                            </span>
                          </div>
                        ) : (
                          <p className="mt-0.5 text-xs text-fg-subtle">No save data reported yet</p>
                        )}
                      </div>

                      <ChevronRight size={16} className="shrink-0 text-fg-subtle group-hover:text-accent" aria-hidden />
                    </Link>
                  </li>
                ))}
              </ul>
            </Card>
          ))}
        </div>
      )}

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
