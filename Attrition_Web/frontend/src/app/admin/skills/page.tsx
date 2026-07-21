"use client";

import { useEffect, useState } from "react";
import { useFieldArray, useForm } from "react-hook-form";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
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

const STAT_TYPES = ["MaxHP", "MaxMana", "MaxStamina", "AD", "AP", "DEF", "RES", "MoveSpeed", "AttackSpeed"];
const finite = z.coerce.number().finite();
const nonNegative = finite.min(0);
const schema = z.object({
  skillId: z.string(), name: z.string().min(1, "Required").max(100), description: z.string().max(2000),
  iconKey: z.string().nullable(), rarity: z.string().min(1).max(50), maxStack: z.coerce.number().int().min(1).max(9999),
  isKeyItem: z.boolean(), modifiers: z.array(z.object({ stat: z.string().min(1), amount: z.coerce.number().int().min(-100000).max(100000) })).max(50),
  element: z.enum(["Fire", "Wood", "Earth", "Thunder", "Thrust"]), manaCost: z.coerce.number().int().min(0),
  castTime: nonNegative, cooldown: nonNegative, activeStartFrac: finite.min(0).max(1), activeEndFrac: finite.min(0).max(1),
  damageType: z.enum(["Physical", "Magic", "True"]), baseDamage: z.coerce.number().int().min(0), apScaling: nonNegative,
  knockbackForce: nonNegative, tickInterval: nonNegative, sweetSpotRadius: nonNegative, sweetSpotMultiplier: nonNegative,
  delivery: z.enum(["AreaInstant", "Projectile"]), hitShape: z.enum(["Cone", "Circle", "Rectangle"]), range: nonNegative,
  angle: finite.min(0).max(360), rectWidth: nonNegative, rectHeight: nonNegative, offsetX: finite, offsetY: finite,
  projectileSpeed: nonNegative, projectileCount: z.coerce.number().int().min(1).max(100), spreadAngle: nonNegative,
  vfxLifetime: nonNegative, imageUrl: z.string().nullable(),
}).refine((v) => v.activeEndFrac >= v.activeStartFrac, { path: ["activeEndFrac"], message: "Must be after active start." });

type Values = z.infer<typeof schema>;
type NumberField = Exclude<keyof Values, "skillId" | "name" | "description" | "iconKey" | "rarity" | "isKeyItem" | "modifiers" | "element" | "damageType" | "delivery" | "hitShape" | "imageUrl">;

export default function AdminSkillsPage() {
  const { user } = useAuth();
  const client = useQueryClient();
  const [editing, setEditing] = useState<SkillResponse | null>(null);
  const [dirty, setDirty] = useState(false);
  const { data: skills = [], isPending } = useQuery({
    queryKey: qk.admin.skills(), enabled: user?.role === "Admin",
    queryFn: async () => { const r = await skillsApi.list(); return r.success ? r.data : []; },
  });
  if (!user || user.role !== "Admin") return null;
  if (isPending) return <PageLoader />;
  return <div>
    <AdminPageHeader title="Skills" />
    <Modal open={!!editing} onClose={() => setEditing(null)} title={editing ? `Edit ${editing.name || editing.skillId}` : "Edit Skill"} size="lg" dirty={dirty}>
      {editing && <SkillForm initial={editing} onDirtyChange={setDirty} onDone={() => { setDirty(false); setEditing(null); client.invalidateQueries({ queryKey: qk.admin.skills() }); }} onCancel={() => { setDirty(false); setEditing(null); }} />}
    </Modal>
    <AdminTable columns={[{ key: "img", label: "" }, { key: "name", label: "Skill" }, { key: "damage", label: "Damage" }, { key: "timing", label: "Timing" }, { key: "action", label: "", align: "right" }]} empty={!skills.length}>
      {skills.map((s) => <AdminRow key={s.skillId} onClick={() => setEditing(s)}>
        <td className="px-3 py-2">{s.imageUrl ? <img src={resolveMediaUrl(s.imageUrl) ?? ""} alt="" className="h-9 w-9 rounded object-cover" /> : <Sparkles size={20} />}</td>
        <td className="px-3 py-2 font-medium">{s.name || s.skillId}<div className="text-xs text-fg-muted">{s.skillId} · {s.element} · {s.delivery}</div></td>
        <td className="px-3 py-2 text-fg-muted">{s.baseDamage} + AP×{s.apScaling}</td>
        <td className="px-3 py-2 text-fg-muted">Cast {s.castTime}s · CD {s.cooldown}s</td>
        <td className="px-3 py-2 text-right"><Button size="sm" variant="secondary">Edit</Button></td>
      </AdminRow>)}
    </AdminTable>
  </div>;
}

