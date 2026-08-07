import Link from "next/link";
import { ArrowRight, BookOpen, Skull, Gem, MessagesSquare, Music, Images, Shield, Heart, Flame, Library, Crown, Download } from "lucide-react";
import { SITE_NAME } from "@/lib/config";
import { Reveal } from "@/components/ui/reveal";
import { LobbyScene } from "@/components/lobby-scene";
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

/**
 * The five functions a civilization needs in its worst hour. Each reads as one sentence —
 * "Shield, still defending a war already won" — because the tragedy is grammatical: every one of
 * them is stuck in the present continuous, doing the right thing past the moment it was right.
 */
const PILLARS = [
  { title: "Shield", icon: Shield, verb: "defending", tail: "a war that was already won" },
  { title: "Hearth", icon: Heart, verb: "healing", tail: "long past the point of death" },
  { title: "Flame", icon: Flame, verb: "hoping", tail: "for a rescue that will not come" },
  { title: "Memory", icon: Library, verb: "remembering", tail: "by imprisoning what it loved" },
  { title: "Crown", icon: Crown, verb: "deciding", tail: "the choice that froze them all" },
] as const;

const TAGS = ["2D", "Co-op", "Souls-like"] as const;

export default function HomePage() {
  return (
    <div>
      {/* ─── Hero: the lobby you actually stand in, tinted to the live accent ─── */}
      <section className="relative overflow-hidden border-b border-border">
        <LobbyScene intensity="hero" priority />
        <span aria-hidden className="pointer-events-none absolute inset-x-0 top-0 h-px bg-gradient-to-r from-transparent via-accent/40 to-transparent" />

        <div className="relative mx-auto w-full max-w-6xl px-5 pb-24 pt-28 sm:px-8 sm:pb-32 sm:pt-36">
          <div className="grid items-center gap-12 lg:grid-cols-[1.35fr_1fr]">
            <div>
              <h1 className="animate-rise-in font-display text-6xl font-extrabold leading-[0.95] tracking-tight text-fg sm:text-7xl lg:text-8xl">
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

              <div className="[&>div]:mt-8 [&>div]:justify-start">
                <HeroCta />
              </div>
            </div>

            {/* A rotating framed field-note — the page's first taste of the world */}
            <Reveal className="relative lg:mt-0">
              <HeroQuote />
            </Reveal>
          </div>
        </div>
      </section>

      {/* ─── The Descent ───────────────────────────────────────────────────────────────────
          The page's one authored moment. Rather than describe a descent in a list, the section
          performs it: a single unbroken shaft runs the full height, the strata hang off it in
          order, and every depth-dependent value (rule opacity, node fill, name weight) is driven
          from one `--d` ratio per row — 0 at the surface, 1 at the core. Going down the page and
          going down the world are the same gesture. */}
      <section className="relative mx-auto max-w-6xl px-5 py-28 sm:px-8">
        <header className="relative max-w-3xl">
          <h2 className="font-display text-4xl font-extrabold leading-[1.05] tracking-tight text-fg sm:text-5xl lg:text-6xl">
            Seven strata,<br />surface to core.
          </h2>
          <p className="mt-6 max-w-xl leading-relaxed text-fg-muted">
            Eldravir fell in layers, and going down is going inward. Each stratum has to be{" "}
            <span className="text-fg">understood</span> before it will let you past — not beaten.
          </p>
        </header>

        <ol className="relative mt-16">
          {/* The shaft. One continuous line the whole column hangs from; it gathers accent as it
              nears the core, so depth is legible before a single word is read. */}
          <span
            aria-hidden
            className="pointer-events-none absolute bottom-0 left-[7px] top-0 w-px sm:left-[11px]"
            style={{
              background:
                "linear-gradient(to bottom, transparent, var(--color-border-strong) 6%, color-mix(in srgb, var(--color-accent) 70%, var(--color-border-strong)) 88%, var(--color-accent))",
            }}
          />

          {STRATA.map((s, i) => {
            const d = i / (STRATA.length - 1); // 0 = surface, 1 = the core
            return (
              <Reveal
                as="li"
                key={s.name}
                delay={i}
                className="group relative flex gap-5 pb-11 last:pb-0 sm:gap-8"
              >
                {/* Depth node. Fills with accent the deeper it sits, and rings on hover. */}
                <span
                  aria-hidden
                  className="relative z-10 mt-[7px] h-[15px] w-[15px] shrink-0 rounded-full border transition-transform duration-300 group-hover:scale-125 sm:h-[23px] sm:w-[23px]"
                  style={{
                    borderColor: `color-mix(in srgb, var(--color-accent) ${20 + d * 80}%, var(--color-border-strong))`,
                    backgroundColor: `color-mix(in srgb, var(--color-accent) ${6 + d * 46}%, var(--color-bg))`,
                  }}
                />

                <div className="min-w-0 flex-1 pt-px lg:grid lg:grid-cols-[minmax(0,22rem)_minmax(0,1fr)] lg:items-baseline lg:gap-10">
                  <div className="flex flex-wrap items-baseline gap-x-4 gap-y-1">
                    <h3
                      className="font-display text-2xl font-bold tracking-tight text-fg transition-colors duration-300 group-hover:text-accent sm:text-3xl"
                      // The deepest strata carry more optical weight, so the column reads heavier
                      // toward the core the way the world does.
                      style={{ opacity: 0.72 + d * 0.28 }}
                    >
                      {s.name}
                    </h3>
                    <span className="font-mono text-[11px] uppercase tracking-[0.2em] text-fg-subtle">
                      {String(i + 1).padStart(2, "0")}
                    </span>
                  </div>
                  <p className="mt-2 max-w-xl leading-relaxed text-fg-muted lg:mt-0 lg:text-lg">{s.note}</p>
                </div>
              </Reveal>
            );
          })}
        </ol>
      </section>

      {/* ─── The Five Pillars ──────────────────────────────────────────────────────────────
          Not five identical cards. Each pillar is one sentence in the present continuous, set as
          an editorial line, because the sentence *is* the tragedy: they never stopped. */}
      <section className="border-y border-border bg-surface/40">
        <div className="mx-auto max-w-6xl px-5 py-28 sm:px-8">
          <header className="max-w-2xl">
            <h2 className="font-display text-4xl font-extrabold leading-[1.05] tracking-tight text-fg sm:text-5xl lg:text-6xl">
              Five heroes.<br />None of them stopped.
            </h2>
            <p className="mt-6 leading-relaxed text-fg-muted">
              They rose in the Void War — the five things a living world <span className="text-fg">is</span>,
              made into persons. Every one of them is still at their post.
            </p>
          </header>

          <ul className="mt-14 divide-y divide-border border-y border-border">
            {PILLARS.map(({ title, icon: Icon, verb, tail }, i) => (
              <Reveal as="li" key={title} delay={i}>
                <div className="group flex items-baseline gap-5 py-7 transition-colors sm:gap-8">
                  <Icon
                    size={20}
                    aria-hidden
                    className="mt-1 shrink-0 self-start text-fg-subtle transition-colors duration-300 group-hover:text-accent"
                  />
                  <p className="min-w-0 flex-1 font-display text-2xl leading-[1.25] tracking-tight text-fg-muted sm:text-3xl lg:text-[2.15rem]">
                    <span className="font-bold text-fg">{title}</span>
                    <span className="text-fg-subtle">, still </span>
                    <span className="font-semibold text-accent">{verb}</span>{" "}
                    <span>{tail}</span>
                    <span className="text-fg-subtle">.</span>
                  </p>
                </div>
              </Reveal>
            ))}
          </ul>

          <Reveal className="mt-12">
            <Link
              href="/story"
              className="group inline-flex items-center gap-2 text-sm font-semibold uppercase tracking-[0.15em] text-fg transition-colors hover:text-accent"
            >
              Read what happened to them
              <ArrowRight size={16} className="transition-transform duration-200 group-hover:translate-x-1" />
            </Link>
          </Reveal>
        </div>
      </section>

      {/* ─── The Archive ───────────────────────────────────────────────────────────────────
          An index, not a card grid — the form a real archive actually takes. Dense, ruled, and
          scannable in one pass. */}
      <section className="mx-auto max-w-6xl px-5 py-28 sm:px-8">
        <header className="max-w-2xl">
          <h2 className="font-display text-4xl font-extrabold leading-[1.05] tracking-tight text-fg sm:text-5xl lg:text-6xl">
            Everything we kept.
          </h2>
          <p className="mt-6 leading-relaxed text-fg-muted">
            The official record of a world that cannot finish dying. One account, seven sections,
            and every word of it cross-linked.
          </p>
        </header>

        <div className="mt-14 grid gap-x-12 border-t border-border sm:grid-cols-2">
          {DESTINATIONS.map(({ href, label, icon: Icon, blurb }, i) => (
            <Reveal key={href} delay={i}>
              <Link
                href={href}
                className="group flex items-start gap-4 border-b border-border py-6 transition-colors duration-200 hover:border-accent/50"
              >
                <Icon
                  size={18}
                  aria-hidden
                  className="mt-1 shrink-0 text-fg-subtle transition-colors duration-300 group-hover:text-accent"
                />
                <span className="min-w-0 flex-1">
                  <span className="flex items-center gap-2">
                    <span className="font-display text-xl font-semibold tracking-tight text-fg transition-colors duration-200 group-hover:text-accent">
                      {label}
                    </span>
                    <ArrowRight
                      size={15}
                      aria-hidden
                      className="-translate-x-1.5 text-accent opacity-0 transition-all duration-300 group-hover:translate-x-0 group-hover:opacity-100"
                    />
                  </span>
                  <span className="mt-1 block text-sm leading-relaxed text-fg-muted">{blurb}</span>
                </span>
              </Link>
            </Reveal>
          ))}
        </div>

        <Reveal className="mt-12">
          <Link
            href="/story/read"
            className="group inline-flex items-center gap-2 text-sm font-semibold uppercase tracking-[0.15em] text-fg transition-colors hover:text-accent"
          >
            <BookOpen size={16} aria-hidden /> Read the manuscript
            <ArrowRight size={16} className="transition-transform duration-200 group-hover:translate-x-1" />
          </Link>
        </Reveal>
      </section>

      {/* ─── Closing CTA — the single, terminal finale ─── */}
      <Reveal as="section" className="mx-auto max-w-6xl px-5 pb-28 sm:px-8">
        <div className="relative overflow-hidden rounded-2xl border border-border bg-surface px-6 py-16 text-center sm:px-12 sm:py-20">
          <LobbyScene intensity="quiet" />
          <span aria-hidden className="pointer-events-none absolute inset-x-0 top-0 h-px bg-gradient-to-r from-transparent via-accent/40 to-transparent" />

          <div className="relative">
            <h2 className="font-display text-4xl font-extrabold tracking-tight text-balance text-fg sm:text-5xl">
              Descend into Eldravir.
            </h2>
            <p className="mx-auto mt-4 max-w-xl leading-relaxed text-fg-muted">
              The world won&rsquo;t end on its own. Take up the leash, read the dead, and see how far
              down you get before the Corruption reads you back.
            </p>

            <div className="mt-9 flex flex-wrap items-center justify-center gap-3">
              <Link
                href="/download"
                className="group inline-flex items-center gap-2.5 rounded-md bg-accent px-7 py-3.5 text-sm font-semibold uppercase tracking-[0.15em] text-accent-fg shadow-[0_0_0_1px_color-mix(in_srgb,var(--color-accent)_45%,transparent)] transition-[filter,box-shadow,transform] duration-200 hover:brightness-105 hover:shadow-[var(--shadow-glow)] active:scale-[0.97]"
              >
                <Download size={17} className="transition-transform duration-200 group-hover:-translate-y-0.5" />
                Download the game
              </Link>
              <Link
                href="/characters"
                className="group inline-flex items-center gap-2 rounded-md border border-border-strong px-6 py-3.5 text-sm font-semibold uppercase tracking-[0.15em] text-fg transition-colors hover:border-accent hover:text-accent"
              >
                Your characters
                <ArrowRight size={16} className="transition-transform duration-200 group-hover:translate-x-1" />
              </Link>
            </div>
            <p className="mt-5 font-mono text-[11px] uppercase tracking-[0.2em] text-fg-subtle">
              Windows · Free · 2-player co-op
            </p>
          </div>
        </div>
      </Reveal>
    </div>
  );
}
