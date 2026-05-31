import type { Metadata } from "next";
import Link from "next/link";
import { PageShell } from "@/components/ui/page-shell";
import { PageTitle } from "@/components/ui/page-title";
import { Card } from "@/components/ui/card";

export const metadata: Metadata = {
  title: "About",
  description: "About Attrition — a 2D co-op souls-like ARPG and its companion archive.",
};

const TEAM = [
  { name: "Phan Phuc Binh", role: "Project Leader · Creative Director · Level & Network" },
  { name: "Nguyen Nhat Dang", role: "Combat & Enemy Design · Gameplay & AI Programming" },
  { name: "Tran Thien Dang", role: "QA Tester" },
  { name: "Le Trung Hau", role: "Narrative & UX/UI · System Design · Full-stack Dev" },
];

export default function AboutPage() {
  return (
    <PageShell size="md">
      <PageTitle eyebrow="The Project" description="A 2D cooperative souls-like ARPG, and the official archive of its dying world.">
        About Attrition
      </PageTitle>

      <div className="prose-content">
        <p>
          Attrition is a dark-fantasy 2D souls-like built from the ground up for two-player
          co-op. Awaken as Ren, an amnesiac soul bound to the mysterious Iris, and cleanse a
          world consumed by a parasitic magic called Corruption. Explore interconnected maps,
          delve into intricate missions, and dismantle formidable bosses, alone or with a partner.
        </p>
        <p>
          This companion portal is the official record of that world: a structured wiki, a
          full bestiary, the soundtrack, character galleries, and a community forum, all under
          a single account.
        </p>
      </div>

      <h2 className="mt-12 font-display text-2xl font-semibold text-fg">The Team</h2>
      <p className="mt-1 text-sm text-fg-muted">Phronetic Studio · FPT University · SEP490 SU2026</p>
      <div className="mt-5 grid gap-3 sm:grid-cols-2">
        {TEAM.map((m) => (
          <Card key={m.name} className="p-5">
            <p className="font-display font-semibold text-fg">{m.name}</p>
            <p className="mt-1 text-sm text-fg-muted">{m.role}</p>
          </Card>
        ))}
      </div>

      <div className="mt-12 flex flex-wrap gap-3 text-sm">
        <Link href="/privacy" className="text-accent transition-opacity hover:opacity-80">Privacy Policy</Link>
        <span className="text-fg-subtle">·</span>
        <Link href="/terms" className="text-accent transition-opacity hover:opacity-80">Terms of Service</Link>
      </div>
    </PageShell>
  );
}
