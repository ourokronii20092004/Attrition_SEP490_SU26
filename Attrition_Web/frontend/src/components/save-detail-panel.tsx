"use client";

import { Heart, Droplet, Zap, Swords, Shield, Sparkles, Gauge, Skull, MapPin, Clock } from "lucide-react";
import { Card } from "@/components/ui/card";
import { InventoryView } from "@/components/inventory-view";
import { RelativeTime } from "@/components/ui/relative-time";
import { formatPlaytime } from "@/lib/format-duration";
import { STAT_LABELS, parseAllocated, unspentPoints } from "@/lib/world-state";
import type { SaveDetailDto } from "@/lib/types";

/**
 * Everything a save file contains, rendered. Swapping the `save` prop is what makes clicking a
 * different save change every number on the page.
 *
 * `showDisplayOnlyNote` explains why the combat stats can look stale relative to a rolled-back
 * character: the game recomputes them on spawn, so they are a record of that moment rather than
 * something the save controls.
 */
export function SaveDetailPanel({ save, showDisplayOnlyNote = true }: {
  save: SaveDetailDto;
  showDisplayOnlyNote?: boolean;
}) {
  const allocated = parseAllocated(save.allocatedPointsJson);
  const unspent = unspentPoints(save.currentLevel, allocated);
  // Older rows predate these columns and store all-zero; showing "0 AD" would be a fabrication.
  const combatKnown = save.ad !== 0 || save.ap !== 0 || save.def !== 0 || save.res !== 0;

  return (
    <div className="space-y-6">
      {/* ── Headline ── */}
      <Card className="p-5">
        <div className="flex flex-wrap items-baseline justify-between gap-3">
          <div>
            <p className="text-xs uppercase tracking-wider text-fg-subtle">Level</p>
            <p className="font-display text-3xl font-bold text-fg">{save.currentLevel}</p>
          </div>
          <div className="flex flex-wrap items-center gap-2 text-xs">
            {save.isAlive ? (
              <span className="rounded-full bg-success/10 px-2.5 py-1 font-medium text-success">Alive</span>
            ) : (
              <span className="inline-flex items-center gap-1 rounded-full bg-danger/10 px-2.5 py-1 font-medium text-danger">
                <Skull size={11} aria-hidden /> Died
              </span>
            )}
            {save.isCurrent && (
              <span className="rounded-full bg-accent/10 px-2.5 py-1 font-semibold text-accent">Current progress</span>
            )}
          </div>
        </div>

        <div className="mt-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <Vital icon={Heart} label="Health" value={`${save.currentHp} / ${save.maxHp}`} tone="danger" />
          <Vital icon={Droplet} label="Mana" value={`${save.currentMana} / ${save.maxMana}`} tone="info" />
          <Vital icon={Zap} label="Stamina" value={String(save.maxStamina)} tone="warning" />
          <Vital icon={Gauge} label="Attack speed" value={save.attackSpeed.toFixed(2)} />
        </div>
      </Card>

      {/* ── Combat stats ── */}
      {combatKnown && (
        <section>
          <h2 className="font-display text-lg font-semibold text-fg">Combat</h2>
          <div className="mt-3 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            <Vital icon={Swords} label="Attack" value={String(save.ad)} />
            <Vital icon={Sparkles} label="Ability power" value={String(save.ap)} />
            <Vital icon={Shield} label="Defence" value={String(save.def)} />
            <Vital icon={Shield} label="Resistance" value={String(save.res)} />
          </div>
          {showDisplayOnlyNote && (
            <p className="mt-2 text-xs text-fg-subtle">
              Recorded as the game calculated them at this save. The game recalculates them from your
              gear and points when you load, so they aren&apos;t restored by rolling back.
            </p>
          )}
        </section>
      )}

      {/* ── Allocated points ── */}
      {allocated.length > 0 && (
        <section>
          <div className="flex flex-wrap items-baseline justify-between gap-2">
            <h2 className="font-display text-lg font-semibold text-fg">Stat points</h2>
            {unspent > 0 && (
              <span className="rounded-full bg-warning/10 px-2.5 py-1 text-xs font-medium text-warning">
                {unspent} unspent
              </span>
            )}
          </div>
          <Card className="mt-3 divide-y divide-border p-0">
            {STAT_LABELS.map((label, i) => (
              <div key={label} className="flex items-center justify-between px-4 py-2 text-sm">
                <span className="text-fg-muted">{label}</span>
                <span className="font-medium tabular-nums text-fg">{allocated[i] ?? 0}</span>
              </div>
            ))}
          </Card>
        </section>
      )}

      {/* ── Counters ── */}
      <section>
        <h2 className="font-display text-lg font-semibold text-fg">This run</h2>
        <div className="mt-3 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <Vital icon={Sparkles} label="Experience" value={save.currentExp.toLocaleString()} />
          <Vital icon={Skull} label="Deaths" value={String(save.deathCount)} tone={save.deathCount > 0 ? "danger" : undefined} />
          <Vital icon={Heart} label="Health flasks" value={`${save.healthCharges} / ${save.potionMaxFlasks}`} />
          <Vital icon={Droplet} label="Mana flasks" value={`${save.manaCharges} / ${save.potionMaxManaFlasks}`} />
        </div>
      </section>

      {/* ── Where ── */}
      <section>
        <h2 className="font-display text-lg font-semibold text-fg">Where</h2>
        <Card className="mt-3 p-4">
          <dl className="grid gap-3 text-sm sm:grid-cols-2">
            <Row label="Scene" value={save.currentScene ?? "Unknown"} icon={MapPin} />
            <Row label="Room" value={save.roomCode ?? "Solo / no room"} />
            <Row label="Rest point" value={save.lastRestPointId ?? "None recorded"} />
            <Row label="Playtime" value={save.playtimeSeconds > 0 ? formatPlaytime(save.playtimeSeconds) : "Not recorded"} />
            <Row label="Saved" value={<RelativeTime iso={save.capturedAt} />} icon={Clock} />
            <Row label="Trigger" value={save.eventType} />
          </dl>
        </Card>
      </section>

      {/* ── Inventory ── */}
      <section>
        <h2 className="font-display text-lg font-semibold text-fg">Inventory</h2>
        <div className="mt-3">
          <InventoryView json={save.inventoryJson} />
        </div>
      </section>
    </div>
  );
}

function Vital({ icon: Icon, label, value, tone }: {
  icon: React.ComponentType<{ size?: number; className?: string }>;
  label: string;
  value: string;
  tone?: "danger" | "info" | "warning";
}) {
  const toneClass =
    tone === "danger" ? "text-danger" : tone === "info" ? "text-info" : tone === "warning" ? "text-warning" : "text-accent";
  return (
    <Card className="p-4">
      <p className="flex items-center gap-1.5 text-xs uppercase tracking-wider text-fg-subtle">
        <Icon size={13} className={toneClass} aria-hidden /> {label}
      </p>
      <p className="mt-1.5 font-display text-xl font-semibold tabular-nums text-fg">{value}</p>
    </Card>
  );
}

function Row({ label, value, icon: Icon }: {
  label: string;
  value: React.ReactNode;
  icon?: React.ComponentType<{ size?: number; className?: string }>;
}) {
  return (
    <div>
      <dt className="flex items-center gap-1.5 text-xs uppercase tracking-wider text-fg-subtle">
        {Icon && <Icon size={12} aria-hidden />} {label}
      </dt>
      <dd className="mt-0.5 break-words font-medium text-fg">{value}</dd>
    </div>
  );
}
