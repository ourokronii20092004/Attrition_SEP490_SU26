import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";
import {
  ArrowRight, BookOpen, Skull, Gem, MessagesSquare, Music, Images, ScrollText, Download,
} from "lucide-react";
import { PageShell } from "@/components/ui/page-shell";
import { Reveal } from "@/components/ui/reveal";
import { LobbyScene } from "@/components/lobby-scene";
import { SITE_NAME } from "@/lib/config";
import { TEAM } from "@/lib/team";
import phroneticFull from "@/content/brand/phronetic-full.png";

export const metadata: Metadata = {
  title: "About",
  description:
    "About Attrition — a 2D co-op souls-like ARPG, the archive of its dying world, and the four-person studio that built it.",
};

const ARCHIVE = [
  { href: "/story", icon: ScrollText, label: "The Story", blurb: "The full lore of Eldravir — characters, world, and the manuscript." },
  { href: "/wiki", icon: BookOpen, label: "Wiki", blurb: "Mechanics, systems, and the canon of a dying world." },
  { href: "/bestiary", icon: Skull, label: "Bestiary", blurb: "Every horror the Corruption animates, cataloged." },
  { href: "/items", icon: Gem, label: "Items", blurb: "Loot, gear, and what bleeds to drop it." },
  { href: "/music", icon: Music, label: "Music", blurb: "The full atmospheric soundtrack." },
  { href: "/gallery", icon: Images, label: "Gallery", blurb: "Concept art and fragments of the world." },
  { href: "/forum", icon: MessagesSquare, label: "Forum", blurb: "Strategies, theories, and co-op companions." },
] as const;

