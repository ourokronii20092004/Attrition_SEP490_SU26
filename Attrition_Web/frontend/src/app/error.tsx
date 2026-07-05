"use client";

import { useEffect } from "react";
import Link from "next/link";
import { Button } from "@/components/ui/button";
import { ErrorScreen, ERROR_COPY } from "@/components/error-screen";

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
    <ErrorScreen code={500} title={ERROR_COPY[500].title} message={ERROR_COPY[500].message}>
      <Button onClick={reset}>Try again</Button>
      <Link href="/"><Button variant="secondary">Go home</Button></Link>
    </ErrorScreen>
  );
}
