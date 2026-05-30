import { clsx } from "clsx";

interface PageTitleProps {
  children: React.ReactNode;
  description?: React.ReactNode;
  actions?: React.ReactNode;
  eyebrow?: string;
  className?: string;
}

/** Page header: optional eyebrow + display heading + description and actions. */
export function PageTitle({ children, description, actions, eyebrow, className }: PageTitleProps) {
  return (
    <div className={clsx("mb-10 flex flex-wrap items-end justify-between gap-4", className)}>
      <div className="min-w-0">
        {eyebrow && (
          <p className="mb-3 text-xs font-medium uppercase tracking-[0.25em] text-accent">{eyebrow}</p>
        )}
        <h1 className="font-display text-4xl font-bold tracking-tight text-balance text-fg sm:text-5xl">
          {children}
        </h1>
        {description && <p className="mt-3 max-w-2xl text-fg-muted">{description}</p>}
      </div>
      {actions && <div className="flex shrink-0 items-center gap-2">{actions}</div>}
    </div>
  );
}

export function SectionTitle({ children, className }: { children: React.ReactNode; className?: string }) {
  return (
    <h2 className={clsx("font-display text-2xl font-semibold tracking-tight text-fg", className)}>
      {children}
    </h2>
  );
}
