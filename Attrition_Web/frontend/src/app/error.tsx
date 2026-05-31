"use client";

import { useEffect } from "react";
import { AlertTriangle } from "lucide-react";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";

/**
 * Route-level error boundary. Replaces the blank-screen-on-throw failure with a themed,
 * recoverable UI. `reset()` re-renders the segment without a full reload.
 */
export default function RouteError({ error, reset }: { error: Error & { digest?: string }; reset: () => void }) {
  useEffect(() => {
    // Surface to the console for diagnostics; a real telemetry sink could hook in here.
    console.error("Route error:", error);
  }, [error]);

  return (
    <div className="mx-auto flex min-h-[60vh] max-w-2xl items-center px-5">
      <EmptyState
        icon={AlertTriangle}
        title="Something went wrong"
        description="An unexpected error broke this page. You can try again, or head back home."
        className="w-full"
        action={
          <div className="flex justify-center gap-3">
            <Button onClick={reset}>Try again</Button>
            <Button variant="secondary" onClick={() => (window.location.href = "/")}>Go home</Button>
          </div>
        }
      />
    </div>
  );
}
