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

/**
 * Bag capacities, mirroring the `[Capacity(n)]` attributes on PlayerInventory's networked arrays.
 * The save blob encodes slot position by list index (every slot is written, empties included), so we
 * render a fixed grid of this size and index into it — that is what makes item placement visible.
 * A shorter list (older save) just leaves the tail empty; a longer one is clamped by the caller.
 */
const BAG_CAPACITY = { equipmentSlots: 40, accessorySlots: 10, materialSlots: 14 } as const;

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
 * web has no live sprite atlas. Bags render as fixed grids so a slot's position matches where the
 * player actually put the item in-game. Gracefully degrades if the blob is missing or malformed.
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

  // Keep every slot, empties included, so index == in-game grid position.
  const bags = (
    [
      { label: "Equipment", key: "equipmentSlots" },
      { label: "Accessories", key: "accessorySlots" },
      { label: "Materials", key: "materialSlots" },
    ] as const
  ).map(({ label, key }) => {
    const saved = data[key] ?? [];
    const size = Math.max(BAG_CAPACITY[key], saved.length);
    return {
      label,
      slots: Array.from({ length: size }, (_, i) => (isFilled(saved[i]) ? saved[i] : undefined)),
      count: saved.filter(isFilled).length,
    };
  });

  const totalItems = equipped.length + bags.reduce((n, b) => n + b.count, 0);

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
        bag.count > 0 ? (
          <div key={bag.label}>
            <p className="mb-2 flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-fg-subtle">
              <Package size={12} /> {bag.label} ({bag.count}/{bag.slots.length})
            </p>
            <div className="grid grid-cols-5 gap-1.5 sm:grid-cols-8 lg:grid-cols-10">
              {bag.slots.map((it, i) =>
                it ? (
                  <div
                    key={i}
                    title={`Slot ${i + 1}: ${it.itemId}${it.amount > 1 ? ` ×${it.amount}` : ""}`}
                    className="relative flex aspect-square items-center justify-center rounded-md border border-border bg-surface-2 p-1"
                  >
                    <span className="line-clamp-2 break-all text-center text-[9px] leading-tight text-fg">{it.itemId}</span>
                    {it.amount > 1 && (
                      <span className="absolute bottom-0 right-0 rounded-tl bg-surface-3 px-1 text-[9px] tabular-nums text-fg-muted">
                        {it.amount}
                      </span>
                    )}
                  </div>
                ) : (
                  <div
                    key={i}
                    title={`Slot ${i + 1}: empty`}
                    className="aspect-square rounded-md border border-dashed border-border/50 bg-surface-1/30"
                  />
                ),
              )}
            </div>
          </div>
        ) : null,
      )}
    </div>
  );
}
