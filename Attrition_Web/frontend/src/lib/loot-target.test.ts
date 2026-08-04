import { describe, it, expect } from "vitest";
import { resolveLootTarget } from "./loot-target";
import type { ItemResponse, SkillResponse } from "@/lib/types";

const items = [
  { itemId: "diamond_chest", name: "Diamond Armor", rarity: "Rare" },
  { itemId: "acc_shield", name: "Shield", rarity: "Common" },
] as unknown as ItemResponse[];

const skills = [
  { skillId: "skill_fire", name: "Fireball", rarity: "Common" },
  { skillId: "skill_earth", name: "Earth Bump", rarity: "Common" },
] as unknown as SkillResponse[];

describe("resolveLootTarget", () => {
  it("resolves an item by display name, since that is what loot rows store", () => {
    const t = resolveLootTarget("Diamond Armor", "Common", items, skills);
    expect(t.kind).toBe("item");
    expect(t.id).toBe("diamond_chest");
    expect(t.href).toBe("/items/diamond_chest");
  });

  it("prefers the catalogue's rarity over the loot row's stale copy", () => {
    // The loot row says Common; the catalogue says Rare. The catalogue is the source of truth.
    expect(resolveLootTarget("Diamond Armor", "Common", items, skills).rarity).toBe("Rare");
  });

  it("resolves a boss skill drop by id, not by name", () => {
    // Loot stores "skill_fire" (a Skill.Service id), while the skill's display name is "Fireball".
    const t = resolveLootTarget("skill_fire", "Common", items, skills);
    expect(t.kind).toBe("skill");
    expect(t.name).toBe("Fireball");
    expect(t.href).toBe("/skills/skill_fire");
  });

  it("does not mistake a skill id for an item", () => {
    expect(resolveLootTarget("skill_earth", "Common", items, skills).kind).toBe("skill");
  });

  it("falls back to the stored values when neither catalogue knows the entry", () => {
    const t = resolveLootTarget("mystery_thing", "Epic", items, skills);
    expect(t.kind).toBe("unknown");
    expect(t.name).toBe("mystery_thing");
    expect(t.rarity).toBe("Epic"); // the loot row's own copy is all we have
    expect(t.href).toBeNull();
  });

  it("prefers an item when a name collides with a skill id", () => {
    const collide = [{ itemId: "x", name: "skill_fire", rarity: "Epic" }] as unknown as ItemResponse[];
    expect(resolveLootTarget("skill_fire", "Common", collide, skills).kind).toBe("item");
  });

  it("uses the skill id as a label when the skill has no name", () => {
    const nameless = [{ skillId: "skill_void", name: "", rarity: "Rare" }] as unknown as SkillResponse[];
    expect(resolveLootTarget("skill_void", "Common", [], nameless).name).toBe("skill_void");
  });

  it("handles empty catalogues without throwing", () => {
    expect(resolveLootTarget("anything", "Common", [], []).kind).toBe("unknown");
  });
});
