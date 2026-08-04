"use client";

import { useEffect, useMemo } from "react";
import { useParams, useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import {
  Backpack,
  Clock,
  Crown,
  Flag,
  Heart,
  Map,
  Shield,
  ShieldHalf,
  Skull,
  Sparkles,
  Swords,
  Users,
  Zap,
  Eye,
  ScrollText,
} from "lucide-react";
import { sessionsApi } from "@/lib/api/sessions";
import { useAuth } from "@/lib/providers";
import { PageShell } from "@/components/ui/page-shell";
import { PageTitle } from "@/components/ui/page-title";
import { BackButton } from "@/components/ui/back-button";
import { Card } from "@/components/ui/card";
import { SkeletonList } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { RelativeTime } from "@/components/ui/relative-time";
import { InventoryView } from "@/components/inventory-view";
import { qk } from "@/lib/query-keys";
import { useLoginHref } from "@/lib/hooks/use-login-href";
import { formatPlaytime } from "@/lib/format-duration";
import {
  splitWorldStates,
  parseFog,
  parseAllocated,
  unspentPoints,
  STAT_LABELS,
} from "@/lib/world-state";
import type { CharacterSessionDto } from "@/lib/types";

export default function RoomDetailPage() {
  const params = useParams<{ id: string }>();
  const loginHref = useLoginHref();
  const { user, loading: authLoading } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (authLoading) return;
    if (!user) router.push(loginHref);
  }, [user, authLoading, router, loginHref]);

  const { data: room, isPending } = useQuery({
    queryKey: qk.sessions.detail(params.id),
    enabled: !!params.id && !!user && !authLoading,
    queryFn: async () => {
      const res = await sessionsApi.get(params.id);
      return res.success ? res.data : null;
    },
  });

  const progress = useMemo(() => splitWorldStates(room?.worldStates), [room]);
  const fog = useMemo(() => parseFog(room?.fogJson), [room]);
  const fogTotal = useMemo(() => [...fog.values()].reduce((a, b) => a + b, 0), [fog]);

  if (!user && !authLoading) return null;

  const back = <div className="mb-5"><BackButton href="/rooms" label="Back to rooms" /></div>;

  if (authLoading || isPending) {
    return (
      <PageShell size="lg">
        {back}
        <SkeletonList rows={3} />
      </PageShell>
    );
  }

  if (!room) {
    return (
      <PageShell size="lg">
        {back}
        <EmptyState
          icon={Map}
          title="Room not found"
          description="This room may have been deleted, or it belongs to another host."
        />
      </PageShell>
    );
  }

  // Host first, then joining clients; ties broken by name so the order is stable between loads.
  const characters = [...room.characters].sort(
    (a, b) => a.playerRole - b.playerRole || (a.name ?? "").localeCompare(b.name ?? ""),
  );

  return (
    <PageShell size="lg">
      {back}
      <PageTitle
        eyebrow={`Room ${room.roomCode}`}
        description={
          <span className="flex flex-wrap items-center gap-x-4 gap-y-1">
            {room.currentScene && (
              <span className="flex items-center gap-1"><Map size={13} /> {room.currentScene}</span>
            )}
            <span className="flex items-center gap-1"><Users size={13} /> {room.characters.length} character{room.characters.length === 1 ? "" : "s"}</span>
            <span className="flex items-center gap-1"><Clock size={13} /> {formatPlaytime(room.playTimeSeconds)}</span>
            <span>Last played <RelativeTime iso={room.lastPlayedAt} /></span>
          </span>
        }
      >
        {room.name}
      </PageTitle>

      <div className="space-y-8">
        <section>
          <h2 className="mb-3 font-display text-xl font-semibold text-fg">Party</h2>
          {characters.length === 0 ? (
            <EmptyState
              icon={Users}
              title="No progress saved yet"
              description="Character data appears after the party rests or quits in this room."
            />
          ) : (
            <div className="space-y-4">
              {characters.map((c) => (
                <CharacterPanel key={c.characterId} character={c} />
              ))}
            </div>
          )}
        </section>

        <section>
          <h2 className="mb-3 font-display text-xl font-semibold text-fg">World progress</h2>
          <div className="grid gap-4 sm:grid-cols-2">
            <ListCard
              icon={Skull}
              title="Bosses defeated"
              items={progress.bosses}
              empty="No bosses defeated yet."
            />
            <ListCard
              icon={Flag}
              title="Rest points discovered"
              items={progress.checkpoints}
              empty="No rest points discovered yet."
            />
            <Card className="p-4">
              <h3 className="mb-2 flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-fg-subtle">
                <Eye size={12} /> Map revealed
              </h3>
              {fogTotal === 0 ? (
                <p className="text-sm text-fg-subtle">Nothing explored yet.</p>
              ) : (
                <>
                  <p className="mb-2 text-sm text-fg-muted">{fogTotal} cells across {fog.size} map{fog.size === 1 ? "" : "s"}</p>
                  <ul className="space-y-1">
                    {[...fog.entries()].sort((a, b) => b[1] - a[1]).map(([scene, count]) => (
                      <li key={scene} className="flex items-center justify-between gap-3 text-sm">
                        <span className="truncate text-fg">{scene}</span>
                        <span className="shrink-0 tabular-nums text-fg-muted">{count}</span>
                      </li>
                    ))}
                  </ul>
                </>
              )}
            </Card>
            <Card className="p-4">
              <h3 className="mb-2 flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-fg-subtle">
                <ScrollText size={12} /> Quests
              </h3>
              {progress.quests.length === 0 ? (
                <p className="text-sm text-fg-subtle">No quest progress recorded.</p>
              ) : (
                <ul className="space-y-1">
                  {progress.quests.map((q) => (
                    <li key={q.id} className="flex items-center justify-between gap-3 text-sm">
                      <span className="truncate text-fg">{q.id}</span>
                      <span className="shrink-0 text-xs text-fg-muted">
                        state {q.state}{q.progress > 0 && ` · ${q.progress}`}
                      </span>
                    </li>
                  ))}
                </ul>
              )}
            </Card>
          </div>
        </section>
      </div>
    </PageShell>
  );
}

