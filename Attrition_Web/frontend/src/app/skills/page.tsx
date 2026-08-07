"use client";

import { Suspense, useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import { Search, Sparkles } from "lucide-react";
import { skillsApi } from "@/lib/api/skills";
import { qk } from "@/lib/query-keys";
import { PageShell } from "@/components/ui/page-shell";
import { PageTitle } from "@/components/ui/page-title";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { EmptyState } from "@/components/ui/empty-state";
import { SkeletonGrid } from "@/components/ui/skeleton";
import { SkillTree } from "@/components/skill-tree";
import { buildSkillTree, ELEMENTS } from "@/lib/skill-tree";
import { useQueryParam } from "@/lib/hooks/use-url-pagination";

function SkillsBrowser() {
  // Filters live in the URL so opening a skill and pressing Back restores the exact view —
  // the behaviour bestiary/items/wiki/forum already have.
  const [search, setSearch] = useQueryParam("q");
  const [element, setElement] = useQueryParam("element");
  const [damageType, setDamageType] = useQueryParam("damage");
  const { data: skills = [], isPending } = useQuery({
    queryKey: qk.skills.list(),
    queryFn: async () => { const r = await skillsApi.list(); return r.success ? r.data ?? [] : []; },
  });
  const filtered = useMemo(() => skills.filter((s) =>
    (!search || `${s.name} ${s.description ?? ""}`.toLowerCase().includes(search.toLowerCase())) &&
    (!element || s.element === element) && (!damageType || s.damageType === damageType)),
  [skills, search, element, damageType]);
  // The whole tree is shown at once — paginating a tree would cut branches in half.
  const branches = useMemo(() => buildSkillTree(filtered), [filtered]);

  return <PageShell>
    <PageTitle description="Player abilities synced from the game, branching by element and deepening with rarity.">Skill Tree</PageTitle>
    <div className="flex flex-wrap items-end gap-3">
      <div className="relative min-w-56 flex-1"><Search size={16} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-fg-subtle" /><Input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Search skills..." aria-label="Search skills" className="pl-9" /></div>
      <Select value={element} onChange={(e) => setElement(e.target.value)} aria-label="Filter by element"><option value="">All elements</option>{ELEMENTS.map((x) => <option key={x}>{x}</option>)}</Select>
      <Select value={damageType} onChange={(e) => setDamageType(e.target.value)} aria-label="Filter by damage type"><option value="">All damage</option>{["Physical","Magic","True"].map((x) => <option key={x}>{x}</option>)}</Select>
    </div>
    {isPending ? <SkeletonGrid count={6} className="mt-6 lg:grid-cols-3" /> : !filtered.length ?
      <EmptyState icon={Sparkles} title="No skills found" description="Try changing the search or filters." className="mt-6" /> :
      <div className="mt-6">
        <SkillTree branches={branches} renderHref={(s) => `/skills/${encodeURIComponent(s.skillId)}`} />
      </div>}
  </PageShell>;
}

export default function SkillsPage() {
  // useSearchParams (via useQueryParam) needs a Suspense boundary.
  return (
    <Suspense fallback={null}>
      <SkillsBrowser />
    </Suspense>
  );
}
