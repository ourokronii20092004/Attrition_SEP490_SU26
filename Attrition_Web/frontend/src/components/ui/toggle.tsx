"use client";

import { clsx } from "clsx";

interface ToggleProps {
  checked: boolean;
  onChange: (checked: boolean) => void;
  label?: string;
  description?: string;
  disabled?: boolean;
}

/** Premium switch toggle. Replaces native checkboxes for settings preferences. */
export function Toggle({ checked, onChange, label, description, disabled }: ToggleProps) {
  const button = (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      aria-label={label}
      disabled={disabled}
      onClick={() => onChange(!checked)}
      className={clsx(
        "relative inline-flex h-6 w-11 shrink-0 items-center rounded-full transition-colors duration-200 outline-none focus-visible:ring-2 focus-visible:ring-accent focus-visible:ring-offset-2 focus-visible:ring-offset-surface",
        checked ? "bg-accent" : "bg-surface-3",
        disabled && "cursor-not-allowed opacity-50",
      )}
    >
      <span
        className={clsx(
          "inline-block h-5 w-5 transform rounded-full bg-white shadow-sm transition-transform duration-200",
          checked ? "translate-x-[1.375rem]" : "translate-x-0.5",
        )}
      />
    </button>
  );

  if (!label && !description) return button;

  return (
    <label className="flex items-center justify-between gap-4">
      <span className="min-w-0">
        {label && <span className="block text-sm font-medium text-fg">{label}</span>}
        {description && <span className="block text-xs text-fg-muted">{description}</span>}
      </span>
      {button}
    </label>
  );
}
