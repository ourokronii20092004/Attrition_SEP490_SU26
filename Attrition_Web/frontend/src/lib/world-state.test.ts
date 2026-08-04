import { describe, it, expect } from "vitest";
import { splitWorldStates, parseFog, parseAllocated, unspentPoints } from "./world-state";
import type { WorldStateDto } from "./types";

const ws = (eventId: string, stateValue = 1, progress = 0): WorldStateDto => ({
  eventId,
  stateValue,
  progress,
  updatedAt: "2026-08-04T00:00:00Z",
});

describe("splitWorldStates", () => {
  it("routes each row by its prefix", () => {
    const out = splitWorldStates([
      ws("q:find_the_bell", 2, 3),
      ws("cp:Elf Valley Rest"),
      ws("severed_fang"),
    ]);
    expect(out.quests).toEqual([{ id: "find_the_bell", state: 2, progress: 3 }]);
    expect(out.checkpoints).toEqual(["Elf Valley Rest"]);
    expect(out.bosses).toEqual(["severed_fang"]);
  });

  it("keeps a boss id that merely starts with the letter q", () => {
    // The prefix is "q:", not "q" — a boss called "queen_moth" must not be read as a quest.
    const out = splitWorldStates([ws("queen_moth")]);
    expect(out.bosses).toEqual(["queen_moth"]);
    expect(out.quests).toEqual([]);
  });

  it("ignores cleared flags for bosses and checkpoints but keeps quest state 0", () => {
    // stateValue 0 means "not defeated"/"not discovered", while a quest legitimately sits at
    // state 0 (not started) and still needs to round-trip its progress counter.
    const out = splitWorldStates([ws("severed_fang", 0), ws("cp:Rest", 0), ws("q:intro", 0, 5)]);
    expect(out.bosses).toEqual([]);
    expect(out.checkpoints).toEqual([]);
    expect(out.quests).toEqual([{ id: "intro", state: 0, progress: 5 }]);
  });

  it("survives null, empty and malformed rows", () => {
    expect(splitWorldStates(null)).toEqual({ quests: [], checkpoints: [], bosses: [] });
    expect(splitWorldStates([ws("")])).toEqual({ quests: [], checkpoints: [], bosses: [] });
  });
});

describe("parseFog", () => {
  it("counts revealed cells per scene", () => {
    const out = parseFog(JSON.stringify(["Map1:0:0", "Map1:1:0", "Map2:4:7"]));
    expect(out.get("Map1")).toBe(2);
    expect(out.get("Map2")).toBe(1);
  });

  it("handles a scene name containing a colon", () => {
    // Real scene names include "Elf Valley -Map 3"; a colon would break a naive split.
    const out = parseFog(JSON.stringify(["Elf:Valley:3:9"]));
    expect(out.get("Elf:Valley")).toBe(1);
  });

  it("returns empty for missing or corrupt json rather than throwing", () => {
    expect(parseFog(null).size).toBe(0);
    expect(parseFog("not json").size).toBe(0);
    expect(parseFog(JSON.stringify({ nope: true })).size).toBe(0);
    expect(parseFog(JSON.stringify(["short"])).size).toBe(0);
  });
});

describe("unspentPoints", () => {
  it("grants 5 per level after the first", () => {
    expect(unspentPoints(1, [])).toBe(0);
    expect(unspentPoints(3, [])).toBe(10);
    expect(unspentPoints(3, [4, 0, 0, 6, 0, 0, 0])).toBe(0);
  });

  it("never reports negative when allocation exceeds the level grant", () => {
    // Possible if a save predates a leveling-config change; clamp instead of showing "-5 points".
    expect(unspentPoints(2, [99])).toBe(0);
  });
});

describe("parseAllocated", () => {
  it("reads the 7-entry array and tolerates junk", () => {
    expect(parseAllocated("[1,2,3,4,5,6,7]")).toEqual([1, 2, 3, 4, 5, 6, 7]);
    expect(parseAllocated(null)).toEqual([]);
    expect(parseAllocated("{}")).toEqual([]);
    expect(parseAllocated("[1,null,3]")).toEqual([1, 0, 3]);
  });
});
