import type { Metadata } from "next";
import Link from "next/link";
import {
  ArrowRight, BookOpen, Skull, Gem, MessagesSquare, Music, Images, ScrollText, Users2, Gamepad2, Swords, Sparkles,
} from "lucide-react";
import { PageShell } from "@/components/ui/page-shell";
import { Card } from "@/components/ui/card";
import { Reveal } from "@/components/ui/reveal";
import { SITE_NAME } from "@/lib/config";

export const metadata: Metadata = {
  title: "About",
  description: "About Attrition — a 2D co-op souls-like ARPG and the official archive of its dying world.",
};

const PILLARS = [
  { icon: Swords, title: "Co-op souls-like", body: "Built from the ground up for two-player co-op. Punishing, deliberate combat where positioning and patience beat button-mashing." },
  { icon: Gamepad2, title: "Interconnected world", body: "Descend through the strata of a dead world — interlocking maps, hidden routes, and bosses that gate the way down." },
  { icon: Sparkles, title: "Corruption", body: "A parasitic magic that never attacks, only offers. The real fight is against the numbness it sells as relief." },
];

const ARCHIVE = [
  { href: "/story", icon: ScrollText, label: "The Story", blurb: "The full lore of Eldravir — characters, world, and the manuscript." },
  { href: "/wiki", icon: BookOpen, label: "Wiki", blurb: "Mechanics, systems, and the canon of a dying world." },
  { href: "/bestiary", icon: Skull, label: "Bestiary", blurb: "Every horror the Corruption animates, cataloged." },
  { href: "/items", icon: Gem, label: "Items", blurb: "Loot, gear, and what bleeds to drop it." },
  { href: "/music", icon: Music, label: "Music", blurb: "The full atmospheric soundtrack." },
  { href: "/gallery", icon: Images, label: "Gallery", blurb: "Concept art and fragments of the world." },
  { href: "/forum", icon: MessagesSquare, label: "Forum", blurb: "Strategies, theories, and co-op companions." },
];

const TEAM = [
  { name: "Phan Phuc Binh", role: "Project Leader · Creative Director · Level & Network" },
  { name: "Nguyen Nhat Dang", role: "Combat & Enemy Design · Gameplay & AI Programming" },
  { name: "Tran Thien Dang", role: "QA Tester" },
  { name: "Le Trung Hau", role: "Narrative & UX/UI · System Design · Full-stack Dev" },
];

export default function AboutPage() {
  return (
    <PageShell size="lg">
      {/* Hero */}
      <Reveal className="relative overflow-hidden rounded-card border border-border bg-surface/60 px-6 py-14 sm:px-12 sm:py-16">
        <span aria-hidden className="pointer-events-none absolute -right-16 -top-16 h-72 w-72 rounded-full bg-accent/10 blur-[110px]" />
        <p className="font-mono text-[11px] uppercase tracking-[0.35em] text-accent">The Project</p>
        <h1 className="mt-4 max-w-3xl font-display text-4xl font-bold tracking-tight text-balance text-fg sm:text-5xl">
          A dying world, built for two.
        </h1>
        <p className="mt-5 max-w-2xl text-lg leading-relaxed text-fg-muted">
          {SITE_NAME} is a dark-fantasy 2D souls-like for co-op. Awaken as Ren, an amnesiac soul
          bound to the god Iris, and descend through a world held forever in the moment of its
          death — to read what killed it before it is allowed to end.
        </p>
        <div className="mt-7 flex flex-wrap gap-3">
          <Link href="/story" className="group inline-flex items-center gap-2 rounded-md bg-accent px-5 py-2.5 text-sm font-semibold uppercase tracking-[0.15em] text-accent-fg transition-[filter,box-shadow] hover:brightness-105 hover:shadow-[var(--shadow-glow)]">
            Read the story <ArrowRight size={15} className="transition-transform group-hover:translate-x-1" />
          </Link>
          <Link href="/wiki" className="inline-flex items-center gap-2 rounded-md border border-border-strong px-5 py-2.5 text-sm font-semibold uppercase tracking-[0.15em] text-fg transition-colors hover:border-accent hover:text-accent">
            Explore the wiki
          </Link>
        </div>
      </Reveal>

      {/* What the game is */}
      <Reveal as="section" className="mt-16">
        <div className="mb-6 border-b border-border pb-4">
          <h2 className="font-display text-sm font-semibold uppercase tracking-[0.25em] text-fg-muted">The Game</h2>
        </div>
        <div className="grid gap-4 sm:grid-cols-3">
          {PILLARS.map(({ icon: Icon, title, body }, i) => (
            <Reveal key={title} delay={i}>
              <Card className="h-full p-6">
                <Icon size={22} className="text-accent" />
                <h3 className="mt-4 font-display text-lg font-semibold text-fg">{title}</h3>
                <p className="mt-2 text-sm leading-relaxed text-fg-muted">{body}</p>
              </Card>
            </Reveal>
          ))}
        </div>
      </Reveal>

      {/* The archive */}
      <Reveal as="section" className="mt-16">
        <div className="mb-6 border-b border-border pb-4">
          <h2 className="font-display text-sm font-semibold uppercase tracking-[0.25em] text-fg-muted">The Archive</h2>
          <p className="mt-2 max-w-2xl text-sm leading-relaxed text-fg-muted">
            This companion portal is the official record of that world — everything below lives
            under a single account.
          </p>
        </div>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
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

      {/* The team */}
      <Reveal as="section" className="mt-16">
        <div className="mb-6 flex items-center gap-3 border-b border-border pb-4">
          <Users2 size={18} className="text-accent" />
          <div>
            <h2 className="font-display text-sm font-semibold uppercase tracking-[0.25em] text-fg-muted">The Team</h2>
            <p className="mt-1 text-sm text-fg-muted">Phronetic Studio · FPT University · SEP490 SU2026</p>
          </div>
        </div>
        <div className="grid gap-3 sm:grid-cols-2">
          {TEAM.map((m, i) => (
            <Reveal key={m.name} delay={i}>
              <Card className="flex h-full items-start gap-4 p-5">
                <span className="inline-flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-accent-soft font-display text-lg font-bold text-accent">
                  {m.name.split(" ").slice(-1)[0][0]}
                </span>
                <div className="min-w-0">
                  <p className="font-display font-semibold text-fg">{m.name}</p>
                  <p className="mt-1 text-sm leading-relaxed text-fg-muted">{m.role}</p>
                </div>
              </Card>
            </Reveal>
          ))}
        </div>
      </Reveal>

      {/* Footer links */}
      <div className="mt-16 flex flex-wrap items-center gap-3 border-t border-border pt-6 text-sm">
        <Link href="/privacy" className="text-accent transition-opacity hover:opacity-80">Privacy Policy</Link>
        <span className="text-fg-subtle">·</span>
        <Link href="/terms" className="text-accent transition-opacity hover:opacity-80">Terms of Service</Link>
      </div>
    </PageShell>
  );
}
