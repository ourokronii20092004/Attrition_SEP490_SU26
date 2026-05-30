import { clsx } from "clsx";
import { forwardRef } from "react";

interface IconButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: "ghost" | "solid" | "danger";
  size?: "sm" | "md";
  label: string;
}

const VARIANTS = {
  ghost: "text-fg-muted hover:bg-surface-2 hover:text-accent active:bg-surface-3",
  solid: "bg-accent text-accent-fg hover:brightness-105 hover:shadow-[var(--shadow-glow)] active:brightness-95",
  danger: "text-fg-muted hover:bg-danger/10 hover:text-danger active:bg-danger/20",
} as const;

const SIZES = { sm: "h-9 w-9", md: "h-10 w-10" } as const;

export const IconButton = forwardRef<HTMLButtonElement, IconButtonProps>(
  ({ variant = "ghost", size = "md", label, className, children, ...props }, ref) => (
    <button
      ref={ref}
      aria-label={label}
      title={label}
      className={clsx(
        "inline-flex items-center justify-center rounded-md transition-[transform,background-color,color,box-shadow] duration-200 ease-[cubic-bezier(0.16,1,0.3,1)]",
        "focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent active:scale-95 disabled:opacity-50 disabled:pointer-events-none",
        VARIANTS[variant],
        SIZES[size],
        className,
      )}
      {...props}
    >
      {children}
    </button>
  ),
);

IconButton.displayName = "IconButton";
