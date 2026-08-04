/**
 * Password rules, mirroring Identity.Service's FluentValidation validators.
 *
 * Kept in one place because the rules were previously restated (or omitted) per form: the
 * register page had the full set, reset-password asked for 6 characters, and the settings and
 * forced-change forms checked nothing beyond "the two fields match". Anything the backend
 * rejected surfaced as a 400 after submitting rather than as guidance while typing.
 *
 * When Identity's validators change, change these — they are the same contract.
 */
import { z } from "zod";

export const PASSWORD_MIN_LENGTH = 8;

export interface PasswordRule {
  /** Stable key, handy for test assertions and React keys. */
  id: "length" | "uppercase" | "lowercase" | "digit" | "special";
  /** Phrased as the requirement itself, so the list reads as a checklist. */
  label: string;
  test: (value: string) => boolean;
}

/** The full rule set, in the order it's shown to the user. */
export const PASSWORD_RULES: readonly PasswordRule[] = [
  {
    id: "length",
    label: `At least ${PASSWORD_MIN_LENGTH} characters`,
    test: (v) => v.length >= PASSWORD_MIN_LENGTH,
  },
  { id: "uppercase", label: "An uppercase letter", test: (v) => /[A-Z]/.test(v) },
  { id: "lowercase", label: "A lowercase letter", test: (v) => /[a-z]/.test(v) },
  { id: "digit", label: "A number", test: (v) => /[0-9]/.test(v) },
  {
    id: "special",
    label: "A special character",
    // Matches the backend's [^a-zA-Z0-9]: anything that isn't a letter or digit counts.
    test: (v) => /[^A-Za-z0-9]/.test(v),
  },
] as const;

/** Which rules the value currently satisfies, for a live checklist. */
export function checkPassword(value: string): { rule: PasswordRule; met: boolean }[] {
  return PASSWORD_RULES.map((rule) => ({ rule, met: rule.test(value) }));
}

/** Whether the value satisfies every rule. */
export function isPasswordValid(value: string): boolean {
  return PASSWORD_RULES.every((rule) => rule.test(value));
}

/**
 * First unmet rule as a sentence, or null when the password passes.
 * For forms that show a single error message rather than the checklist.
 */
export function firstPasswordError(value: string): string | null {
  const failed = PASSWORD_RULES.find((rule) => !rule.test(value));
  if (!failed) return null;
  return failed.id === "length"
    ? `Password must be at least ${PASSWORD_MIN_LENGTH} characters.`
    : `Password needs ${failed.label.charAt(0).toLowerCase()}${failed.label.slice(1)}.`;
}

/**
 * Zod schema for a new password, for the forms built on react-hook-form.
 *
 * Messages are phrased as instructions ("Add at least one number.") because they surface under
 * the field as the user types, where an instruction reads better than a statement of fact.
 */
export const passwordSchema = z
  .string()
  .min(PASSWORD_MIN_LENGTH, `Password must be at least ${PASSWORD_MIN_LENGTH} characters.`)
  .regex(/[A-Z]/, "Add at least one uppercase letter.")
  .regex(/[a-z]/, "Add at least one lowercase letter.")
  .regex(/[0-9]/, "Add at least one number.")
  .regex(/[^A-Za-z0-9]/, "Add at least one special character.");

/**
 * One-line statement of the rules, for contexts that can't render the checklist
 * (a native prompt, a toast, an email).
 */
export const PASSWORD_RULES_SUMMARY =
  `Must be at least ${PASSWORD_MIN_LENGTH} characters and include an uppercase letter, ` +
  `a lowercase letter, a number, and a special character.`;
