import { clsx } from "clsx";

const SIZES = {
  sm: "max-w-md",
  md: "max-w-3xl",
  lg: "max-w-5xl",
  xl: "max-w-6xl",
} as const;

interface PageShellProps {
  size?: keyof typeof SIZES;
  className?: string;
  children: React.ReactNode;
}

/** Standard page container — spacious vertical rhythm, generous gutters. */
export function PageShell({ size = "xl", className, children }: PageShellProps) {
  return (
    <div className={clsx("mx-auto w-full px-5 py-12 sm:px-8 sm:py-16", SIZES[size], className)}>
      {children}
    </div>
  );
}
