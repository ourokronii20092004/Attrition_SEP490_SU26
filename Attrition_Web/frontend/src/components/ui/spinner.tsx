import { clsx } from "clsx";
import { Loader2 } from "lucide-react";

export function Spinner({ className }: { className?: string }) {
  // animate-spin-always keeps it turning even under prefers-reduced-motion (a spinner is a status
  // indicator). See globals.css.
  return <Loader2 className={clsx("animate-spin-always text-accent", className)} />;
}

export function PageLoader() {
  return (
    <div className="flex min-h-[40vh] items-center justify-center">
      <Spinner className="h-7 w-7" />
    </div>
  );
}
