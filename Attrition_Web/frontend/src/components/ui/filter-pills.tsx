import { clsx } from "clsx";

interface Option<T> {
  value: T;
  label: string;
}

interface FilterPillsProps<T> {
  options: Option<T>[];
  value: T;
  onChange: (value: T) => void;
  className?: string;
}

export function FilterPills<T extends string | number | null>({ options, value, onChange, className }: FilterPillsProps<T>) {
  return (
    <div className={clsx("flex flex-wrap gap-2", className)} role="tablist">
      {options.map((opt) => {
        const active = opt.value === value;
        return (
          <button
            key={String(opt.value)}
            role="tab"
            aria-selected={active}
            onClick={() => onChange(opt.value)}
            className={clsx(
              "rounded-md px-3.5 py-1.5 text-sm font-medium transition-all duration-200",
              active
                ? "bg-accent text-accent-fg shadow-[var(--shadow-glow)]"
                : "border border-border text-fg-muted hover:border-accent/60 hover:text-fg",
            )}
          >
            {opt.label}
          </button>
        );
      })}
    </div>
  );
}
