import Link from "next/link";
import { ArrowRight, BookOpen, Skull, Gem, MessagesSquare, Music, Images, Shield, Heart, Flame, Library, Crown, ScrollText } from "lucide-react";
import { SITE_NAME } from "@/lib/config";
import { Reveal } from "@/components/ui/reveal";
import { HeroCta } from "./hero-cta";
import { HeroQuote } from "./hero-quote";

const DESTINATIONS = [
  { href: "/wiki", label: "Wiki", icon: BookOpen, blurb: "Lore, mechanics, and the canon of a dying world." },
  { href: "/bestiary", label: "Bestiary", icon: Skull, blurb: "Every horror the Corruption animates, cataloged." },
  { href: "/items", label: "Items", icon: Gem, blurb: "Loot, gear, and what bleeds to drop it." },
  { href: "/forum", label: "Forum", icon: MessagesSquare, blurb: "Strategies, theories, and co-op companions." },
  { href: "/music", label: "Music", icon: Music, blurb: "The full atmospheric soundtrack." },
  { href: "/gallery", label: "Gallery", icon: Images, blurb: "Concept art and fragments of the world." },
] as const;

// The seven strata of Eldravir, surface to core — the shape of Ren's descent.
const STRATA = [
  { name: "The Square", note: "Where you wake. The frozen fleeing dead." },
  { name: "The Cistern", note: "A broken reservoir. You learn the leash." },
  { name: "The Wall", note: "A parade ground fighting a won war." },
  { name: "The Ward", note: "A hospital where no one is allowed to die." },
  { name: "The Cathedral", note: "The joyful dead, waiting for a rescue." },
  { name: "The Archive", note: "A vault of kept souls." },
  { name: "The Throne", note: "Where the world died, and keeps dying." },
] as const;

// The five functions a civilization needs in its worst hour, each frozen mid-function.
const PILLARS = [
  { title: "Shield", fn: "defend", icon: Shield, frozen: "fighting a war already won" },
  { title: "Hearth", fn: "heal", icon: Heart, frozen: "healing past death" },
  { title: "Flame", fn: "hope", icon: Flame, frozen: "awaiting a rescue that won't come" },
  { title: "Memory", fn: "remember", icon: Library, frozen: "remembering by imprisoning" },
  { title: "Crown", fn: "decide", icon: Crown, frozen: "the choice that froze them all" },
] as const;

const TAGS = ["2D", "Co-op", "Souls-like"] as const;

