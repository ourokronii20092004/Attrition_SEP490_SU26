"use client";

import { Suspense, useMemo } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { DoorOpen, Users, MapPin, Trophy, Flag, Sparkles, Skull, Crown, Clock, History } from "lucide-react";
import { charactersApi } from "@/lib/api/characters";
import { useAuth } from "@/lib/providers";
import { PageLoader } from "@/components/ui/spinner";
import { EmptyState } from "@/components/ui/empty-state";
import { Card } from "@/components/ui/card";
import { BackButton } from "@/components/ui/back-button";
import { RelativeTime } from "@/components/ui/relative-time";
import { CopyButton } from "@/components/admin/copy-button";
import { formatPlaytime } from "@/lib/format-duration";
import { formatDateTime } from "@/lib/format-date";
import { splitWorldStates } from "@/lib/world-state";
import { qk } from "@/lib/query-keys";
import { useAdminPageLabel } from "@/lib/hooks/use-admin-page-label";

function AdminRoomDetail() {
  const params = useParams<{ id: string }>();
  const { user } = useAuth();

  const { data: room, isPending } = useQuery({
    queryKey: qk.rooms.adminDetail(params.id),
    enabled: user?.role === "Admin" && !!params.id,
    queryFn: async () => {
      const res = await charactersApi.getAdminRoom(params.id);
      return res.success ? res.data : null;
    },
  });

  useAdminPageLabel(room?.roomCode);

  // Reuses the shared parser rather than re-deriving the eventId prefixes, so the room page and the
  // game agree on what "q:" and "cp:" mean.
  const progress = useMemo(() => splitWorldStates(room?.worldStates), [room?.worldStates]);

  if (!user || user.role !== "Admin") return null;
  if (isPending) return <PageLoader />;

  if (!room) {
    return (
      <div>
        <div className="mb-5"><BackButton fallbackHref="/admin/rooms" label="Back to rooms" /></div>
        <EmptyState icon={DoorOpen} title="Room not found" description="It may have been deleted." />
      </div>
    );
  }

  const host = room.party.find((p) => p.playerRole === 0);
  const joiners = room.party.filter((p) => p.playerRole !== 0);

  return (
    <div>
      <div className="mb-5"><BackButton fallbackHref="/admin/rooms" label="Back to rooms" /></div>

      {/* ── Identity ── */}
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="flex items-center gap-3">
          <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl bg-accent-soft text-accent">
            <DoorOpen size={20} aria-hidden />
          </span>
          <div className="min-w-0">
            <h1 className="font-mono text-2xl font-bold tracking-tight text-fg">{room.roomCode}</h1>
            <p className="text-sm text-fg-muted">{room.name}</p>
          </div>
        </div>
        <div className="flex items-center gap-1 text-xs text-fg-subtle">
          <span>Room ID</span>
          <CopyButton value={room.id} label="Room ID" />
        </div>
      </div>

      <dl className="mt-5 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <Fact icon={Users} label="Party" value={`${room.party.length} character${room.party.length === 1 ? "" : "s"}`} />
        <Fact icon={MapPin} label="Current scene" value={room.currentScene ?? "Unknown"} />
        <Fact icon={Clock} label="Playtime" value={room.playTimeSeconds > 0 ? formatPlaytime(room.playTimeSeconds) : "Not recorded"} />
        <Fact icon={Trophy} label="World flags" value={String(room.worldStates.length)} />
      </dl>

      {/* ── Party: who played with whom ── */}
      <section className="mt-8">
        <h2 className="flex items-center gap-2 font-display text-lg font-semibold text-fg">
          <Users size={17} aria-hidden /> Party
        </h2>
        <p className="mt-1 text-xs text-fg-subtle">
          The host owns the room; joiners bring their own characters into it.
        </p>

        {room.party.length === 0 ? (
          <Card className="mt-3 p-4 text-sm text-fg-muted">No characters have saved in this room yet.</Card>
        ) : (
          <Card className="mt-3 divide-y divide-border p-0">
            {[...(host ? [host] : []), ...joiners].map((member) => (
              <Link
                key={member.characterId}
                href={`/admin/characters/${member.characterId}`}
                className="group flex items-center gap-3 px-4 py-3 transition-colors hover:bg-surface-2"
              >
                <span className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-lg ${
                  member.playerRole === 0 ? "bg-accent-soft text-accent" : "bg-surface-3 text-fg-muted"
                }`}>
                  {member.playerRole === 0 ? <Crown size={15} aria-hidden /> : <Users size={15} aria-hidden />}
                </span>

                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="truncate font-medium text-fg group-hover:text-accent">{member.characterName}</span>
                    <span className={`rounded-full px-2 py-0.5 text-[11px] font-medium ${
                      member.playerRole === 0 ? "bg-accent/10 text-accent" : "bg-surface-3 text-fg-muted"
                    }`}>
                      {member.playerRole === 0 ? "Host" : "Joined"}
                    </span>
                  </div>
                  <div className="mt-0.5 flex flex-wrap items-center gap-x-3 gap-y-0.5 text-xs text-fg-muted">
                    <span className="font-medium text-fg">Lv.{member.currentLevel}</span>
                    {member.deathCount > 0 && (
                      <span className="flex items-center gap-1">
                        <Skull size={11} aria-hidden /> {member.deathCount} death{member.deathCount === 1 ? "" : "s"}
                      </span>
                    )}
                    {member.ownerUsername ? (
                      <span>owner: <span className="text-fg">{member.ownerUsername}</span></span>
                    ) : (
                      // Only the host's owning user is recorded on the room; a joiner's is not, so
                      // this says so rather than inventing an attribution.
                      <span className="text-fg-subtle">owner not recorded for joiners</span>
                    )}
                    <span className="flex items-center gap-1 text-fg-subtle">
                      <Clock size={11} aria-hidden /> <RelativeTime iso={member.updatedAt} />
                    </span>
                  </div>
                </div>

                <CopyButton value={member.characterId} label="Character ID" />
              </Link>
            ))}
          </Card>
        )}
      </section>

      {/* ── Shared world state ── */}
      <section className="mt-8">
        <h2 className="flex items-center gap-2 font-display text-lg font-semibold text-fg">
          <Trophy size={17} aria-hidden /> World progress
        </h2>
        <p className="mt-1 text-xs text-fg-subtle">
          Shared by everyone in the room — rolling it back affects the whole party.
        </p>

        <div className="mt-3 grid gap-4 lg:grid-cols-3">
          <ProgressCard icon={Skull} title="Bosses defeated" items={progress.bosses}
            empty="None defeated yet." />
          <ProgressCard icon={Flag} title="Rest points found" items={progress.checkpoints}
            empty="None discovered yet." />
          <Card className="p-4">
            <h3 className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-fg-subtle">
              <Sparkles size={12} aria-hidden /> Quests
            </h3>
            {progress.quests.length === 0 ? (
              <p className="mt-2 text-sm text-fg-muted">No quest progress recorded.</p>
            ) : (
              <ul className="mt-2 space-y-1 text-sm">
                {progress.quests.map((q) => (
                  <li key={q.id} className="flex items-center justify-between gap-2">
                    <span className="truncate text-fg-muted">{q.id}</span>
                    <span className="shrink-0 tabular-nums text-xs text-fg">
                      state {q.state}{q.progress > 0 ? ` · ${q.progress}` : ""}
                    </span>
                  </li>
                ))}
              </ul>
            )}
          </Card>
        </div>
      </section>

      {/* ── State history ── */}
      <section className="mt-8">
        <h2 className="flex items-center gap-2 font-display text-lg font-semibold text-fg">
          <History size={17} aria-hidden /> World state history
        </h2>
        <p className="mt-1 text-xs text-fg-subtle">
          One entry per save. If the owner rolled the room back, progress here goes down rather than up.
        </p>

        {room.stateHistory.length === 0 ? (
          <Card className="mt-3 p-4 text-sm text-fg-muted">No snapshots recorded yet.</Card>
        ) : (
          <Card className="mt-3 divide-y divide-border p-0">
            {room.stateHistory.map((h) => (
              <div key={h.id} className="flex flex-wrap items-center gap-x-4 gap-y-1 px-4 py-2.5 text-sm">
                <span className="text-fg-muted"><RelativeTime iso={h.capturedAt} /></span>
                <span className="rounded-full bg-surface-3 px-2 py-0.5 text-[11px] text-fg-muted">{h.eventType}</span>
                {h.currentScene && <span className="truncate text-xs text-fg-muted">{h.currentScene}</span>}
                <span className="ml-auto flex items-center gap-3 text-xs text-fg-subtle">
                  <span>{h.worldStateCount} flag{h.worldStateCount === 1 ? "" : "s"}</span>
                  <span>{h.fogCellCount} cells</span>
                  {h.playTimeSeconds > 0 && <span>{formatPlaytime(h.playTimeSeconds)}</span>}
                </span>
              </div>
            ))}
          </Card>
        )}
      </section>

      <p className="mt-8 text-xs text-fg-subtle">
        Room created {formatDateTime(room.createdAt)} · last played {formatDateTime(room.lastPlayedAt)}
      </p>
    </div>
  );
}

function Fact({ icon: Icon, label, value }: {
  icon: React.ComponentType<{ size?: number; className?: string }>;
  label: string;
  value: string;
}) {
  return (
    <Card className="p-4">
      <p className="flex items-center gap-1.5 text-xs uppercase tracking-wider text-fg-subtle">
        <Icon size={12} className="text-accent" aria-hidden /> {label}
      </p>
      <p className="mt-1 truncate font-medium text-fg">{value}</p>
    </Card>
  );
}

function ProgressCard({ icon: Icon, title, items, empty }: {
  icon: React.ComponentType<{ size?: number; className?: string }>;
  title: string;
  items: string[];
  empty: string;
}) {
  return (
    <Card className="p-4">
      <h3 className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-fg-subtle">
        <Icon size={12} aria-hidden /> {title}
      </h3>
      {items.length === 0 ? (
        <p className="mt-2 text-sm text-fg-muted">{empty}</p>
      ) : (
        <ul className="mt-2 space-y-1 text-sm text-fg-muted">
          {items.map((id) => <li key={id} className="truncate">{id}</li>)}
        </ul>
      )}
    </Card>
  );
}

export default function AdminRoomDetailPage() {
  return (
    <Suspense fallback={null}>
      <AdminRoomDetail />
    </Suspense>
  );
}
