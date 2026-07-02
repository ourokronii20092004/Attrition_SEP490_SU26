"use client";

import { useEffect, useRef, useState } from "react";
import { ChevronLeft, ChevronRight } from "lucide-react";

// Short, self-contained lines from the manuscript — chosen to stand alone out of context.
const QUOTES: { line: string; attribution: string }[] = [
  {
    line: "The trouble most people have isn't courage. It's that they spend all their strength arguing with the situation instead of solving it.",
    attribution: "Iris · the square",
  },
  {
    line: "I've found that lying about clocks is the single cruelest thing you can do to a person who's standing on one.",
    attribution: "Iris · the cistern",
  },
  {
    line: "Pain and fear and dying are what living costs, they're the price of the thing itself.",
    attribution: "Ren · the ward",
  },
  {
    line: "A kindness built on a lie isn't kindness. It's a slower cruelty with better manners.",
    attribution: "Ren · the cathedral",
  },
  {
    line: "Don't freeze. A self assembled out of rules, in a place that wanted him to have none.",
    attribution: "Ren · the descent",
  },
  {
    line: "You take the false hope and you leave the true one standing.",
    attribution: "Ren · the cathedral",
  },
  {
    line: "Memory moves. Memory is a living thing carrying a dead one forward... A thing that can't leave you was never really with you in the first place. It's just held.",
    attribution: "Ren · the archive",
  },
  {
    line: "To choose an ending is not to lose.",
    attribution: "Iris · the throne",
  },
  {
    line: "You are allowed to have failed at the impossible thing and to have done the real thing anyway.",
    attribution: "Maren · the throne",
  },
  {
    line: "A yes from someone who didn't want the other thing is just a man with nowhere to go, dressed up as a hero.",
    attribution: "Iris · the throne",
  },
  {
    line: "You carry out the ones you can and the grief of the ones you can't, and you do not call the grief a reason to stop.",
    attribution: "Narrator · the breach",
  }
];

/**
 * Rotating hero field-note. Fixed-height frame (no layout shift as quotes change), a single
 * cross-fade between lines, prev/next controls, and auto-advance that pauses on hover/focus.
 */
export function HeroQuote() {
  const [i, setI] = useState(0);
  const [paused, setPaused] = useState(false);
  const timer = useRef<ReturnType<typeof setInterval> | null>(null);

  useEffect(() => {
    if (paused) return;
    timer.current = setInterval(() => setI((v) => (v + 1) % QUOTES.length), 7000);
    return () => { if (timer.current) clearInterval(timer.current); };
  }, [paused]);

  const go = (dir: 1 | -1) => setI((v) => (v + dir + QUOTES.length) % QUOTES.length);

  return (
    <figure
      className="relative flex h-[20rem] flex-col overflow-hidden rounded-card border border-border bg-surface/60 p-7"
      onMouseEnter={() => setPaused(true)}
      onMouseLeave={() => setPaused(false)}
      onFocusCapture={() => setPaused(true)}
      onBlurCapture={() => setPaused(false)}
    >
      <span aria-hidden className="pointer-events-none absolute -left-4 -top-6 select-none font-display text-7xl leading-none text-accent/40">
        &ldquo;
      </span>

      {/* Crossfade stack: every quote is layered absolutely, only the active one is opaque.
          Because they share the same box, the frame never resizes between quotes. */}
      <div className="relative flex-1">
        {QUOTES.map((q, idx) => (
          <blockquote
            key={idx}
            aria-hidden={idx !== i}
            className={`absolute inset-0 flex items-center font-display text-lg font-medium leading-snug text-balance text-fg transition-opacity duration-700 ease-out sm:text-xl ${
              idx === i ? "opacity-100" : "pointer-events-none opacity-0"
            }`}
          >
            {q.line}
          </blockquote>
        ))}
      </div>

      <figcaption className="mt-4 flex items-center justify-between gap-3">
        <span className="flex min-w-0 items-center gap-3 font-mono text-[11px] uppercase tracking-[0.2em] text-fg-subtle">
          <span aria-hidden className="h-px w-6 shrink-0 bg-accent/60" />
          <span className="truncate">{QUOTES[i].attribution}</span>
        </span>
        <span className="flex shrink-0 items-center gap-1">
          <button onClick={() => go(-1)} aria-label="Previous quote" className="rounded-md p-1.5 text-fg-subtle transition-colors hover:bg-surface-2 hover:text-accent">
            <ChevronLeft size={16} />
          </button>
          <button onClick={() => go(1)} aria-label="Next quote" className="rounded-md p-1.5 text-fg-subtle transition-colors hover:bg-surface-2 hover:text-accent">
            <ChevronRight size={16} />
          </button>
        </span>
      </figcaption>

      {/* Progress dots */}
      <div className="mt-3 flex items-center gap-1.5" role="tablist" aria-label="Quote selector">
        {QUOTES.map((_, idx) => (
          <button
            key={idx}
            role="tab"
            aria-selected={idx === i}
            aria-label={`Quote ${idx + 1}`}
            onClick={() => setI(idx)}
            className={`h-1.5 rounded-full transition-all ${idx === i ? "w-5 bg-accent" : "w-1.5 bg-border-strong hover:bg-fg-subtle"}`}
          />
        ))}
      </div>
    </figure>
  );
}
