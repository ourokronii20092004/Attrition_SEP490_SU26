import { clsx } from "clsx";
import { Loader2 } from "lucide-react";

interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: "primary" | "secondary" | "danger" | "ghost";
  size?: "sm" | "md" | "lg";
  loading?: boolean;
}

export function Button({ variant = "primary", size = "md", loading, className, children, disabled, ...props }: ButtonProps) {
  const base = "inline-flex items-center justify-center font-medium rounded-lg transition disabled:opacity-50 disabled:pointer-events-none";
  const variants = {
    primary: "bg-accent text-accent-fg hover:opacity-90",
    secondary: "border border-border-strong text-fg hover:bg-surface-2",
    danger: "bg-danger text-white hover:opacity-90",
    ghost: "text-fg-muted hover:bg-surface-2 hover:text-fg",
  };
  const sizes = {
    sm: "px-3 py-1.5 text-sm",
    md: "px-4 py-2 text-sm",
    lg: "px-6 py-3 text-base",
  };

  return (
    <button className={clsx(base, variants[variant], sizes[size], className)} disabled={disabled || loading} {...props}>
      {loading && <Loader2 size={16} className="mr-2 animate-spin" />}
      {children}
    </button>
  );
}
