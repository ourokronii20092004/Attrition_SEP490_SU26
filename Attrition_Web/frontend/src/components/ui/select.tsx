import { clsx } from "clsx";
import { forwardRef } from "react";
import { ChevronDown } from "lucide-react";

interface SelectProps extends React.SelectHTMLAttributes<HTMLSelectElement> {
  label?: string;
  error?: string;
}

export const Select = forwardRef<HTMLSelectElement, SelectProps>(
  ({ label, error, className, id, children, ...props }, ref) => {
    const selectId = id ?? label?.toLowerCase().replace(/\s+/g, "-");
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
        {error && <p className="text-xs text-danger">{error}</p>}
      </div>
    );
  },
);

Select.displayName = "Select";
