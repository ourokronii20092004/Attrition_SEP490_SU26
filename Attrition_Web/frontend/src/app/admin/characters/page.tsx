"use client";

import { useEffect, useState } from "react";
import { Heart, Coins, MapPin, ChevronDown, ChevronRight } from "lucide-react";
import { charactersApi } from "@/lib/api/characters";
import { useAuth } from "@/lib/providers";
import { PageLoader } from "@/components/ui/spinner";
import { SnapshotTimeline } from "@/components/snapshot-timeline";
import { formatDateTime } from "@/lib/format-date";
import type { AdminCharacterDto, CharacterDetailDto, SnapshotDto } from "@/lib/types";

export default function AdminCharactersPage() {
  const { user } = useAuth();
  const [characters, setCharacters] = useState<AdminCharacterDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");

  useEffect(() => {
    if (user?.role !== "Admin") return;
    charactersApi
      .getAll()
      .then((res) => {
        if (res.success) setCharacters(res.data);
      })
      .finally(() => setLoading(false));
  }, [user]);

  if (!user || user.role !== "Admin") return null;
  if (loading) return <PageLoader />;

  const filtered = search.trim()
    ? characters.filter((c) =>
        c.name.toLowerCase().includes(search.toLowerCase()) ||
        (c.ownerUsername ?? c.ownerId).toLowerCase().includes(search.toLowerCase()))
    : characters;

  return (
    <div className="mx-auto max-w-5xl">
      <h1 className="font-display text-3xl font-bold text-fg">Character Status</h1>
      <p className="mt-2 text-fg-muted">All players&apos; characters across the game ({characters.length})</p>

      <input
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        placeholder="Search by character or owner..."
        className="mt-6 w-full max-w-sm rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-fg outline-none placeholder:text-fg-subtle focus:border-accent"
      />

      <div className="mt-4 space-y-2">
        {filtered.map((c) => (
          <AdminCharacterRow key={c.id} character={c} />
        ))}
        {filtered.length === 0 && <p className="py-8 text-center text-fg-muted">No characters found.</p>}
      </div>
    </div>
  );
}

function AdminCharacterRow({ character }: { character: AdminCharacterDto }) {
  const [expanded, setExpanded] = useState(false);
  const [detail, setDetail] = useState<CharacterDetailDto | null>(null);
  const [loadingDetail, setLoadingDetail] = useState(false);
  const snap = character.latestSnapshot;

  const toggle = async () => {
    const next = !expanded;
    setExpanded(next);
    if (next && !detail) {
      setLoadingDetail(true);
      try {
        const res = await charactersApi.getAdmin(character.id);
        if (res.success) setDetail(res.data);
      } finally {
        setLoadingDetail(false);
      }
    }
  };

  return (
    <div className="card overflow-hidden">
      <button onClick={toggle} className="flex w-full items-center gap-4 p-4 text-left transition hover:bg-surface-2">
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2">
            <h3 className="truncate font-medium text-fg">{character.name}</h3>
            <span className="rounded bg-surface-3 px-2 py-0.5 text-xs text-fg-muted">{character.archetype}</span>
            {snap && (
              <span className={`rounded px-2 py-0.5 text-xs ${snap.isAlive ? "bg-success/10 text-success" : "bg-danger/10 text-danger"}`}>
                {snap.isAlive ? "Alive" : "Dead"}
              </span>
            )}
          </div>
          <p className="mt-0.5 text-xs text-fg-subtle">owner: {character.ownerUsername ?? character.ownerId}</p>
          {snap && <AdminStatLine snap={snap} />}
        </div>
        {expanded ? <ChevronDown size={18} className="text-fg-subtle" /> : <ChevronRight size={18} className="text-fg-subtle" />}
      </button>

      {expanded && (
        <div className="border-t border-border bg-surface-2/40 px-4 py-3">
          <h4 className="mb-2 text-xs font-semibold uppercase tracking-wider text-fg-subtle">History</h4>
          {loadingDetail ? (
            <p className="py-4 text-center text-sm text-fg-muted">Loading...</p>
          ) : (
            <SnapshotTimeline snapshots={detail?.snapshots ?? []} />
          )}
        </div>
      )}
    </div>
  );
}

function AdminStatLine({ snap }: { snap: SnapshotDto }) {
  return (
    <div className="mt-1 flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-fg-muted">
      <span>Lv.{snap.level}</span>
      <span className="flex items-center gap-1"><Heart size={12} /> {snap.hp}/{snap.maxHp}</span>
      <span className="flex items-center gap-1"><Coins size={12} /> {snap.gold}</span>
      {snap.roomCode && <span className="flex items-center gap-1"><MapPin size={12} /> {snap.roomCode}</span>}
      <span className="text-fg-subtle">{formatDateTime(snap.capturedAt)}</span>
    </div>
  );
}
