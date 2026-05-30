import Link from "next/link";
import { SITE_NAME } from "@/lib/config";

const EXPLORE = [
  { href: "/wiki", label: "Wiki" },
  { href: "/bestiary", label: "Bestiary" },
  { href: "/forum", label: "Forum" },
  { href: "/music", label: "Music" },
  { href: "/gallery", label: "Gallery" },
];

const COMPANY = [
  { href: "/about", label: "About" },
  { href: "/privacy", label: "Privacy" },
  { href: "/terms", label: "Terms" },
];

export function Footer() {
  return (
    <footer className="mt-24 border-t border-border bg-surface/30">
      <div className="mx-auto grid max-w-7xl gap-10 px-5 py-14 sm:grid-cols-2 sm:px-8 lg:grid-cols-4">
        <div className="lg:col-span-2">
          <div className="flex items-center gap-2">
            <span className="h-2 w-2 rounded-full bg-accent shadow-[var(--shadow-glow)]" />
            <p className="font-display text-base font-bold uppercase tracking-[0.2em] text-fg">{SITE_NAME}</p>
          </div>
          <p className="mt-3 max-w-xs text-sm text-fg-muted">
            A 2D co-op souls-like, and the archive of its dying world.
          </p>
        </div>
        <FooterCol title="Explore" links={EXPLORE} />
        <FooterCol title="Company" links={COMPANY} />
      </div>
      <div className="border-t border-border">
        <p className="mx-auto max-w-7xl px-5 py-5 text-xs text-fg-subtle sm:px-8">
          A thesis project &middot; SEP490 SU26 &middot; Phronetic Studio
        </p>
      </div>
    </footer>
  );
}

function FooterCol({ title, links }: { title: string; links: { href: string; label: string }[] }) {
  return (
    <div>
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
