import Link from "next/link";
import { SITE_NAME } from "@/lib/config";

const EXPLORE = [
  { href: "/wiki", label: "Wiki" },
  { href: "/bestiary", label: "Bestiary" },
  { href: "/items", label: "Items" },
  { href: "/world", label: "World" },
];

const COMMUNITY = [
  { href: "/forum", label: "Forum" },
  { href: "/music", label: "Music" },
  { href: "/gallery", label: "Gallery" },
  { href: "/characters", label: "Characters" },
];

const LORE = [
  { href: "/story", label: "The Story" },
  { href: "/story/ren", label: "Ren" },
  { href: "/story/five-pillars", label: "The Five Pillars" },
  { href: "/story/the-void", label: "The Void" },
];

const COMPANY = [
  { href: "/about", label: "About" },
  { href: "/privacy", label: "Privacy" },
  { href: "/terms", label: "Terms" },
];

export function Footer() {
  return (
    <footer className="mt-24 border-t border-border bg-surface/30">
      {/* Accent hairline echoing the hero */}
      <div aria-hidden className="h-px bg-gradient-to-r from-transparent via-accent/30 to-transparent" />

      <div className="mx-auto grid max-w-7xl gap-10 px-5 py-14 sm:grid-cols-2 sm:px-8 lg:grid-cols-12">
        <div className="lg:col-span-4">
          <div className="flex items-center gap-2">
            <span className="h-2 w-2 rounded-full bg-accent shadow-[var(--shadow-glow)]" />
            <p className="font-display text-base font-bold uppercase tracking-[0.2em] text-fg">{SITE_NAME}</p>
          </div>
          <p className="mt-3 max-w-xs text-sm leading-relaxed text-fg-muted">
            A 2D co-op souls-like, and the archive of its dying world. Everything dies; nothing is forgotten.
          </p>
          <Link
            href="/story"
            className="mt-5 inline-flex items-center gap-1.5 font-mono text-[11px] uppercase tracking-[0.2em] text-fg-subtle transition-colors hover:text-accent"
          >
            Read the lore &rarr;
          </Link>
        </div>

        <FooterCol title="Explore" links={EXPLORE} className="lg:col-span-2" />
        <FooterCol title="Community" links={COMMUNITY} className="lg:col-span-2" />
        <FooterCol title="Lore" links={LORE} className="lg:col-span-2" />
        <FooterCol title="Company" links={COMPANY} className="lg:col-span-2" />
      </div>

      <div className="border-t border-border">
        <div className="mx-auto flex max-w-7xl flex-col gap-2 px-5 py-5 text-xs text-fg-subtle sm:flex-row sm:items-center sm:justify-between sm:px-8">
          <p>A thesis project &middot; SEP490 SU26 &middot; Phronetic Studio</p>
          <p className="font-mono uppercase tracking-[0.15em]">Eldravir &middot; Aerithreria</p>
        </div>
      </div>
    </footer>
  );
}

function FooterCol({ title, links, className }: { title: string; links: { href: string; label: string }[]; className?: string }) {
  return (
    <div className={className}>
      <p className="mb-3 text-xs font-semibold uppercase tracking-[0.2em] text-fg-subtle">{title}</p>
      <nav className="flex flex-col gap-2.5">
        {links.map((l) => (
          <Link key={l.href} href={l.href} className="text-sm text-fg-muted transition-colors hover:text-accent">
            {l.label}
          </Link>
        ))}
      </nav>
    </div>
  );
}
