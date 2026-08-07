import { clsx } from "clsx";
import { forwardRef, useId } from "react";
import { ChevronDown } from "lucide-react";

interface SelectProps extends React.SelectHTMLAttributes<HTMLSelectElement> {
  label?: string;
  error?: string;
}

export const Select = forwardRef<HTMLSelectElement, SelectProps>(
  ({ label, error, className, id, children, ...props }, ref) => {
    // Same collision guard as Input: two selects sharing a label (e.g. two "Stat" dropdowns in the
    // item modifier list) would otherwise both claim the same id.
    const generatedId = useId();
    const selectId = id ?? (label ? `${label.toLowerCase().replace(/\s+/g, "-")}-${generatedId}` : generatedId);
    const errorId = `${selectId}-error`;
    return (
      <div className="space-y-1.5">
        {label && (
          <label htmlFor={selectId} className="block text-xs font-medium uppercase tracking-wider text-fg-muted">
            {label}
          </label>
        )}
        <div className="relative">
          <select
            ref={ref}
            id={selectId}
            aria-invalid={error ? true : undefined}
            aria-describedby={error ? errorId : undefined}
            className={clsx(
              "w-full appearance-none rounded-md border border-border bg-surface-2/60 px-3.5 py-2.5 pr-9 text-fg outline-none transition-colors",
              "focus:border-accent focus:bg-surface-2 focus:ring-1 focus:ring-accent",
              error && "border-danger focus:border-danger focus:ring-danger",
              className,
            )}
            {...props}
          >
            {children}
          </select>
          <ChevronDown size={16} className="pointer-events-none absolute right-3 top-1/2 -translate-y-1/2 text-fg-subtle" />
        </div>
        {error && <p id={errorId} className="text-xs text-danger">{error}</p>}
      </div>
    );
  },
);

Select.displayName = "Select";
