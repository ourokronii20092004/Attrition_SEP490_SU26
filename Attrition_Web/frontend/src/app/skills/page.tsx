"use client";

import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { Search, Sparkles } from "lucide-react";
import { skillsApi } from "@/lib/api/skills";
import { resolveMediaUrl } from "@/lib/api/media";
import { qk } from "@/lib/query-keys";
import { PageShell } from "@/components/ui/page-shell";
import { PageTitle } from "@/components/ui/page-title";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { Card } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/empty-state";
import { SkeletonGrid } from "@/components/ui/skeleton";
import { Pagination } from "@/components/ui/pagination";
import { useClientPagination } from "@/lib/hooks/use-client-pagination";

export default function SkillsPage() {
  const [search, setSearch] = useState("");
  const [element, setElement] = useState("");
  const [damageType, setDamageType] = useState("");
  const { data: skills = [], isPending } = useQuery({
    queryKey: qk.skills.list(),
    queryFn: async () => { const r = await skillsApi.list(); return r.success ? r.data ?? [] : []; },
  });
  const filtered = useMemo(() => skills.filter((s) =>
    (!search || `${s.name} ${s.description ?? ""}`.toLowerCase().includes(search.toLowerCase())) &&
    (!element || s.element === element) && (!damageType || s.damageType === damageType)),
  [skills, search, element, damageType]);
  const { page, setPage, totalPages, paged } = useClientPagination(filtered, 12);

  return <PageShell>
    <PageTitle description="Player abilities synced from the game and tuned for the live world.">Skills</PageTitle>
    <div className="flex flex-wrap items-end gap-3">
      <div className="relative min-w-56 flex-1"><Search size={16} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-fg-subtle" /><Input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Search skills..." aria-label="Search skills" className="pl-9" /></div>
      <Select value={element} onChange={(e) => setElement(e.target.value)} aria-label="Filter by element"><option value="">All elements</option>{["Fire","Wood","Earth","Thunder","Thrust"].map((x) => <option key={x}>{x}</option>)}</Select>
      <Select value={damageType} onChange={(e) => setDamageType(e.target.value)} aria-label="Filter by damage type"><option value="">All damage</option>{["Physical","Magic","True"].map((x) => <option key={x}>{x}</option>)}</Select>
    </div>
    {isPending ? <SkeletonGrid count={6} className="mt-6 lg:grid-cols-3" /> : !filtered.length ?
      <EmptyState icon={Sparkles} title="No skills found" description="Try changing the search or filters." className="mt-6" /> :
      <div className="stagger mt-6 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">{paged.map((s, i) =>
        <Link key={s.skillId} href={`/skills/${encodeURIComponent(s.skillId)}`} className="group rounded-card focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent">
          <Card style={{ "--i": i } as React.CSSProperties} className="h-full overflow-hidden transition-transform group-hover:-translate-y-0.5">
          {s.imageUrl ? <img src={resolveMediaUrl(s.imageUrl) ?? ""} alt={s.name} className="aspect-[16/9] w-full object-cover" /> : <div className="flex aspect-[16/9] items-center justify-center bg-surface-2"><Sparkles size={42} className="text-accent" /></div>}
          <div className="p-5"><div className="flex items-start justify-between gap-3"><h2 className="font-display text-xl font-semibold text-fg group-hover:text-accent">{s.name}</h2><span className="rounded-full bg-accent/10 px-2.5 py-1 text-xs font-semibold text-accent">{s.element}</span></div>
          <p className="mt-2 line-clamp-2 min-h-10 text-sm text-fg-muted">{s.description || "No description available."}</p>
          <div className="mt-4 grid grid-cols-3 gap-2 text-center text-xs"><Metric label="Damage" value={`${s.baseDamage} + ${s.apScaling} AP`} /><Metric label="Mana" value={s.manaCost} /><Metric label="Cooldown" value={`${s.cooldown}s`} /></div>
          <p className="mt-4 border-t border-border pt-3 text-xs text-fg-subtle">{s.damageType} · {s.delivery} · {s.hitShape} · Range {s.range}</p></div>
          </Card>
        </Link>)}</div>}
    {!isPending && filtered.length > 0 && <Pagination page={page} totalPages={totalPages} onChange={setPage} />}
  </PageShell>;
}

function Metric({ label, value }: { label: string; value: React.ReactNode }) {
  return <div className="rounded-lg bg-surface-2 p-2"><div className="font-semibold text-fg">{value}</div><div className="mt-0.5 text-fg-subtle">{label}</div></div>;
}
