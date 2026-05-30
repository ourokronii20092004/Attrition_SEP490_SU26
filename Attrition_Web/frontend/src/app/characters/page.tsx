"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Heart, Coins, MapPin, Clock, ChevronDown, ChevronRight } from "lucide-react";
import { charactersApi } from "@/lib/api/characters";
import { useAuth } from "@/lib/providers";
import { PageLoader } from "@/components/ui/spinner";
import { SnapshotTimeline } from "@/components/snapshot-timeline";
import type { CharacterSummaryDto, CharacterDetailDto, SnapshotDto } from "@/lib/types";

export default function CharactersPage() {
  const { user, loading: authLoading } = useAuth();
  const router = useRouter();
  const [characters, setCharacters] = useState<CharacterSummaryDto[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (authLoading) return;
    if (!user) {
      router.push("/login");
      return;
    }
    charactersApi
      .getMine()
      .then((res) => {
        if (res.success) setCharacters(res.data);
      })
      .finally(() => setLoading(false));
  }, [user, authLoading, router]);

  if (authLoading || loading) return <PageLoader />;
  if (!user) return null;

  return (
    <div className="mx-auto max-w-4xl px-4 py-8">
      <h1 className="font-display text-4xl font-bold text-fg">Character Status</h1>
      <p className="mt-2 text-fg-muted">Your characters and their progression across runs</p>

      {characters.length === 0 ? (
        <div className="mt-10 rounded-lg border border-dashed border-border py-16 text-center">
          <p className="text-fg-muted">No characters yet.</p>
          <p className="mt-1 text-sm text-fg-subtle">Play a session in the game client to see your characters here.</p>
        </div>
      ) : (
        <div className="mt-6 space-y-3">
          {characters.map((c) => (
            <CharacterCard key={c.id} character={c} />
          ))}
        </div>
      )}
    </div>
  );
}

function CharacterCard({ character }: { character: CharacterSummaryDto }) {
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
        const res = await charactersApi.get(character.id);
        if (res.success) setDetail(res.data);
      } finally {
        setLoadingDetail(false);
      }
    }
  };

  return (
    <div className="card overflow-hidden">
      <button onClick={toggle} className="flex w-full items-center gap-4 p-4 text-left transition hover:bg-surface-2">
        <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-lg bg-accent-soft font-display text-lg font-bold text-accent">
          {character.name[0]?.toUpperCase() ?? "?"}
        </div>
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
          {snap ? (
            <StatLine snap={snap} />
          ) : (
            <p className="mt-1 text-xs text-fg-subtle">No status reported yet</p>
          )}
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

function StatLine({ snap }: { snap: SnapshotDto }) {
  return (
    <div className="mt-1 flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-fg-muted">
      <span>Lv.{snap.level}</span>
      <span className="flex items-center gap-1"><Heart size={12} /> {snap.hp}/{snap.maxHp}</span>
      <span className="flex items-center gap-1"><Coins size={12} /> {snap.gold}</span>
      {snap.roomCode && <span className="flex items-center gap-1"><MapPin size={12} /> {snap.roomCode}</span>}
      <span className="flex items-center gap-1"><Clock size={12} /> {formatRelative(snap.capturedAt)}</span>
    </div>
  );
}

function formatRelative(iso: string): string {
  const diff = Date.now() - new Date(iso).getTime();
  const mins = Math.floor(diff / 60000);
  if (mins < 1) return "just now";
  if (mins < 60) return `${mins}m ago`;
  const hrs = Math.floor(mins / 60);
  if (hrs < 24) return `${hrs}h ago`;
  return `${Math.floor(hrs / 24)}d ago`;
}
