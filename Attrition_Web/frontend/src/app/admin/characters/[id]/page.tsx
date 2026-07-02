"use client";

import { useQuery } from "@tanstack/react-query";
import { useParams } from "next/navigation";
import Link from "next/link";
import {
  ArrowLeft, Heart, MapPin, Clock, Gamepad2, Activity, Skull, Shield, TrendingUp, Backpack,
} from "lucide-react";
import { charactersApi } from "@/lib/api/characters";
import { useAuth } from "@/lib/providers";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { PageLoader } from "@/components/ui/spinner";
import { EmptyState } from "@/components/ui/empty-state";
import { SnapshotTimeline } from "@/components/snapshot-timeline";
import { InventoryView } from "@/components/inventory-view";
import { formatDateTime } from "@/lib/format-date";
import { qk } from "@/lib/query-keys";
import { useAdminPageLabel } from "@/lib/hooks/use-admin-page-label";
import type { SnapshotDto } from "@/lib/types";

function fmtPlaytime(seconds: number): string {
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  return h > 0 ? `${h}h ${m}m` : `${m}m`;
}

export default function AdminCharacterDetailPage() {
  const params = useParams<{ id: string }>();
  const { user } = useAuth();

  const { data: detail, isPending } = useQuery({
    queryKey: qk.admin.character(params.id),
    enabled: user?.role === "Admin" && !!params.id,
    queryFn: async () => {
      const res = await charactersApi.getAdmin(params.id);
      return res.success ? res.data : null;
    },
  });

  useAdminPageLabel(detail ? `Characters · ${detail.name}` : null);

  if (!user || user.role !== "Admin") return null;
  if (isPending) return <PageLoader />;

  if (!detail) {
    return (
      <EmptyState
        title="Character not found"
        description="This character may have been removed."
        action={<Link href="/admin/characters"><Button variant="secondary">Back to characters</Button></Link>}
      />
    );
  }

  // Snapshots come newest-first from the API; latest = first.
  const snaps = detail.snapshots ?? [];
  const latest: SnapshotDto | null = snaps[0] ?? null;
  const oldest: SnapshotDto | null = snaps[snaps.length - 1] ?? null;

  // Derived analytics across the snapshot history. (Gold is not tracked by the game — always 0 —
  // so it's intentionally omitted from the UI.)
  const peakLevel = snaps.reduce((m, s) => Math.max(m, s.level), 0);
  const deaths = snaps.filter((s) => !s.isAlive).length;
  const totalPlaytime = latest?.playtimeSeconds ?? 0;
  const hpPct = latest && latest.maxHp > 0 ? Math.round((latest.hp / latest.maxHp) * 100) : 0;

  return (
    <div className="mx-auto max-w-5xl">
      <Link href="/admin/characters" className="inline-flex items-center gap-1.5 text-sm text-fg-muted transition-colors hover:text-fg">
        <ArrowLeft size={16} /> Characters
      </Link>

      {/* Header */}
      <div className="mt-4 flex flex-wrap items-center gap-4">
        <span className="flex h-14 w-14 items-center justify-center rounded-xl bg-accent-soft text-accent">
          <Gamepad2 size={26} />
        </span>
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <h1 className="font-display text-3xl font-bold text-fg">{detail.name}</h1>
            <span className="rounded-full bg-surface-3 px-2.5 py-0.5 text-xs font-medium text-fg-muted">{detail.archetype}</span>
            {latest && (
              <span className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${latest.isAlive ? "bg-success/10 text-success" : "bg-danger/10 text-danger"}`}>
                {latest.isAlive ? "Alive" : "Dead"}
              </span>
            )}
          </div>
          <p className="mt-1 text-sm text-fg-muted">
            Owner:{" "}
            <Link href={`/admin/users/${detail.ownerId}`} className="text-accent hover:underline">
              {detail.ownerId.slice(0, 8)}…
            </Link>
          </p>
        </div>
      </div>

      {/* Current state cards */}
      {latest ? (
        <div className="mt-6 grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5">
          <StatCard icon={TrendingUp} label="Level" value={latest.level} />
          <StatCard icon={Heart} label="HP" value={`${latest.hp}/${latest.maxHp}`} sub={`${hpPct}%`} />
          <StatCard icon={Clock} label="Playtime" value={fmtPlaytime(totalPlaytime)} />
          <StatCard icon={Activity} label="Snapshots" value={snaps.length} />
          <StatCard icon={MapPin} label="Room" value={latest.roomCode ?? "—"} />
        </div>
      ) : (
        <p className="mt-6 rounded-lg bg-surface-2 px-4 py-3 text-sm text-fg-muted">No snapshots recorded for this character yet.</p>
      )}

      {/* Inventory */}
      <Card className="mt-6 p-4">
        <h2 className="flex items-center gap-2 text-sm font-semibold text-fg"><Backpack size={15} /> Inventory</h2>
        <div className="mt-3">
          <InventoryView json={detail.inventoryJson} />
        </div>
      </Card>

      <div className="mt-6 grid gap-4 lg:grid-cols-3">
        {/* Lifetime analytics */}
        <Card className="p-4">
          <h2 className="text-sm font-semibold text-fg">Lifetime</h2>
          <dl className="mt-3 space-y-2.5 text-sm">
            <Row icon={TrendingUp} label="Peak level" value={peakLevel} />
            <Row icon={Skull} label="Death events" value={deaths} />
            <Row icon={Shield} label="Created" value={oldest ? formatDateTime(oldest.capturedAt) : "—"} />
            <Row icon={Clock} label="Last update" value={latest ? formatDateTime(latest.capturedAt) : formatDateTime(detail.updatedAt)} />
          </dl>
        </Card>

        {/* History timeline */}
        <Card className="p-4 lg:col-span-2">
          <h2 className="text-sm font-semibold text-fg">Snapshot history</h2>
          <div className="mt-3">
            <SnapshotTimeline snapshots={snaps} />
          </div>
        </Card>
      </div>
    </div>
  );
}

function StatCard({ icon: Icon, label, value, sub }: {
  icon: React.ComponentType<{ size?: number; className?: string }>;
  label: string; value: React.ReactNode; sub?: string;
}) {
  return (
    <Card className="p-3">
      <div className="flex items-center gap-1.5 text-fg-subtle">
        <Icon size={13} />
        <span className="text-[10px] uppercase tracking-wider">{label}</span>
      </div>
      <p className="mt-1 truncate text-lg font-semibold tabular-nums text-fg">{value}</p>
      {sub && <p className="text-xs text-fg-subtle">{sub}</p>}
    </Card>
  );
}

function Row({ icon: Icon, label, value }: {
  icon: React.ComponentType<{ size?: number; className?: string }>;
  label: string; value: React.ReactNode;
}) {
  return (
    <div className="flex items-center justify-between gap-2">
      <span className="flex items-center gap-2 text-fg-muted"><Icon size={14} className="text-fg-subtle" /> {label}</span>
      <span className="text-right font-medium tabular-nums text-fg">{value}</span>
    </div>
  );
}
