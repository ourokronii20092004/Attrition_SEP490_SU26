import { clsx } from "clsx";
import { forwardRef, useId, useState } from "react";
import { Eye, EyeOff } from "lucide-react";

interface InputProps extends React.InputHTMLAttributes<HTMLInputElement> {
  label?: string;
  error?: string;
}

export const Input = forwardRef<HTMLInputElement, InputProps>(({ label, error, className, id, type, ...props }, ref) => {
  // A stable generated fallback: deriving the id from the label alone collided whenever two fields
  // shared a label on one page (two "Name" inputs pointed the same htmlFor at the first one).
  const generatedId = useId();
  const inputId = id ?? (label ? `${label.toLowerCase().replace(/\s+/g, "-")}-${generatedId}` : generatedId);
  const errorId = `${inputId}-error`;
  // Password fields get a reveal toggle so users can check what they typed (register/login/settings).
  const isPassword = type === "password";
  const [reveal, setReveal] = useState(false);
  const effectiveType = isPassword && reveal ? "text" : type;

  return (
    <div className="space-y-1.5">
      {label && (
        <label htmlFor={inputId} className="block text-xs font-medium uppercase tracking-wider text-fg-muted">
          {label}
        </label>
      )}
      <div className="relative">
        <input
          ref={ref}
          id={inputId}
          type={effectiveType}
          // Without these, a screen reader reads the field as valid and never reaches the error
          // text sitting next to it in the DOM.
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? errorId : undefined}
          className={clsx(
            "w-full rounded-md border border-border bg-surface-2/60 px-3.5 py-2.5 text-fg outline-none transition-colors",
            "placeholder:text-fg-subtle focus:border-accent focus:bg-surface-2 focus:ring-1 focus:ring-accent",
            isPassword && "pr-11",
            error && "border-danger focus:border-danger focus:ring-danger",
            className,
          )}
          {...props}
        />
        {isPassword && (
          <button
            type="button"
            onClick={() => setReveal((v) => !v)}
            tabIndex={-1}
            aria-label={reveal ? "Hide password" : "Show password"}
            className="absolute inset-y-0 right-0 flex items-center px-3 text-fg-subtle transition-colors hover:text-fg"
          >
            {reveal ? <EyeOff size={16} /> : <Eye size={16} />}
          </button>
        )}
      </div>
      {error && <p id={errorId} className="text-xs text-danger">{error}</p>}
    </div>
  );
});

Input.displayName = "Input";