export default function AboutPage() {
  return (
    <div>
      {/* ─── Opening statement over the world it describes ─── */}
      <section className="relative overflow-hidden border-b border-border">
        <LobbyScene intensity="hero" priority />
        <span aria-hidden className="pointer-events-none absolute inset-x-0 top-0 h-px bg-gradient-to-r from-transparent via-accent/40 to-transparent" />

        <div className="relative mx-auto w-full max-w-5xl px-5 pb-24 pt-28 sm:px-8 sm:pb-28 sm:pt-32">
          <h1 className="animate-rise-in max-w-3xl font-display text-5xl font-extrabold leading-[0.98] tracking-tight text-balance text-fg sm:text-6xl lg:text-7xl">
            A dying world, built for two.
          </h1>
          <p className="animate-rise-in mt-7 max-w-2xl text-lg leading-relaxed text-fg-muted [animation-delay:200ms]">
            {SITE_NAME} is a dark-fantasy 2D souls-like made for co-op. You wake as Ren, an amnesiac
            soul bound to the god Iris, and descend through a world held forever in the moment of
            its death — to read what killed it before it is allowed to end.
          </p>
        </div>
      </section>

      <PageShell size="lg">
        {/* ─── What the game is. Prose, not three identical cards: each claim gets the room it
             needs and the reader gets an argument instead of a grid. ─── */}
        <Reveal as="section">
          <h2 className="font-display text-sm font-semibold uppercase tracking-[0.25em] text-fg-muted">The Game</h2>
          <div className="mt-8 space-y-10 border-l border-border pl-6 sm:pl-8">
            <div>
              <h3 className="font-display text-2xl font-bold tracking-tight text-fg">Punishing, and meant for a friend</h3>
              <p className="mt-3 max-w-2xl leading-relaxed text-fg-muted">
                Combat is deliberate: positioning, patience and reading a tell beat mashing an
                attack button. The whole campaign runs in two-player co-op, and it was designed that
                way from the first prototype rather than patched in afterwards.
              </p>
            </div>
            <div>
              <h3 className="font-display text-2xl font-bold tracking-tight text-fg">A world that interlocks</h3>
              <p className="mt-3 max-w-2xl leading-relaxed text-fg-muted">
                Seven strata fall away surface to core, joined by hidden routes, fast travel and
                bosses that gate the way down. Descending is the plot: each layer has to be
                understood before it will let you past.
              </p>
            </div>
            <div>
              <h3 className="font-display text-2xl font-bold tracking-tight text-fg">The Corruption never attacks</h3>
              <p className="mt-3 max-w-2xl leading-relaxed text-fg-muted">
                It only offers. A parasitic magic that ends suffering by ending the capacity to
                suffer, it is the reason a dead world cannot finish dying — and the real fight is
                against the numbness it sells as relief.
              </p>
            </div>
          </div>
        </Reveal>

        {/* ─── The team — the portraits are the section ─── */}
        <Reveal as="section" className="mt-24">
          <div className="flex flex-wrap items-end justify-between gap-4 border-b border-border pb-4">
            <h2 className="font-display text-sm font-semibold uppercase tracking-[0.25em] text-fg-muted">The Team</h2>
            <p className="font-mono text-xs uppercase tracking-[0.2em] text-fg-subtle">
              Four people · FPT University · SEP490 SU2026
            </p>
          </div>

          <div className="mt-8 grid gap-5 sm:grid-cols-2">
            {TEAM.map((m, i) => (
              <Reveal key={m.name} delay={i}>
                <article className="group relative h-full overflow-hidden rounded-card border border-border bg-surface">
                  <div className="relative aspect-[4/5] overflow-hidden sm:aspect-[3/4]">
                    <Image
                      src={m.photo}
                      alt={`Portrait of ${m.name}`}
                      placeholder="blur"
                      sizes="(min-width: 640px) 24rem, 100vw"
                      className="h-full w-full object-cover transition-transform duration-[900ms] ease-[cubic-bezier(0.16,1,0.3,1)] group-hover:scale-[1.04]"
                    />
                    {/* The portraits are shot on a deep crimson backdrop, so names sit on a
                        bottom-anchored scrim rather than fighting the image for contrast. */}
                    <div
                      aria-hidden
                      className="absolute inset-0"
                      style={{ background: "linear-gradient(to top, rgb(0 0 0 / 0.92) 0%, rgb(0 0 0 / 0.55) 32%, transparent 62%)" }}
                    />
                    <div className="absolute inset-x-0 bottom-0 p-5 sm:p-6">
                      <h3 className="font-display text-xl font-bold tracking-tight text-white sm:text-2xl">{m.name}</h3>
                      <p className="mt-1 text-sm font-medium text-white/75">{m.role}</p>
                    </div>
                  </div>
                </article>
              </Reveal>
            ))}
          </div>
        </Reveal>

        {/* ─── The studio ─── */}
        <Reveal as="section" className="mt-24">
          <div className="relative overflow-hidden rounded-2xl border border-border bg-surface px-6 py-12 sm:px-12 sm:py-16">
            <span aria-hidden className="pointer-events-none absolute -right-20 -top-20 h-64 w-64 rounded-full bg-accent/10 blur-[110px]" />
            <div className="relative flex flex-col items-center gap-8 text-center sm:flex-row sm:gap-12 sm:text-left">
              {/* The lockup's wordmark is near-black, which disappears on the dark theme.
                  `logo-invert-on-dark` is keyed off [data-theme] in globals.css, since this project
                  does not use Tailwind's class-based `dark:` variant. */}
              <Image
                src={phroneticFull}
                alt="Phronetic Studio"
                sizes="200px"
                priority
                className="logo-invert-on-dark h-auto w-40 shrink-0 sm:w-48"
              />
              <div>
                <h2 className="font-display text-2xl font-bold tracking-tight text-fg sm:text-3xl">Phronetic Studio</h2>
                <p className="mt-3 max-w-xl leading-relaxed text-fg-muted">
                  Attrition is our graduation project — the game, this archive, and the services
                  behind both, built end to end by the four of us. Everything you can read here is
                  the same world the game ships with, kept in one place so it survives the semester.
                </p>
              </div>
            </div>
          </div>
        </Reveal>

        {/* ─── The archive ─── */}
        <Reveal as="section" className="mt-24">
          <div className="border-b border-border pb-4">
            <h2 className="font-display text-sm font-semibold uppercase tracking-[0.25em] text-fg-muted">The Archive</h2>
            <p className="mt-2 max-w-2xl text-sm leading-relaxed text-fg-muted">
              The official record of that world — everything below lives under a single account.
            </p>
          </div>
          <div className="mt-8 grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {ARCHIVE.map(({ href, icon: Icon, label, blurb }, i) => (
              <Reveal key={href} delay={i}>
                <Link
                  href={href}
                  className="group flex h-full items-start gap-3 rounded-card border border-border bg-surface p-5 transition-[border-color,transform] duration-300 hover:-translate-y-1 hover:border-accent/60"
                >
                  <span className="mt-0.5 inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-md border border-border bg-surface-2 text-fg-subtle transition-colors group-hover:border-accent/40 group-hover:text-accent">
                    <Icon size={16} />
                  </span>
                  <span className="min-w-0">
                    <span className="flex items-center gap-1.5 font-display font-semibold text-fg transition-colors group-hover:text-accent">
                      {label}
                      <ArrowRight size={13} className="-translate-x-1 opacity-0 transition-all group-hover:translate-x-0 group-hover:opacity-100" />
                    </span>
                    <span className="mt-1 block text-sm leading-relaxed text-fg-muted">{blurb}</span>
                  </span>
                </Link>
              </Reveal>
            ))}
          </div>
        </Reveal>

        {/* ─── Close ─── */}
        <Reveal as="section" className="mt-24">
          <div className="rounded-2xl border border-border bg-surface-2/40 px-6 py-12 text-center sm:py-14">
            <h2 className="font-display text-3xl font-extrabold tracking-tight text-balance text-fg sm:text-4xl">
              It runs on Windows, and it&rsquo;s free.
            </h2>
            <div className="mt-8 flex flex-wrap items-center justify-center gap-3">
              <Link
                href="/download"
                className="group inline-flex items-center gap-2.5 rounded-md bg-accent px-7 py-3.5 text-sm font-semibold uppercase tracking-[0.15em] text-accent-fg shadow-[0_0_0_1px_color-mix(in_srgb,var(--color-accent)_45%,transparent)] transition-[filter,box-shadow,transform] duration-200 hover:brightness-105 hover:shadow-[var(--shadow-glow)] active:scale-[0.97]"
              >
                <Download size={17} className="transition-transform duration-200 group-hover:-translate-y-0.5" />
                Download the game
              </Link>
              <Link
                href="/story"
                className="group inline-flex items-center gap-2 rounded-md border border-border-strong px-6 py-3.5 text-sm font-semibold uppercase tracking-[0.15em] text-fg transition-colors hover:border-accent hover:text-accent"
              >
                Read the story
                <ArrowRight size={16} className="transition-transform duration-200 group-hover:translate-x-1" />
              </Link>
            </div>
          </div>
        </Reveal>

        <div className="mt-16 flex flex-wrap items-center gap-3 border-t border-border pt-6 text-sm">
          <Link href="/privacy" className="text-accent transition-opacity hover:opacity-80">Privacy Policy</Link>
          <span className="text-fg-subtle">·</span>
          <Link href="/terms" className="text-accent transition-opacity hover:opacity-80">Terms of Service</Link>
        </div>
      </PageShell>
    </div>
  );
}
