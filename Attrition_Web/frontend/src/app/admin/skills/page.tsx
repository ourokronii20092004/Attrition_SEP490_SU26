"use client";

import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Sparkles } from "lucide-react";
import { useAuth } from "@/lib/providers";
import { skillsApi } from "@/lib/api/skills";
import { resolveMediaUrl } from "@/lib/api/media";
import { parseApiError } from "@/lib/api/parse-error";
import { qk } from "@/lib/query-keys";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { Modal } from "@/components/ui/modal";
import { PageLoader } from "@/components/ui/spinner";
import { AdminPageHeader, AdminTable, AdminRow } from "@/components/admin/admin-table";
import { AssetImageField } from "@/components/admin/asset-image-field";
import type { SkillResponse, SkillUpdateRequest } from "@/lib/types";

const finite = z.coerce.number().finite();
const nonNegative = finite.min(0);
const schema = z.object({
  skillId: z.string(), element: z.enum(["Fire", "Wood", "Earth", "Thunder", "Thrust"]),
  manaCost: z.coerce.number().int().min(0), castTime: nonNegative, cooldown: nonNegative,
  activeStartFrac: finite.min(0).max(1), activeEndFrac: finite.min(0).max(1),
  damageType: z.enum(["Physical", "Magic", "True"]), baseDamage: z.coerce.number().int().min(0),
  apScaling: nonNegative, knockbackForce: nonNegative, tickInterval: nonNegative,
  sweetSpotRadius: nonNegative, sweetSpotMultiplier: nonNegative,
  delivery: z.enum(["AreaInstant", "Projectile"]), hitShape: z.enum(["Cone", "Circle", "Rectangle"]),
  range: nonNegative, angle: finite.min(0).max(360), rectWidth: nonNegative, rectHeight: nonNegative,
  offsetX: finite, offsetY: finite, projectileSpeed: nonNegative,
  projectileCount: z.coerce.number().int().min(1).max(100), spreadAngle: nonNegative,
  vfxLifetime: nonNegative, imageUrl: z.string().nullable(),
}).refine((v) => v.activeEndFrac >= v.activeStartFrac, { path: ["activeEndFrac"], message: "Must be after active start." });

type Values = z.infer<typeof schema>;

export default function AdminSkillsPage() {
  const { user } = useAuth();
  const client = useQueryClient();
  const [editing, setEditing] = useState<SkillResponse | null>(null);
  const { data: skills = [], isPending } = useQuery({
    queryKey: qk.admin.skills(), enabled: user?.role === "Admin",
    queryFn: async () => { const r = await skillsApi.list(); return r.success ? r.data : []; },
  });
  if (!user || user.role !== "Admin") return null;
  if (isPending) return <PageLoader />;
  return <div>
    <AdminPageHeader title="Skills" />
    <Modal open={!!editing} onClose={() => setEditing(null)} title={editing ? `Edit ${editing.skillId}` : "Edit Skill"} size="lg">
      {editing && <SkillForm initial={editing} onDone={() => { setEditing(null); client.invalidateQueries({ queryKey: qk.admin.skills() }); }} onCancel={() => setEditing(null)} />}
    </Modal>
    <AdminTable columns={[{ key: "img", label: "" }, { key: "id", label: "Skill" }, { key: "damage", label: "Damage" }, { key: "timing", label: "Timing" }, { key: "action", label: "", align: "right" }]} empty={!skills.length}>
      {skills.map((s) => <AdminRow key={s.skillId} onClick={() => setEditing(s)}>
        <td className="px-3 py-2">{s.imageUrl ? <img src={resolveMediaUrl(s.imageUrl) ?? ""} alt="" className="h-9 w-9 rounded object-cover" /> : <Sparkles size={20} />}</td>
        <td className="px-3 py-2 font-medium">{s.skillId}<div className="text-xs text-fg-muted">{s.element} · {s.delivery}</div></td>
        <td className="px-3 py-2 text-fg-muted">{s.baseDamage} + AP×{s.apScaling}</td>
        <td className="px-3 py-2 text-fg-muted">Cast {s.castTime}s · CD {s.cooldown}s</td>
        <td className="px-3 py-2 text-right"><Button size="sm" variant="secondary">Edit</Button></td>
      </AdminRow>)}
    </AdminTable>
  </div>;
}

function SkillForm({ initial, onDone, onCancel }: { initial: SkillResponse; onDone: () => void; onCancel: () => void }) {
  const [error, setError] = useState<string | null>(null);
  const defaults = { ...initial, element: initial.element as Values["element"], damageType: initial.damageType as Values["damageType"], delivery: initial.delivery as Values["delivery"], hitShape: initial.hitShape as Values["hitShape"] };
  const { register, handleSubmit, watch, setValue, formState: { errors, isSubmitting } } = useForm<Values>({ resolver: zodResolver(schema), defaultValues: defaults });
  const mutation = useMutation({ mutationFn: (v: SkillUpdateRequest) => skillsApi.update(initial.skillId, v) });
  const submit = async (values: Values) => { setError(null); try { await mutation.mutateAsync(values); onDone(); } catch (e) { setError(parseApiError(e)); } };
  const fields: Array<[keyof Values, string]> = [
    ["manaCost", "Mana cost"], ["castTime", "Cast time"], ["cooldown", "Cooldown"],
    ["activeStartFrac", "Active start"], ["activeEndFrac", "Active end"], ["baseDamage", "Base damage"],
    ["apScaling", "AP scaling"], ["knockbackForce", "Knockback"], ["tickInterval", "Tick interval"],
    ["sweetSpotRadius", "Sweet radius"], ["sweetSpotMultiplier", "Sweet multiplier"], ["range", "Range"],
    ["angle", "Angle"], ["rectWidth", "Rect width"], ["rectHeight", "Rect height"],
    ["offsetX", "Offset X"], ["offsetY", "Offset Y"], ["projectileSpeed", "Projectile speed"],
    ["projectileCount", "Projectile count"], ["spreadAngle", "Spread angle"], ["vfxLifetime", "VFX lifetime"],
  ];
  return <form onSubmit={handleSubmit(submit)} className="space-y-4">
    <AssetImageField value={watch("imageUrl")} onChange={(v) => setValue("imageUrl", v, { shouldDirty: true })} sourceType="skill" sourceId={initial.skillId} />
    <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
      <label className="text-sm">Element<Select {...register("element")}><option>Fire</option><option>Wood</option><option>Earth</option><option>Thunder</option><option>Thrust</option></Select></label>
      <label className="text-sm">Damage type<Select {...register("damageType")}><option>Physical</option><option>Magic</option><option>True</option></Select></label>
      <label className="text-sm">Delivery<Select {...register("delivery")}><option>AreaInstant</option><option>Projectile</option></Select></label>
      <label className="text-sm">Hit shape<Select {...register("hitShape")}><option>Cone</option><option>Circle</option><option>Rectangle</option></Select></label>
      {fields.map(([name, label]) => <label key={name} className="text-sm">{label}<Input type="number" step="any" {...register(name)} /><span className="text-xs text-danger">{errors[name]?.message as string}</span></label>)}
    </div>
    {error && <p className="text-sm text-danger">{error}</p>}
    <div className="flex justify-end gap-2"><Button type="button" variant="secondary" onClick={onCancel}>Cancel</Button><Button type="submit" disabled={isSubmitting}>Save</Button></div>
  </form>;
}
