import { clsx } from "clsx";

interface CardProps extends React.HTMLAttributes<HTMLDivElement> {
  interactive?: boolean;
  glass?: boolean;
}

/**
 * Surface container. `interactive` adds a hover lift + corruption-edge glow for
 * clickable cards; `glass` swaps the solid fill for the translucent blur.
 */
export function Card({ interactive, glass, className, children, ...props }: CardProps) {
  return (
    <div
      className={clsx(
        glass ? "glass" : "card",
        interactive &&
          "transition-[transform,border-color,box-shadow] duration-300 ease-[cubic-bezier(0.16,1,0.3,1)] hover:-translate-y-1 hover:border-accent/60 hover:shadow-[var(--shadow-glow)]",
        className,
      )}
      {...props}
    >
      {children}
    </div>
  );
}
