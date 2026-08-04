"use client";

import { useState } from "react";
import { Check, Copy } from "lucide-react";

/**
 * Copy an identifier without putting it on screen.
 *
 * A GUID rendered as text is not information — it is plumbing an admin has to read past. This keeps
 * the value reachable for a bug report or a database query while leaving the interface legible.
 */
export function CopyButton({ value, label, className }: {
  value: string;
  /** What is being copied, for the tooltip and the screen-reader label. */
  label: string;
  className?: string;
}) {
  const [copied, setCopied] = useState(false);

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(value);
      setCopied(true);
      // Long enough to notice, short enough that a second copy still feels responsive.
      setTimeout(() => setCopied(false), 1600);
    } catch {
      // Clipboard access can be refused (insecure origin, permissions). Leave the icon unchanged
      // rather than claiming a copy that didn't happen.
    }
  };

  return (
    <button
      type="button"
      onClick={copy}
      title={copied ? `${label} copied` : `Copy ${label.toLowerCase()}`}
      aria-label={copied ? `${label} copied` : `Copy ${label.toLowerCase()}`}
      className={`inline-flex items-center gap-1 rounded p-1 text-fg-subtle transition-colors hover:bg-surface-2 hover:text-fg focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent ${className ?? ""}`}
    >
      {copied ? <Check size={13} className="text-success" aria-hidden /> : <Copy size={13} aria-hidden />}
    </button>
  );
}

/**
 * A JSON blob behind a disclosure. The rendered view is the answer; this is for when the rendering
 * itself is what's in question.
 */
export function JsonDisclosure({ json, label = "Show JSON" }: { json: string | null | undefined; label?: string }) {
  if (!json) return null;

  let pretty = json;
  try {
    pretty = JSON.stringify(JSON.parse(json), null, 2);
  } catch {
    // Not valid JSON — show it raw rather than hiding it, since that is itself worth seeing.
  }

  return (
    <details className="mt-3 rounded-lg border border-border bg-surface-2/40">
      <summary className="cursor-pointer select-none px-3 py-2 text-xs font-medium text-fg-muted hover:text-fg">
        {label}
      </summary>
      <pre className="max-h-80 overflow-auto border-t border-border px-3 py-2 text-[11px] leading-relaxed text-fg-muted">
        {pretty}
      </pre>
    </details>
  );
}