function ListCard({
  icon: Icon,
  title,
  items,
  empty,
}: {
  icon: React.ComponentType<{ size?: number; className?: string }>;
  title: string;
  items: string[];
  empty: string;
}) {
  return (
    <Card className="p-4">
      <h3 className="mb-2 flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-fg-subtle">
        <Icon size={12} /> {title}
        {items.length > 0 && <span className="text-fg-muted">({items.length})</span>}
      </h3>
      {items.length === 0 ? (
        <p className="text-sm text-fg-subtle">{empty}</p>
      ) : (
        <div className="flex flex-wrap gap-1.5">
          {items.map((id) => (
            <span key={id} className="rounded-md border border-border bg-surface-2 px-2 py-1 text-xs text-fg">
              {id}
            </span>
          ))}
        </div>
      )}
    </Card>
  );
}

function CharacterPanel({ character: c }: { character: CharacterSessionDto }) {
  const allocated = parseAllocated(c.allocatedPointsJson);
  const unspent = unspentPoints(c.currentLevel, allocated);
  const isHost = c.playerRole === 0;
  const hasCombatStats = c.ad > 0 || c.ap > 0 || c.def > 0 || c.res > 0;

  return (
    <Card className="overflow-hidden p-0">
      <div className="flex flex-wrap items-center gap-3 border-b border-border p-4">
        <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-accent-soft font-display text-lg font-bold text-accent">
          {(c.name ?? "?")[0]?.toUpperCase() ?? "?"}
        </div>
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            {/* Name comes from the characters table; fall back to the id so a deleted row is still identifiable. */}
            <h3 className="truncate font-medium text-fg">{c.name ?? c.characterId}</h3>
            {c.archetype && (
              <span className="rounded-full bg-surface-3 px-2 py-0.5 text-xs text-fg-muted">{c.archetype}</span>
            )}
            <span className={`flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium ${isHost ? "bg-accent-soft text-accent" : "bg-surface-3 text-fg-muted"}`}>
              {isHost && <Crown size={11} />} {isHost ? "Host" : "Client"}
            </span>
          </div>
          <div className="mt-1 flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-fg-muted">
            <span>Lv.{c.currentLevel}</span>
            <span>{c.currentExp} exp</span>
            <span className="flex items-center gap-1"><Skull size={12} /> {c.deathCount} death{c.deathCount === 1 ? "" : "s"}</span>
            <span>Saved <RelativeTime iso={c.updatedAt} /></span>
          </div>
        </div>
      </div>

      <div className="space-y-4 p-4">
        <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
          <Vital icon={Heart} label="HP" value={`${c.currentHp}/${c.maxHp}`} />
          <Vital icon={Sparkles} label="Mana" value={`${c.currentMana}/${c.maxMana}`} />
          <Vital icon={Zap} label="Stamina" value={String(c.maxStamina)} />
          <Vital icon={Swords} label="Atk speed" value={c.attackSpeed.toFixed(2)} />
        </div>

        {/* Final stats (base + points + gear), computed in-game. All-zero means the save predates
            this field, in which case showing "0 AD" would be a lie — hide the row instead. */}
        {hasCombatStats && (
          <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
            <Vital icon={Swords} label="AD" value={String(c.ad)} />
            <Vital icon={Sparkles} label="AP" value={String(c.ap)} />
            <Vital icon={Shield} label="DEF" value={String(c.def)} />
            <Vital icon={ShieldHalf} label="RES" value={String(c.res)} />
          </div>
        )}

        <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
          <Vital icon={Heart} label="Health flasks" value={`${c.healthCharges}/${c.potionMaxFlasks}`} />
          <Vital icon={Sparkles} label="Mana flasks" value={`${c.manaCharges}/${c.potionMaxManaFlasks}`} />
          {c.lastRestPointId && <Vital icon={Flag} label="Last rest" value={c.lastRestPointId} />}
          <Vital
            icon={Map}
            label="Position"
            value={`${c.posX.toFixed(1)}, ${c.posY.toFixed(1)}`}
          />
        </div>

        <div>
          <h4 className="mb-2 text-xs font-semibold uppercase tracking-wider text-fg-subtle">
            Allocated points
            {/* Derived, never stored: 5 per level after the first, minus what's spent. */}
            {unspent > 0 && <span className="ml-1.5 text-accent">{unspent} unspent</span>}
          </h4>
          {allocated.length === 0 || allocated.every((n) => n === 0) ? (
            <p className="text-sm text-fg-subtle">No points allocated.</p>
          ) : (
            <div className="flex flex-wrap gap-1.5">
              {STAT_LABELS.map((label, i) =>
                (allocated[i] ?? 0) > 0 ? (
                  <span key={label} className="rounded-md border border-border bg-surface-2 px-2 py-1 text-xs text-fg">
                    {label} <span className="tabular-nums text-accent">+{allocated[i]}</span>
                  </span>
                ) : null,
              )}
            </div>
          )}
        </div>

        <div>
          <h4 className="mb-2 flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-fg-subtle">
            <Backpack size={12} /> Inventory
          </h4>
          <InventoryView json={c.inventoryJson} />
        </div>
      </div>
    </Card>
  );
}

function Vital({
  icon: Icon,
  label,
  value,
}: {
  icon: React.ComponentType<{ size?: number; className?: string }>;
  label: string;
  value: string;
}) {
  return (
    <div className="rounded-lg border border-border bg-surface-2 px-3 py-2">
      <p className="flex items-center gap-1 text-[10px] uppercase tracking-wider text-fg-subtle">
        <Icon size={11} /> {label}
      </p>
      <p className="mt-0.5 truncate text-sm tabular-nums text-fg">{value}</p>
    </div>
  );
}
