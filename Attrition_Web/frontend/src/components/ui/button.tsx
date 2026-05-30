import { clsx } from "clsx";
import { Loader2 } from "lucide-react";

interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: "primary" | "secondary" | "danger" | "ghost";
  size?: "sm" | "md" | "lg";
  loading?: boolean;
}

export function Button({ variant = "primary", size = "md", loading, className, children, disabled, ...props }: ButtonProps) {
  const base =
    "inline-flex items-center justify-center font-medium rounded-md transition-[transform,background-color,box-shadow,border-color,color] duration-200 ease-[cubic-bezier(0.16,1,0.3,1)] active:scale-[0.97] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent disabled:opacity-50 disabled:pointer-events-none";
  const variants = {
    primary:
      "bg-accent text-accent-fg shadow-[0_0_0_1px_color-mix(in_srgb,var(--color-accent)_40%,transparent)] hover:shadow-[var(--shadow-glow)] hover:brightness-105 active:brightness-95",
    secondary:
      "border border-border-strong text-fg hover:border-accent hover:text-accent hover:bg-accent-soft/40 active:bg-surface-3",
    danger: "bg-danger text-white hover:brightness-110 active:brightness-95",
    ghost: "text-fg-muted hover:bg-surface-2 hover:text-fg active:bg-surface-3",
  };
  const sizes = {
    sm: "px-3.5 py-1.5 text-xs",
    md: "px-5 py-2.5 text-sm",
    lg: "px-7 py-3.5 text-sm tracking-wide",
  };
  return (
    <button className={clsx(base, variants[variant], sizes[size], className)} disabled={disabled || loading} {...props}>
      {loading && <Loader2 size={16} className="mr-2 animate-spin" />}
      {children}
    </button>
  );
}
