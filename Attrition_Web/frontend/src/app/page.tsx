import Link from "next/link";
import { ArrowRight, BookOpen, Skull, MessagesSquare, Music, Images } from "lucide-react";
import { SITE_NAME } from "@/lib/config";

const DESTINATIONS = [
  { href: "/wiki", label: "Wiki", icon: BookOpen, blurb: "Lore, mechanics, and the canon of a dying world." },
  { href: "/bestiary", label: "Bestiary", icon: Skull, blurb: "Every horror the Corruption animates, cataloged." },
  { href: "/forum", label: "Forum", icon: MessagesSquare, blurb: "Strategies, theories, and co-op companions." },
  { href: "/music", label: "Music", icon: Music, blurb: "The full atmospheric soundtrack." },
  { href: "/gallery", label: "Gallery", icon: Images, blurb: "Concept art and fragments of the world." },
] as const;

export default function HomePage() {
  return (
    <div>
      {/* Hero */}
      <section className="relative overflow-hidden">
        {/* Corruption glow behind the title */}
        <span aria-hidden className="pointer-events-none absolute left-1/2 top-1/3 h-[36rem] w-[36rem] -translate-x-1/2 -translate-y-1/2 rounded-full bg-accent/10 blur-[120px]" />
        <span aria-hidden className="pointer-events-none absolute inset-x-0 top-0 h-px bg-gradient-to-r from-transparent via-accent/40 to-transparent" />

        <div className="mx-auto max-w-5xl px-5 pb-24 pt-28 text-center sm:px-8 sm:pb-32 sm:pt-36">
          <p className="animate-rise-in text-xs font-medium uppercase tracking-[0.4em] text-accent [animation-delay:0ms]">
            Companion Archive
          </p>
          <h1 className="reveal-lines mt-6 font-display text-5xl font-extrabold leading-[1.02] tracking-tight text-balance sm:text-7xl lg:text-8xl">
            <span className="block" style={{ "--i": 0 } as React.CSSProperties}>Everything dies.</span>
            <span className="sheen block" style={{ "--i": 1 } as React.CSSProperties}>Nothing is forgotten.</span>
          </h1>
          <p className="animate-rise-in mx-auto mt-7 max-w-xl text-lg leading-relaxed text-fg-muted [animation-delay:450ms]">
            The official record of {SITE_NAME} — a 2D co-op souls-like. Lore, enemies,
            and the people who survive them, kept in one place while the world rots.
          </p>
          <div className="animate-rise-in mt-10 flex flex-wrap items-center justify-center gap-4 [animation-delay:600ms]">
            <Link
              href="/wiki"
              className="group inline-flex items-center gap-2 rounded-md bg-accent px-7 py-3.5 text-sm font-semibold uppercase tracking-[0.15em] text-accent-fg animate-pulse-glow transition-[transform,filter] duration-200 hover:brightness-105 active:scale-[0.97]"
            >
              Enter the Archive
              <ArrowRight size={17} className="transition-transform duration-200 group-hover:translate-x-1" />
            </Link>
            <Link
              href="/forum"
              className="inline-flex items-center rounded-md border border-border-strong px-7 py-3.5 text-sm font-semibold uppercase tracking-[0.15em] text-fg transition-colors duration-200 hover:border-accent hover:text-accent active:scale-[0.97]"
            >
              Find a Partner
            </Link>
          </div>
        </div>
      </section>

      {/* Destinations */}
      <section className="mx-auto max-w-6xl px-5 pb-28 sm:px-8">
        <div className="mb-8 flex items-end justify-between border-b border-border pb-4">
          <h2 className="font-display text-sm font-semibold uppercase tracking-[0.25em] text-fg-muted">
            Explore
          </h2>
          <span className="text-xs uppercase tracking-[0.2em] text-fg-subtle">05 entries</span>
        </div>
        <div className="stagger grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {DESTINATIONS.map(({ href, label, icon: Icon, blurb }, i) => (
            <Link
              key={href}
              href={href}
              style={{ "--i": i } as React.CSSProperties}
              className="group relative overflow-hidden rounded-card border border-border bg-surface p-6 transition-[transform,border-color,box-shadow] duration-300 ease-[cubic-bezier(0.16,1,0.3,1)] hover:-translate-y-1.5 hover:border-accent/60 hover:shadow-[var(--shadow-glow)]"
            >
              <span aria-hidden className="pointer-events-none absolute -right-8 -top-8 h-28 w-28 rounded-full bg-accent/10 opacity-0 blur-2xl transition-opacity duration-500 group-hover:opacity-100" />
              <span className="mb-12 inline-flex h-11 w-11 items-center justify-center rounded-md border border-border bg-surface-2 text-fg-subtle transition-colors duration-300 group-hover:border-accent/40 group-hover:text-accent">
                <Icon size={20} />
              </span>
              <div className="flex items-center justify-between">
                <h3 className="font-display text-xl font-semibold text-fg transition-colors group-hover:text-accent">
                  {label}
                </h3>
                <ArrowRight size={18} className="-translate-x-2 text-accent opacity-0 transition-all duration-300 group-hover:translate-x-0 group-hover:opacity-100" />
              </div>
              <p className="mt-1.5 text-sm leading-relaxed text-fg-muted">{blurb}</p>
            </Link>
          ))}
        </div>
      </section>
    </div>
  );
}
