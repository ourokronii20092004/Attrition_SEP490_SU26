"use client";

import { useQuery } from "@tanstack/react-query";
import { useParams } from "next/navigation";
import Link from "next/link";
import { Sparkles } from "lucide-react";
import { skillsApi } from "@/lib/api/skills";
import { resolveMediaUrl } from "@/lib/api/media";
import { qk } from "@/lib/query-keys";
import { PageShell } from "@/components/ui/page-shell";
import { BackButton } from "@/components/ui/back-button";
import { Reveal } from "@/components/ui/reveal";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";

export default function SkillDetailPage() {
  const params = useParams<{ id: string }>();
  const { data: skill, isPending } = useQuery({
    queryKey: qk.skills.detail(params.id),
    enabled: !!params.id,
    queryFn: async () => {
      const res = await skillsApi.get(params.id);
      return res.success ? res.data : null;
    },
  });

  if (isPending) return <PageShell><Skeleton className="h-4 w-20" /><Skeleton className="mt-5 aspect-[21/9] w-full rounded-xl" /><Skeleton className="mt-6 h-12 w-1/2" /><div className="mt-6 grid grid-cols-2 gap-3 sm:grid-cols-4">{Array.from({ length: 8 }).map((_, i) => <Skeleton key={i} className="h-20 rounded-card" />)}</div></PageShell>;

  if (!skill) return <PageShell><EmptyState icon={Sparkles} title="Skill not found" description="This skill may no longer be available." action={<Link href="/skills"><Button variant="secondary">Back to Skills</Button></Link>} /></PageShell>;

  const projectile = skill.delivery === "Projectile";
  return <PageShell>
    <BackButton href="/skills" label="Skills" />
    <Reveal className="mt-6">
      {skill.imageUrl ? <div className="aspect-[21/9] overflow-hidden rounded-xl border border-border bg-surface-2"><img src={resolveMediaUrl(skill.imageUrl) ?? ""} alt={skill.name} className="h-full w-full object-cover" /></div> : <div className="flex aspect-[21/9] items-center justify-center rounded-xl border border-border bg-surface-2"><Sparkles size={72} className="text-accent" /></div>}
      <p className="mt-6 font-mono text-[11px] uppercase tracking-[0.3em] text-accent">{skill.element} skill</p>
      <div className="mt-3 flex flex-wrap items-center gap-3"><h1 className="font-display text-4xl font-bold tracking-tight text-fg sm:text-5xl">{skill.name}</h1><Badge>{skill.rarity}</Badge><Badge>{skill.damageType}</Badge></div>
      <p className="mt-5 max-w-3xl whitespace-pre-wrap text-base leading-relaxed text-fg-muted">{skill.description || "No description available."}</p>
    </Reveal>

    <Section title="Core stats">
      <Stat label="Base damage" value={skill.baseDamage} /><Stat label="AP scaling" value={skill.apScaling} /><Stat label="Mana cost" value={skill.manaCost} /><Stat label="Cooldown" value={`${skill.cooldown}s`} />
      <Stat label="Cast time" value={`${skill.castTime}s`} /><Stat label="Delivery" value={skill.delivery} /><Stat label="Hit shape" value={skill.hitShape} /><Stat label="Range" value={skill.range} />
    </Section>

    <Section title="Hit timing & impact">
      <Stat label="Active start" value={`${Math.round(skill.activeStartFrac * 100)}%`} /><Stat label="Active end" value={`${Math.round(skill.activeEndFrac * 100)}%`} />
      <Stat label="Knockback" value={skill.knockbackForce} /><Stat label="Tick interval" value={`${skill.tickInterval}s`} />
      <Stat label="Sweet spot radius" value={skill.sweetSpotRadius} /><Stat label="Sweet spot multiplier" value={`${skill.sweetSpotMultiplier}×`} /><Stat label="VFX lifetime" value={`${skill.vfxLifetime}s`} />
    </Section>

    <Section title="Hit area">
      <Stat label="Angle" value={`${skill.angle}°`} /><Stat label="Width" value={skill.rectWidth} /><Stat label="Height" value={skill.rectHeight} /><Stat label="Offset X" value={skill.offsetX} /><Stat label="Offset Y" value={skill.offsetY} />
    </Section>

    {projectile && <Section title="Projectile"><Stat label="Speed" value={skill.projectileSpeed} /><Stat label="Count" value={skill.projectileCount} /><Stat label="Spread" value={`${skill.spreadAngle}°`} /></Section>}
  </PageShell>;
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return <Reveal as="section" className="mt-10"><h2 className="font-display text-sm font-semibold uppercase tracking-[0.25em] text-fg-muted">{title}</h2><div className="mt-3 grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4">{children}</div></Reveal>;
}

function Stat({ label, value }: { label: string; value: React.ReactNode }) {
  return <Card className="p-4"><p className="text-[10px] uppercase tracking-wider text-fg-subtle">{label}</p><p className="mt-1 break-words text-lg font-semibold tabular-nums text-fg">{value}</p></Card>;
}

function Badge({ children }: { children: React.ReactNode }) {
  return <span className="rounded-full bg-accent/10 px-3 py-1 text-sm font-medium text-accent">{children}</span>;
}