export default function HomePage() {
  return (
    <div>
      {/* ─── Hero ─── */}
      <section className="relative overflow-hidden border-b border-border">
        <span aria-hidden className="pointer-events-none absolute -right-20 top-0 h-[40rem] w-[40rem] rounded-full bg-accent/10 blur-[130px]" />
        <span aria-hidden className="pointer-events-none absolute inset-x-0 top-0 h-px bg-gradient-to-r from-transparent via-accent/40 to-transparent" />

        <div className="mx-auto w-full max-w-6xl px-5 pb-20 pt-24 sm:px-8 sm:pb-28 sm:pt-32">
          <div className="grid items-center gap-12 lg:grid-cols-[1.35fr_1fr]">
            {/* Left: wordmark + premise + actions */}
            <div>
              <p className="animate-rise-in flex items-center gap-3 font-mono text-[11px] uppercase tracking-[0.35em] text-accent">
                <span aria-hidden className="h-px w-6 bg-accent/60" /> Companion Archive
              </p>
              <h1 className="animate-rise-in mt-5 font-display text-6xl font-extrabold leading-[0.95] tracking-tight text-fg [animation-delay:80ms] sm:text-7xl lg:text-8xl">
                {SITE_NAME}
              </h1>
              <p className="animate-rise-in mt-6 max-w-xl text-lg leading-relaxed text-fg-muted [animation-delay:240ms]">
                A 2D co-op souls-like. A living man, last of a dead world, is sent down through the
                strata of its failure — to read what killed it before it is allowed to end.
              </p>

              <ul className="animate-rise-in mt-6 flex flex-wrap items-center gap-x-3 gap-y-2 font-mono text-[11px] uppercase tracking-[0.2em] text-fg-subtle [animation-delay:360ms]">
                {TAGS.map((t, i) => (
                  <li key={t} className="flex items-center gap-3">
                    {i > 0 && <span aria-hidden className="h-1 w-1 rounded-full bg-accent/50" />}
                    {t}
                  </li>
                ))}
              </ul>

              <div className="[&>div]:justify-start [&>div]:mt-8">
                <HeroCta />
              </div>
            </div>

            {/* Right: a rotating framed field-note — the page's first taste of the world */}
            <Reveal className="relative lg:mt-0">
              <HeroQuote />
            </Reveal>
          </div>
        </div>
      </section>

      {/* ─── The Descent (strata) ─── */}
      <Reveal as="section" className="mx-auto max-w-6xl px-5 py-24 sm:px-8">
        <div className="mb-8 border-b border-border pb-4">
          <h2 className="font-display text-sm font-semibold uppercase tracking-[0.25em] text-fg-muted">The Descent</h2>
          <p className="mt-2 max-w-2xl text-sm leading-relaxed text-fg-muted">
            Eldravir falls away in layers, surface to core. Going down is going inward — each
            stratum must be <span className="text-fg">understood</span> to pass, not beaten.
          </p>
        </div>
        <ol className="stagger relative space-y-px">
          {STRATA.map((s, i) => (
            <li
              key={s.name}
              style={{ "--i": i } as React.CSSProperties}
              className="group flex items-baseline gap-4 rounded-md border border-transparent px-4 py-3 transition-colors hover:border-border hover:bg-surface sm:gap-6"
            >
              <span className="font-mono text-xs tabular-nums text-fg-subtle transition-colors group-hover:text-accent">
                {String(i + 1).padStart(2, "0")}
              </span>
              <span className="flex min-w-0 flex-1 flex-col gap-0.5 sm:flex-row sm:items-baseline sm:gap-4">
                <span className="font-display text-lg font-semibold text-fg transition-colors group-hover:text-accent sm:w-44 sm:shrink-0">
                  {s.name}
                </span>
                <span className="text-sm leading-relaxed text-fg-muted">{s.note}</span>
              </span>
            </li>
          ))}
        </ol>
      </Reveal>

      {/* ─── The Five Pillars ─── */}
      <Reveal as="section" className="mx-auto max-w-6xl px-5 py-24 sm:px-8">
        <div className="mb-8 border-b border-border pb-4">
          <h2 className="font-display text-sm font-semibold uppercase tracking-[0.25em] text-fg-muted">The Five Pillars</h2>
          <p className="mt-2 max-w-2xl text-sm leading-relaxed text-fg-muted">
            Five heroes rose in the Void War — the five things a living world <span className="text-fg">is</span>,
            made into persons. Each now frozen mid-function, doing the right thing past the moment it was right.
          </p>
        </div>
        <div className="stagger grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5">
          {PILLARS.map(({ title, fn, icon: Icon, frozen }, i) => (
            <div
              key={title}
              style={{ "--i": i } as React.CSSProperties}
              className="group relative overflow-hidden rounded-card border border-border bg-surface p-5 transition-[border-color,transform] duration-300 hover:-translate-y-1 hover:border-accent/50"
            >
              <span aria-hidden className="pointer-events-none absolute -right-6 -top-6 h-20 w-20 rounded-full bg-accent/10 opacity-0 blur-2xl transition-opacity duration-500 group-hover:opacity-100" />
              <Icon size={22} className="text-fg-subtle transition-colors duration-300 group-hover:text-accent" />
              <h3 className="mt-4 font-display text-lg font-semibold text-fg">{title}</h3>
              <p className="mt-0.5 font-mono text-[11px] uppercase tracking-[0.2em] text-accent">{fn}</p>
              <p className="mt-3 text-xs leading-relaxed text-fg-muted">Frozen {frozen}.</p>
            </div>
          ))}
        </div>
        <div className="mt-8 text-center">
          <Link href="/story" className="group inline-flex items-center gap-2 text-sm font-medium text-fg-muted transition-colors hover:text-accent">
            Read the full story <ArrowRight size={15} className="transition-transform duration-200 group-hover:translate-x-1" />
          </Link>
        </div>
      </Reveal>

      {/* ─── The Archive (destinations) ─── */}
      <Reveal as="section" className="mx-auto max-w-6xl px-5 py-24 sm:px-8">
        <div className="mb-8 flex items-end justify-between border-b border-border pb-4">
          <h2 className="font-display text-sm font-semibold uppercase tracking-[0.25em] text-fg-muted">The Archive</h2>
          <span className="font-mono text-xs uppercase tracking-[0.2em] text-fg-subtle">
            {String(DESTINATIONS.length).padStart(2, "0")} sections
          </span>
        </div>

        <div className="stagger grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {DESTINATIONS.map(({ href, label, icon: Icon, blurb }, i) => (
            <Link
              key={href}
              href={href}
              style={{ "--i": i } as React.CSSProperties}
              className="group relative overflow-hidden rounded-card border border-border bg-surface p-6 transition-[transform,border-color,box-shadow] duration-300 ease-[cubic-bezier(0.16,1,0.3,1)] hover:-translate-y-1.5 hover:border-accent/60 hover:shadow-[var(--shadow-glow)]"
            >
              <span aria-hidden className="pointer-events-none absolute bottom-6 left-0 top-6 w-px origin-bottom scale-y-0 bg-accent transition-transform duration-300 ease-[cubic-bezier(0.16,1,0.3,1)] group-hover:scale-y-100" />
              <span aria-hidden className="pointer-events-none absolute -right-8 -top-8 h-28 w-28 rounded-full bg-accent/10 opacity-0 blur-2xl transition-opacity duration-500 group-hover:opacity-100" />

              <div className="flex items-center justify-between">
                <span className="font-mono text-xs tracking-[0.2em] text-fg-subtle transition-colors duration-300 group-hover:text-accent">
                  {String(i + 1).padStart(2, "0")}
                </span>
                <ArrowRight size={18} className="-translate-x-2 text-accent opacity-0 transition-all duration-300 group-hover:translate-x-0 group-hover:opacity-100" />
              </div>

              <span className="mt-8 mb-4 inline-flex h-11 w-11 items-center justify-center rounded-md border border-border bg-surface-2 text-fg-subtle transition-colors duration-300 group-hover:border-accent/40 group-hover:text-accent">
                <Icon size={20} />
              </span>

              <h3 className="font-display text-xl font-semibold text-fg transition-colors group-hover:text-accent">{label}</h3>
              <p className="mt-1.5 text-sm leading-relaxed text-fg-muted">{blurb}</p>
            </Link>
          ))}
        </div>
      </Reveal>

      {/* ─── Closing CTA ─── */}
      {/* ─── Closing: descent CTA + Enter the Story ─── */}
      <Reveal as="section" className="mx-auto flex max-w-6xl flex-col gap-6 px-5 py-24 sm:px-8">
        <div className="relative overflow-hidden rounded-card border border-border bg-surface px-6 py-12 text-center sm:px-12 sm:py-14">
          <span aria-hidden className="pointer-events-none absolute left-1/2 top-0 h-64 w-64 -translate-x-1/2 -translate-y-1/2 rounded-full bg-accent/10 blur-[100px]" />
          <span aria-hidden className="pointer-events-none absolute inset-x-0 bottom-0 h-px bg-gradient-to-r from-transparent via-accent/30 to-transparent" />

          <p className="font-mono text-[11px] uppercase tracking-[0.3em] text-accent">What do you do with a world that won&rsquo;t die?</p>
          <h2 className="mt-4 font-display text-3xl font-bold tracking-tight text-balance text-fg sm:text-4xl">
            Track your own descent.
          </h2>
          <p className="mx-auto mt-4 max-w-xl leading-relaxed text-fg-muted">
            Sign in to follow your characters across runs — their progression, their inventory, and
            the moment each one fell to the Corruption.
          </p>
          <div className="mt-8 flex flex-wrap items-center justify-center gap-3">
            <Link
              href="/characters"
              className="group inline-flex items-center gap-2 rounded-md bg-accent px-6 py-3 text-sm font-semibold uppercase tracking-[0.15em] text-accent-fg transition-[filter,box-shadow] hover:brightness-105 hover:shadow-[var(--shadow-glow)]"
            >
              Your characters
              <ArrowRight size={16} className="transition-transform duration-200 group-hover:translate-x-1" />
            </Link>
            <Link
              href="/story"
              className="inline-flex items-center gap-2 rounded-md border border-border-strong px-6 py-3 text-sm font-semibold uppercase tracking-[0.15em] text-fg transition-colors hover:border-accent hover:text-accent"
            >
              Read the lore
            </Link>
          </div>
        </div>

        <Link
          href="/story"
          className="group relative flex flex-col items-start gap-6 overflow-hidden rounded-card border border-border bg-surface p-8 transition-[border-color,box-shadow] duration-300 hover:border-accent/60 hover:shadow-[var(--shadow-glow)] sm:flex-row sm:items-center sm:justify-between sm:p-10"
        >
          <span aria-hidden className="pointer-events-none absolute -left-10 -top-10 h-40 w-40 rounded-full bg-accent/10 opacity-0 blur-[80px] transition-opacity duration-500 group-hover:opacity-100" />
          <div className="relative flex items-start gap-5">
            <span className="mt-1 inline-flex h-12 w-12 shrink-0 items-center justify-center rounded-md border border-border bg-surface-2 text-fg-subtle transition-colors duration-300 group-hover:border-accent/40 group-hover:text-accent">
              <ScrollText size={22} />
            </span>
            <div>
              <p className="font-mono text-[11px] uppercase tracking-[0.3em] text-accent">The Lore of Eldravir</p>
              <h2 className="mt-2 font-display text-2xl font-bold tracking-tight text-fg sm:text-3xl">Enter the Story</h2>
              <p className="mt-2 max-w-xl text-sm leading-relaxed text-fg-muted">
                The dead world, its five fallen pillars, the god who guards the breach, and the
                living man sent down to read it all — fully cross-linked.
              </p>
            </div>
          </div>
          <div className="relative flex shrink-0 flex-col gap-3 sm:flex-row sm:items-center">
            <span className="inline-flex items-center justify-center gap-2 rounded-md bg-accent px-6 py-3 text-sm font-semibold uppercase tracking-[0.15em] text-accent-fg transition-[filter] duration-300 group-hover:brightness-105">
              Explore <ArrowRight size={16} className="transition-transform duration-200 group-hover:translate-x-1" />
            </span>
          </div>
        </Link>
        <div className="text-center sm:text-right">
          <Link href="/story/read" className="group inline-flex items-center gap-2 text-sm font-medium text-fg-muted transition-colors hover:text-accent">
            <BookOpen size={15} /> Or read the full manuscript — 17 chapters
            <ArrowRight size={14} className="transition-transform duration-200 group-hover:translate-x-1" />
          </Link>
        </div>
      </Reveal>
    </div>
  );
}
