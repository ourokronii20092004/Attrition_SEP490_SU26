import { describe, it, expect } from "vitest";
import {
  RARITY_ORDER,
  canonicalRarity,
  rarityColor,
  rarityMatches,
  rarityRank,
  RARITY_FALLBACK_COLOR,
} from "./rarity";

describe("canonicalRarity", () => {
  it("accepts the canonical spellings", () => {
    expect(canonicalRarity("Common")).toBe("Common");
    expect(canonicalRarity("Legendary")).toBe("Legendary");
  });

  it("tolerates the casing and whitespace free-text entry produces", () => {
    // This is the actual failure: admins type rarity by hand, so "rare" reached the filter and
    // never matched "Rare".
    expect(canonicalRarity("rare")).toBe("Rare");
    expect(canonicalRarity("RARE")).toBe("Rare");
    expect(canonicalRarity("  Epic  ")).toBe("Epic");
  });

  it("returns null for values that aren't on the ladder", () => {
    expect(canonicalRarity("Mythic")).toBeNull();
    expect(canonicalRarity("")).toBeNull();
    expect(canonicalRarity(null)).toBeNull();
    expect(canonicalRarity(undefined)).toBeNull();
  });
});

describe("rarityMatches", () => {
  it("treats an empty selection as 'all'", () => {
    expect(rarityMatches("Rare", "")).toBe(true);
    expect(rarityMatches(null, "")).toBe(true);
  });

  it("matches across casing and whitespace", () => {
    expect(rarityMatches("rare", "Rare")).toBe(true);
    expect(rarityMatches("  EPIC ", "Epic")).toBe(true);
  });

  it("does not match different rarities", () => {
    expect(rarityMatches("Common", "Rare")).toBe(false);
    expect(rarityMatches("Epic", "Legendary")).toBe(false);
  });

  it("can still filter an off-ladder value against itself", () => {
    expect(rarityMatches("Mythic", "Mythic")).toBe(true);
    expect(rarityMatches("mythic", "Mythic")).toBe(true);
    expect(rarityMatches("Mythic", "Rare")).toBe(false);
  });

  it("does not match a missing stored value against a real selection", () => {
    expect(rarityMatches(null, "Rare")).toBe(false);
    expect(rarityMatches("", "Rare")).toBe(false);
  });
});

describe("rarityRank", () => {
  it("orders the ladder weakest to strongest", () => {
    expect(rarityRank("Common")).toBeLessThan(rarityRank("Rare"));
    expect(rarityRank("Rare")).toBeLessThan(rarityRank("Legendary"));
  });

  it("ranks unrecognised values below Common instead of dropping them", () => {
    expect(rarityRank("Mythic")).toBe(-1);
    expect(rarityRank(null)).toBe(-1);
  });

  it("is case-insensitive", () => {
    expect(rarityRank("epic")).toBe(rarityRank("Epic"));
  });
});

describe("rarityColor", () => {
  it("returns a distinct class per known rarity", () => {
    const classes = RARITY_ORDER.map((r) => rarityColor(r));
    expect(new Set(classes).size).toBe(RARITY_ORDER.length);
  });

  it("falls back for unknown or missing values rather than returning undefined", () => {
    expect(rarityColor("Mythic")).toBe(RARITY_FALLBACK_COLOR);
    expect(rarityColor(null)).toBe(RARITY_FALLBACK_COLOR);
  });

  it("resolves colours case-insensitively", () => {
    expect(rarityColor("legendary")).toBe(rarityColor("Legendary"));
  });
});
