"use client";

import { useEffect, useState } from "react";
import { clsx } from "clsx";
import { Spinner } from "@/components/ui/spinner";
import { LOADING_MESSAGES, randomLoadingMessage } from "@/lib/loading-messages";
import { SITE_NAME } from "@/lib/config";

/**
 * Spinner + a cycling tongue-in-cheek status line.
 * - `fullscreen`: a branded, opaque overlay (used for the app boot splash).
 * - otherwise: an inline centered block (used for route-transition suspense).
 *
 * Starts on a fixed message so the server and first client render match, then randomizes and
 * cycles on the client — avoids a hydration mismatch from Math.random() at render time.
 */
export function LoadingScreen({ fullscreen = false }: { fullscreen?: boolean }) {
  const [msg, setMsg] = useState<string>(LOADING_MESSAGES[0]);
  // Trailing ellipsis that builds up . → .. → ... (JS-driven so it animates even under
  // prefers-reduced-motion, which would otherwise freeze a CSS animation).
  const [dots, setDots] = useState(1);

  useEffect(() => {
    setMsg(randomLoadingMessage());
    const msgId = setInterval(() => setMsg((prev) => randomLoadingMessage(prev)), 2000);
    const dotId = setInterval(() => setDots((d) => (d % 3) + 1), 400);
    return () => { clearInterval(msgId); clearInterval(dotId); };
  }, []);

  return (
    <div
      className={clsx(
        "flex flex-col items-center justify-center gap-5",
        fullscreen ? "fixed inset-0 z-[9999] overflow-hidden bg-bg" : "min-h-[40vh]",
      )}
      role="status"
      aria-live="polite"
      aria-busy="true"
    >
      {fullscreen && (
        <span aria-hidden className="pointer-events-none absolute h-72 w-72 rounded-full bg-accent/10 blur-[110px]" />
      )}

      <div className="relative flex flex-col items-center gap-5">
        {fullscreen && (
          <span className="font-display text-lg font-bold uppercase tracking-[0.35em] text-fg-subtle">
            {SITE_NAME}
          </span>
        )}
        <Spinner className={fullscreen ? "h-9 w-9" : "h-7 w-7"} />
        {/* key={msg} re-triggers the fade each time the line changes. The invisible dots keep the
            total width fixed at "..." so the growing ellipsis doesn't shift the centered text. */}
        <p key={msg} className="animate-fade-in font-mono text-sm tracking-wide text-fg-muted">
          {msg}
          {".".repeat(dots)}
          <span aria-hidden className="invisible">{".".repeat(3 - dots)}</span>
        </p>
      </div>
    </div>
  );
}
