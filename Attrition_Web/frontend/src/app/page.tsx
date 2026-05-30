import Link from "next/link";
import { SITE_NAME } from "@/lib/config";

export default function HomePage() {
  return (
    <div className="mx-auto max-w-6xl px-4 py-12">
      <section className="text-center">
        <h1 className="font-display text-5xl font-bold tracking-tight text-fg">
          {SITE_NAME}
        </h1>
        <p className="mt-4 text-lg text-fg-muted">
          Wiki, lore, and community for the Attrition universe
        </p>
        <div className="mt-8 flex flex-wrap justify-center gap-4">
          <Link href="/wiki" className="rounded-lg bg-accent px-6 py-3 font-medium text-accent-fg transition hover:opacity-90">
            Explore Wiki
          </Link>
          <Link href="/bestiary" className="rounded-lg border border-border-strong px-6 py-3 font-medium text-fg transition hover:bg-surface-2">
            Bestiary
          </Link>
          <Link href="/forum" className="rounded-lg border border-border-strong px-6 py-3 font-medium text-fg transition hover:bg-surface-2">
            Forum
          </Link>
          <Link href="/music" className="rounded-lg border border-border-strong px-6 py-3 font-medium text-fg transition hover:bg-surface-2">
            Music
          </Link>
        </div>
      </section>
    </div>
  );
}