function SkillForm({ initial, onDone, onCancel, onDirtyChange }: { initial: SkillResponse; onDone: () => void; onCancel: () => void; onDirtyChange: (dirty: boolean) => void }) {
  const [error, setError] = useState<string | null>(null);
  const defaults: Values = { ...initial, description: initial.description ?? "", element: initial.element as Values["element"], damageType: initial.damageType as Values["damageType"], delivery: initial.delivery as Values["delivery"], hitShape: initial.hitShape as Values["hitShape"] };
  const { register, handleSubmit, watch, setValue, control, formState: { errors, isSubmitting, isDirty } } = useForm<Values>({ resolver: zodResolver(schema), defaultValues: defaults });
  const { fields, append, remove } = useFieldArray({ control, name: "modifiers" });
  const mutation = useMutation({ mutationFn: (v: SkillUpdateRequest) => skillsApi.update(initial.skillId, v) });
  const delivery = watch("delivery");
  const hitShape = watch("hitShape");
  useEffect(() => onDirtyChange(isDirty), [isDirty, onDirtyChange]);
  const submit = async (values: Values) => { setError(null); try { await mutation.mutateAsync({ ...values, description: values.description || null, iconKey: values.iconKey || null }); onDone(); } catch (e) { setError(parseApiError(e)); } };
  const number = (name: NumberField, label: string) => <Input label={label} type="number" step="any" error={errors[name]?.message as string} {...register(name)} />;

  return <form onSubmit={handleSubmit(submit)} className="space-y-6">
    <Section title="General">
      <AssetImageField value={watch("imageUrl")} onChange={(v) => setValue("imageUrl", v, { shouldDirty: true })} sourceType="skill" sourceId={initial.skillId} label="Skill image" />
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        <Input label="Skill ID" value={initial.skillId} disabled />
        <Input label="Name" error={errors.name?.message} {...register("name")} />
        <Input label="Icon key" {...register("iconKey")} />
        <Input label="Rarity" error={errors.rarity?.message} {...register("rarity")} />
        {number("maxStack", "Max stack")}
        <label className="flex items-end gap-2 pb-2 text-sm text-fg-muted"><input type="checkbox" {...register("isKeyItem")} /> Key item</label>
      </div>
      <label className="block text-sm font-medium text-fg-muted">Description<textarea rows={3} {...register("description")} className="mt-1 w-full rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-fg outline-none focus:border-accent" /></label>
      <div className="flex items-center justify-between"><h4 className="text-sm font-medium text-fg">Stat modifiers</h4><Button type="button" size="sm" variant="secondary" onClick={() => append({ stat: "AD", amount: 1 })}>Add modifier</Button></div>
      {fields.map((field, index) => <div key={field.id} className="grid grid-cols-[1fr_1fr_auto] items-end gap-2">
        <Select label="Stat" {...register(`modifiers.${index}.stat`)}>{STAT_TYPES.map((x) => <option key={x}>{x}</option>)}</Select>
        <Input label="Amount" type="number" {...register(`modifiers.${index}.amount`)} />
        <Button type="button" size="sm" variant="danger" onClick={() => remove(index)}>Remove</Button>
      </div>)}
    </Section>
    <Section title="Cost & timing"><div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
      <Select label="Element" {...register("element")}><option>Fire</option><option>Wood</option><option>Earth</option><option>Thunder</option><option>Thrust</option></Select>
      {number("manaCost", "Mana cost")}{number("castTime", "Cast time")}{number("cooldown", "Cooldown")}{number("activeStartFrac", "Active start")}{number("activeEndFrac", "Active end")}
    </div></Section>
    <Section title="Damage"><div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
      <Select label="Damage type" {...register("damageType")}><option>Physical</option><option>Magic</option><option>True</option></Select>
      {number("baseDamage", "Base damage")}{number("apScaling", "AP scaling")}{number("knockbackForce", "Knockback")}{number("tickInterval", "Tick interval")}{number("sweetSpotRadius", "Sweet radius")}{number("sweetSpotMultiplier", "Sweet multiplier")}
    </div></Section>
    <Section title="Delivery & hitbox"><div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
      <Select label="Delivery" {...register("delivery")}><option>AreaInstant</option><option>Projectile</option></Select>
      {delivery === "AreaInstant" && <><Select label="Hit shape" {...register("hitShape")}><option>Cone</option><option>Circle</option><option>Rectangle</option></Select>{number("range", "Range")}{hitShape === "Cone" && number("angle", "Angle")}{hitShape === "Rectangle" && <>{number("rectWidth", "Rect width")}{number("rectHeight", "Rect height")}</>}{number("offsetX", "Offset X")}{number("offsetY", "Offset Y")}</>}
      {delivery === "Projectile" && <>{number("projectileSpeed", "Projectile speed")}{number("projectileCount", "Projectile count")}{number("spreadAngle", "Spread angle")}</>}
    </div></Section>
    <Section title="VFX"><div className="max-w-xs">{number("vfxLifetime", "VFX lifetime")}</div><p className="text-xs text-fg-subtle">Projectile and VFX prefabs remain configured in Unity.</p></Section>
    {error && <p className="rounded-md bg-danger/10 px-3 py-2 text-sm text-danger">{error}</p>}
    <div className="flex justify-end gap-2"><Button type="button" variant="secondary" onClick={onCancel}>Cancel</Button><Button type="submit" loading={isSubmitting}>Save</Button></div>
  </form>;
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return <fieldset className="space-y-3 rounded-lg border border-border p-4"><legend className="px-1 font-display font-semibold text-fg">{title}</legend>{children}</fieldset>;
}
