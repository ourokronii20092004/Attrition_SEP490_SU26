"use client";

import { Shirt, Hand, Footprints, HardHat, Sparkles, Gem, Package } from "lucide-react";

interface SlotSave { itemId: string; amount: number }
interface InventorySave {
  equipmentSlots?: SlotSave[];
  accessorySlots?: SlotSave[];
  materialSlots?: SlotSave[];
  equippedHead?: SlotSave;
  equippedChest?: SlotSave;
  equippedLegs?: SlotSave;
  equippedBoots?: SlotSave;
  equippedSkill?: SlotSave;
  equippedAccessory?: SlotSave;
}

const isFilled = (s?: SlotSave): s is SlotSave => !!s && !!s.itemId && s.itemId.trim() !== "";

const EQUIP_SLOTS: { key: keyof InventorySave; label: string; icon: React.ComponentType<{ size?: number; className?: string }> }[] = [
  { key: "equippedHead", label: "Head", icon: HardHat },
  { key: "equippedChest", label: "Chest", icon: Shirt },
  { key: "equippedLegs", label: "Legs", icon: Hand },
  { key: "equippedBoots", label: "Boots", icon: Footprints },
  { key: "equippedSkill", label: "Skill", icon: Sparkles },
  { key: "equippedAccessory", label: "Accessory", icon: Gem },
];

/**
 * Renders a character's saved inventory blob (the JSON the Unity client pushes). The shape is
 * { equipped* slots, equipmentSlots[], accessorySlots[], materialSlots[] } with each slot an
 * { itemId, amount }. Item IDs map to the game's ItemSO catalog; we show the id + count since the
 * web has no live sprite atlas. Gracefully degrades if the blob is missing or malformed.
 */
export function InventoryView({ json }: { json: string | null | undefined }) {
  if (!json) {
    return <p className="text-sm text-fg-subtle">No inventory saved for this character.</p>;
  }

  let data: InventorySave;
  try {
    data = JSON.parse(json);
  } catch {
    return <p className="text-sm text-danger">Inventory data is corrupted and could not be read.</p>;
  }

  const equipped = EQUIP_SLOTS.map((s) => ({ ...s, slot: data[s.key] as SlotSave | undefined })).filter((s) => isFilled(s.slot));
  const bags: { label: string; items: SlotSave[] }[] = [
    { label: "Equipment", items: (data.equipmentSlots ?? []).filter(isFilled) },
    { label: "Accessories", items: (data.accessorySlots ?? []).filter(isFilled) },
    { label: "Materials", items: (data.materialSlots ?? []).filter(isFilled) },
  ];
  const totalItems = equipped.length + bags.reduce((n, b) => n + b.items.length, 0);

  if (totalItems === 0) {
    return <p className="text-sm text-fg-subtle">Inventory is empty.</p>;
  }

  return (
    <div className="space-y-4">
      {equipped.length > 0 && (
        <div>
          <p className="mb-2 text-xs font-semibold uppercase tracking-wider text-fg-subtle">Equipped</p>
          <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
            {equipped.map((s) => (
              <div key={s.key as string} className="flex items-center gap-2 rounded-lg border border-border bg-surface-2 px-3 py-2">
                <s.icon size={15} className="shrink-0 text-accent" />
                <div className="min-w-0">
                  <p className="text-[10px] uppercase tracking-wider text-fg-subtle">{s.label}</p>
                  <p className="truncate text-sm text-fg">{s.slot!.itemId}</p>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {bags.map((bag) =>
        bag.items.length > 0 ? (
          <div key={bag.label}>
            <p className="mb-2 flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-fg-subtle">
              <Package size={12} /> {bag.label} ({bag.items.length})
            </p>
            <div className="flex flex-wrap gap-1.5">
              {bag.items.map((it, i) => (
                <span key={`${it.itemId}-${i}`} className="inline-flex items-center gap-1.5 rounded-md border border-border bg-surface-2 px-2.5 py-1 text-xs text-fg">
                  {it.itemId}
                  {it.amount > 1 && <span className="rounded bg-surface-3 px-1 tabular-nums text-fg-muted">×{it.amount}</span>}
                </span>
              ))}
            </div>
          </div>
        ) : null
      )}
    </div>
  );
}
