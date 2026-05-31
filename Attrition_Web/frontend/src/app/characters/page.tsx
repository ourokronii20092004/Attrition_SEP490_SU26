"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { Heart, Coins, MapPin, Clock, ChevronDown, Gamepad2 } from "lucide-react";
import { charactersApi } from "@/lib/api/characters";
import { useAuth } from "@/lib/providers";
import { PageShell } from "@/components/ui/page-shell";
import { PageTitle } from "@/components/ui/page-title";
import { Card } from "@/components/ui/card";
import { SkeletonList } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { SnapshotTimeline } from "@/components/snapshot-timeline";
import { RelativeTime } from "@/components/ui/relative-time";
import { qk } from "@/lib/query-keys";
import type { CharacterSummaryDto, SnapshotDto } from "@/lib/types";

export default function CharactersPage() {
  const { user, loading: authLoading } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (authLoading) return;
    if (!user) router.push("/login");
  }, [user, authLoading, router]);

  const { data: characters = [], isPending } = useQuery({
    queryKey: qk.characters.mine(),
    enabled: !!user && !authLoading,
    queryFn: async () => {
      const res = await charactersApi.getMine();
      return res.success ? res.data ?? [] : [];
    },
  });

  if (!user && !authLoading) return null;

  return (
    <PageShell size="lg">
      <PageTitle description="Your characters and their progression across runs.">Character Status</PageTitle>

      {authLoading || isPending ? (
        <SkeletonList rows={4} />
      ) : characters.length === 0 ? (
        <EmptyState
          icon={Gamepad2}
          title="No characters yet"
          description="Play a session in the game client and your characters will appear here."
        />
      ) : (
        <div className="stagger space-y-3">
          {characters.map((c, i) => (
            <div key={c.id} style={{ "--i": i } as React.CSSProperties}>
              <CharacterCard character={c} />
            </div>
          ))}
        </div>
      )}
    </PageShell>
  );
}

function CharacterCard({ character }: { character: CharacterSummaryDto }) {
  const [expanded, setExpanded] = useState(false);
  const snap = character.latestSnapshot;

  const { data: detail, isFetching: loadingDetail } = useQuery({
    queryKey: qk.characters.detail(character.id),
    enabled: expanded,
    queryFn: async () => {
      const res = await charactersApi.get(character.id);
      return res.success ? res.data : null;
    },
  });

  const toggle = () => setExpanded((v) => !v);

  return (
    <Card className="overflow-hidden p-0">
      <button onClick={toggle} className="flex w-full items-center gap-4 p-4 text-left transition-colors hover:bg-surface-2">
        <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl bg-accent-soft font-display text-lg font-bold text-accent">
          {character.name[0]?.toUpperCase() ?? "?"}
        </div>
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <h3 className="truncate font-medium text-fg">{character.name}</h3>
            <span className="rounded-full bg-surface-3 px-2 py-0.5 text-xs text-fg-muted">{character.archetype}</span>
            {snap && (
              <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${snap.isAlive ? "bg-success/10 text-success" : "bg-danger/10 text-danger"}`}>
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
        <ChevronDown size={18} className={`shrink-0 text-fg-subtle transition-transform duration-200 ${expanded ? "rotate-180" : ""}`} />
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
    </Card>
  );
}

function StatLine({ snap }: { snap: SnapshotDto }) {
  return (
    <div className="mt-1 flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-fg-muted">
      <span>Lv.{snap.level}</span>
      <span className="flex items-center gap-1"><Heart size={12} /> {snap.hp}/{snap.maxHp}</span>
      <span className="flex items-center gap-1"><Coins size={12} /> {snap.gold}</span>
      {snap.roomCode && <span className="flex items-center gap-1"><MapPin size={12} /> {snap.roomCode}</span>}
      <span className="flex items-center gap-1"><Clock size={12} /> <RelativeTime iso={snap.capturedAt} /></span>
    </div>
  );
}
