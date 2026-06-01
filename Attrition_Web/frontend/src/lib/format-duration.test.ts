import { describe, it, expect } from "vitest";
import { formatDuration } from "./format-duration";

describe("formatDuration", () => {
  it("formats seconds as m:ss", () => {
    expect(formatDuration(0)).toBe("0:00");
    expect(formatDuration(5)).toBe("0:05");
    expect(formatDuration(59)).toBe("0:59");
    expect(formatDuration(60)).toBe("1:00");
    expect(formatDuration(83)).toBe("1:23");
    expect(formatDuration(600)).toBe("10:00");
    expect(formatDuration(3661)).toBe("61:01");
  });

  it("zero-pads the seconds component", () => {
    expect(formatDuration(61)).toBe("1:01");
    expect(formatDuration(125)).toBe("2:05");
  });

  it("floors fractional seconds", () => {
    expect(formatDuration(83.9)).toBe("1:23");
  });

  it("returns 0:00 for non-finite input (metadata not loaded yet)", () => {
    expect(formatDuration(NaN)).toBe("0:00");
    expect(formatDuration(Infinity)).toBe("0:00");
  });
});
