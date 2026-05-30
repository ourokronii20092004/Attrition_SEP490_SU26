import { Loader2 } from "lucide-react";

export function Spinner({ className }: { className?: string }) {
  return <Loader2 className={`animate-spin text-fg-muted ${className ?? "h-6 w-6"}`} />;
}

export function PageLoader() {
  return (
    <div className="flex min-h-[40vh] items-center justify-center">
      <Spinner className="h-8 w-8" />
    </div>
  );
}
