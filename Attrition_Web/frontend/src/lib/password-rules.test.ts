import { describe, it, expect } from "vitest";
import {
  PASSWORD_MIN_LENGTH,
  PASSWORD_RULES,
  checkPassword,
  firstPasswordError,
  isPasswordValid,
} from "./password-rules";

const VALID = "Attrition!7";

describe("PASSWORD_RULES", () => {
  it("matches the backend contract: 8 chars, upper, lower, digit, special", () => {
    expect(PASSWORD_MIN_LENGTH).toBe(8);
    expect(PASSWORD_RULES.map((r) => r.id)).toEqual([
      "length", "uppercase", "lowercase", "digit", "special",
    ]);
  });
});

describe("isPasswordValid", () => {
  it("accepts a password meeting every rule", () => {
    expect(isPasswordValid(VALID)).toBe(true);
  });

  it("rejects one that is too short even with all character classes", () => {
    expect(isPasswordValid("Ab!1cde")).toBe(false); // 7 chars
    expect(isPasswordValid("Ab!1cdef")).toBe(true); // 8 chars
  });

  it("rejects a missing character class", () => {
    expect(isPasswordValid("attrition!7")).toBe(false);  // no uppercase
    expect(isPasswordValid("ATTRITION!7")).toBe(false);  // no lowercase
    expect(isPasswordValid("Attrition!!")).toBe(false);  // no digit
    expect(isPasswordValid("Attrition77")).toBe(false);  // no special
  });

  it("rejects an empty password", () => {
    expect(isPasswordValid("")).toBe(false);
  });

  it("counts a space as a special character, as the backend regex does", () => {
    // Backend uses [^a-zA-Z0-9], so a space qualifies; the UI must agree or it would
    // reject a password the server accepts.
    expect(isPasswordValid("Attrition 7x")).toBe(true);
  });

  it("accepts non-ASCII letters only via the special-character rule", () => {
    // "é" is not [A-Za-z], so it satisfies "special" but not "lowercase" — same as the server.
    expect(isPasswordValid("Attritioné7")).toBe(true);
  });
});

describe("checkPassword", () => {
  it("reports every rule as unmet for an empty value", () => {
    const result = checkPassword("");
    expect(result).toHaveLength(5);
    expect(result.every((r) => !r.met)).toBe(true);
  });

  it("reports every rule as met for a valid value", () => {
    expect(checkPassword(VALID).every((r) => r.met)).toBe(true);
  });

  it("reports rules independently as they are satisfied", () => {
    const byId = Object.fromEntries(checkPassword("abcdefgh").map((r) => [r.rule.id, r.met]));
    expect(byId.length).toBe(true);       // 8 chars
    expect(byId.lowercase).toBe(true);
    expect(byId.uppercase).toBe(false);
    expect(byId.digit).toBe(false);
    expect(byId.special).toBe(false);
  });

  it("keeps the display order stable", () => {
    expect(checkPassword("x").map((r) => r.rule.id)).toEqual(PASSWORD_RULES.map((r) => r.id));
  });
});

describe("firstPasswordError", () => {
  it("returns null when the password passes", () => {
    expect(firstPasswordError(VALID)).toBeNull();
  });

  it("names the length requirement with its number", () => {
    expect(firstPasswordError("Ab!1")).toBe("Password must be at least 8 characters.");
  });

  it("names the first missing character class once length is satisfied", () => {
    expect(firstPasswordError("attrition!7")).toBe("Password needs an uppercase letter.");
    expect(firstPasswordError("ATTRITION!7")).toBe("Password needs a lowercase letter.");
    expect(firstPasswordError("Attrition!!")).toBe("Password needs a number.");
    expect(firstPasswordError("Attrition77")).toBe("Password needs a special character.");
  });

  it("reports length first for an empty value, not a character class", () => {
    expect(firstPasswordError("")).toBe("Password must be at least 8 characters.");
  });
});
