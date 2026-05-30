import type { SnapshotDto } from "@/lib/types";

export function SnapshotTimeline({ snapshots }: { snapshots: SnapshotDto[] }) {
  if (snapshots.length === 0) {
    return <p className="py-3 text-sm text-fg-subtle">No snapshots recorded.</p>;
  }
  return (
    <ol className="relative space-y-3 border-l border-border pl-4">
      {snapshots.map((s, i) => (
        <li key={i} className="relative">
          <span className={`absolute -left-[1.30rem] top-1 h-2.5 w-2.5 rounded-full ${s.isAlive ? "bg-success" : "bg-danger"}`} />
          <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-xs">
            <span className="font-medium text-fg">Lv.{s.level}</span>
            <span className="text-fg-muted">HP {s.hp}/{s.maxHp}</span>
            <span className="text-fg-muted">{s.gold}g</span>
            {s.roomCode && <span className="text-fg-muted">{s.roomCode}</span>}
            <span className="rounded bg-surface-3 px-1.5 py-0.5 text-fg-subtle">{s.eventType}</span>
            <span className="text-fg-subtle">{formatPlaytime(s.playtimeSeconds)}</span>
            <span className="ml-auto text-fg-subtle">{new Date(s.capturedAt).toLocaleString()}</span>
          </div>
        </li>
      ))}
    </ol>
  );
}

function formatPlaytime(seconds: number): string {
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  return h > 0 ? `${h}h ${m}m` : `${m}m`;
}
