"use client";

import { useState } from "react";
import { EyeOff, Eye } from "lucide-react";

/**
 * Spoiler block — collapsed by default behind a click-to-reveal, so ending
 * specifics don't ambush a reader who only wants setting/premise.
 */
export function SpoilerBlock({ paragraphs }: { paragraphs: string[] }) {
  const [shown, setShown] = useState(false);

  return (
    <div className="mt-10 rounded-card border border-warning/30 bg-warning/5 p-5">
      <div className="flex items-center justify-between gap-3">
        <p className="flex items-center gap-2 font-mono text-[11px] uppercase tracking-[0.2em] text-warning">
          {shown ? <Eye size={13} /> : <EyeOff size={13} />} Spoilers
        </p>
        <button
          onClick={() => setShown((v) => !v)}
          className="rounded-md border border-border px-3 py-1.5 text-xs font-medium text-fg-muted transition-colors hover:border-accent hover:text-accent"
        >
          {shown ? "Hide" : "Reveal"}
        </button>
      </div>
      {shown && (
        <div className="mt-4 space-y-4">
          {paragraphs.map((p, i) => (
            <p key={i} className="leading-relaxed text-fg-muted">{p}</p>
          ))}
        </div>
      )}
    </div>
  );
}
