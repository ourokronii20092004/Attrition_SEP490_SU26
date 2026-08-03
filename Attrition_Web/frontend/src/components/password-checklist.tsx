"use client";

import { Check, Circle } from "lucide-react";
import { checkPassword } from "@/lib/password-rules";

/**
 * Live checklist of the password rules.
 *
 * The rules were previously invisible — you found out what they were by submitting and reading a
 * 400. Showing them up front, and ticking them off as they're satisfied, means the requirements
 * are answerable while typing.
 *
 * Rendered as a list rather than a single error line so every requirement is visible at once;
 * a one-at-a-time message makes a password feel like it's failing repeatedly.
 */
export function PasswordChecklist({ value, className }: { value: string; className?: string }) {
  const results = checkPassword(value);
  const metCount = results.filter((r) => r.met).length;

  return (
    <div className={className}>
      {/* Announce progress, not each individual tick, so a screen reader isn't spammed per keypress. */}
      <p className="sr-only" aria-live="polite">
        {metCount} of {results.length} password requirements met.
      </p>
      <ul className="mt-2 grid gap-1 sm:grid-cols-2">
        {results.map(({ rule, met }) => (
          <li
            key={rule.id}
            className={`flex items-center gap-1.5 text-xs transition-colors ${
              met ? "text-success" : "text-fg-subtle"
            }`}
          >
            {met ? (
              <Check size={13} className="shrink-0" aria-hidden />
            ) : (
              <Circle size={13} className="shrink-0 opacity-50" aria-hidden />
            )}
            {rule.label}
          </li>
        ))}
      </ul>
    </div>
  );
}
