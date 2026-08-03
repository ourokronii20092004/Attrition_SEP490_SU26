import { describe, it, expect } from "vitest";
import { buildSkillTree, RARITY_TIERS } from "./skill-tree";
import type { SkillResponse } from "@/lib/types";

function skill(skillId: string, element: string, rarity: string, manaCost = 0, name = skillId): SkillResponse {
  return { skillId, name, description: null, iconKey: null, rarity, element, manaCost } as unknown as SkillResponse;
}

describe("buildSkillTree", () => {
  it("makes one branch per element, ordered as the game lists them", () => {
    const tree = buildSkillTree([
      skill("a", "Thunder", "Common"),
      skill("b", "Fire", "Common"),
      skill("c", "Earth", "Common"),
    ]);
    expect(tree.map((b) => b.element)).toEqual(["Fire", "Earth", "Thunder"]);
  });

  it("splits a branch into rarity tiers, weakest first", () => {
    const tree = buildSkillTree([
      skill("a", "Fire", "Legendary"),
      skill("b", "Fire", "Common"),
      skill("c", "Fire", "Rare"),
    ]);
    expect(tree[0].tiers.map((t) => t.rarity)).toEqual(["Common", "Rare", "Legendary"]);
  });

  it("drops tiers with no skills instead of leaving empty rows", () => {
    const tree = buildSkillTree([skill("a", "Fire", "Common"), skill("b", "Fire", "Epic")]);
    expect(tree[0].tiers).toHaveLength(2);
    expect(tree[0].tiers.map((t) => t.rarity)).toEqual(["Common", "Epic"]);
  });

  it("sorts within a tier by mana cost, then name", () => {
    const tree = buildSkillTree([
      skill("a", "Fire", "Common", 30, "Zeta"),
      skill("b", "Fire", "Common", 10, "Alpha"),
      skill("c", "Fire", "Common", 10, "Beta"),
    ]);
    expect(tree[0].tiers[0].skills.map((s) => s.name)).toEqual(["Alpha", "Beta", "Zeta"]);
  });

  it("counts every skill in the branch total", () => {
    const tree = buildSkillTree([
      skill("a", "Fire", "Common"),
      skill("b", "Fire", "Rare"),
      skill("c", "Fire", "Legendary"),
    ]);
    expect(tree[0].total).toBe(3);
  });

  it("keeps an unrecognised element as its own branch, sorted last", () => {
    const tree = buildSkillTree([skill("a", "Void", "Common"), skill("b", "Fire", "Common")]);
    expect(tree.map((b) => b.element)).toEqual(["Fire", "Void"]);
  });

  it("groups a missing element under Unaligned rather than dropping the skill", () => {
    const tree = buildSkillTree([skill("a", "", "Common")]);
    expect(tree).toHaveLength(1);
    expect(tree[0].element).toBe("Unaligned");
    expect(tree[0].total).toBe(1);
  });

  it("keeps an off-ladder rarity in an Other tier at the end", () => {
    const tree = buildSkillTree([skill("a", "Fire", "Mythic"), skill("b", "Fire", "Common")]);
    expect(tree[0].tiers.map((t) => t.rarity)).toEqual(["Common", "Other"]);
    expect(tree[0].tiers[1].skills[0].skillId).toBe("a");
  });

  it("matches rarity case-insensitively", () => {
    const tree = buildSkillTree([skill("a", "Fire", "common"), skill("b", "Fire", "RARE")]);
    expect(tree[0].tiers.map((t) => t.rarity)).toEqual(["Common", "Rare"]);
  });

  it("returns nothing for no skills", () => {
    expect(buildSkillTree([])).toEqual([]);
  });

  it("exposes the rarity ladder used for tier depth", () => {
    expect(RARITY_TIERS).toEqual(["Common", "Uncommon", "Rare", "Epic", "Legendary"]);
  });
});
