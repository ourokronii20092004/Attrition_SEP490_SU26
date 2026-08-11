"use client";

import Link from "next/link";
import { Sparkles, Trash2 } from "lucide-react";
import { resolveMediaUrl } from "@/lib/api/media";
import { TIER_COLOR, type SkillBranch } from "@/lib/skill-tree";
import type { SkillResponse } from "@/lib/types";

/**
 * Skill tree: one branch per element, each branch descending through rarity tiers.
 *
 * Drawn as a real tree rather than a grid — a trunk runs down each branch, every tier hangs off
 * it, and each node connects back with a short stub. Connectors are plain bordered divs (no SVG,
 * no measuring), so the layout survives reflow, zoom and text scaling.
 *
 * Nodes are buttons when `onSelect` is given (admin: open the editor) and links otherwise
 * (public: go to the skill page). The caller decides; this component only draws.
 */
export function SkillTree({ branches, onSelect, onDelete, renderHref }: {
  branches: SkillBranch[];
  onSelect?: (skill: SkillResponse) => void;
  onDelete?: (skill: SkillResponse) => void;
  renderHref?: (skill: SkillResponse) => string;
}) {
  return (
    <div className="space-y-8">
      {branches.map((branch) => (
        <BranchColumn key={branch.element} branch={branch} onSelect={onSelect} onDelete={onDelete} renderHref={renderHref} />
      ))}
    </div>
  );
}

function BranchColumn({ branch, onSelect, onDelete, renderHref }: {
  branch: SkillBranch;
  onSelect?: (skill: SkillResponse) => void;
  onDelete?: (skill: SkillResponse) => void;
  renderHref?: (skill: SkillResponse) => string;
}) {
  return (
    <section className="rounded-card border border-border bg-surface-1 p-5" aria-label={`${branch.element} branch`}>
      <header className="flex items-baseline justify-between gap-3">
        <h2 className="font-display text-xl font-semibold tracking-tight text-fg">{branch.element}</h2>
        <span className="text-xs text-fg-subtle">
          {branch.total} {branch.total === 1 ? "skill" : "skills"}
        </span>
      </header>

      <div className="mt-4 space-y-0">
        {branch.tiers.map((tier, tierIndex) => (
          <div key={tier.rarity}>
            {/* Trunk segment joining the previous tier to this one. */}
            {tierIndex > 0 && (
              <div className="ml-4 h-6 w-px bg-border sm:ml-6" aria-hidden />
            )}

            <div className="flex items-center gap-3">
              <span className={`rounded-full px-2.5 py-1 text-xs font-semibold ${TIER_COLOR[tier.rarity] ?? TIER_COLOR.Common}`}>
                {tier.rarity}
              </span>
              <span className="h-px flex-1 bg-border" aria-hidden />
            </div>

            <div className="mt-3 flex flex-wrap gap-3 pl-4 sm:pl-6">
              {tier.skills.map((skill) => (
                <SkillNode
                  key={skill.skillId}
                  skill={skill}
                  onSelect={onSelect}
                  onDelete={onDelete}
                  href={renderHref?.(skill)}
                />
              ))}
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}

/** Card content is identical whether the node is a link or a button, so it lives here once. */
function NodeBody({ skill }: { skill: SkillResponse }) {
  const image = skill.imageUrl ? resolveMediaUrl(skill.imageUrl) : null;
  return (
    <>
      <span className="flex h-11 w-11 shrink-0 items-center justify-center overflow-hidden rounded-lg bg-surface-2">
        {image ? (
          <img src={image} alt="" className="h-full w-full object-cover" />
        ) : (
          <Sparkles size={20} className="text-accent" aria-hidden />
        )}
      </span>
      <span className="min-w-0">
        <span className="block truncate font-medium text-fg group-hover:text-accent">
          {skill.name || skill.skillId}
        </span>
        <span className="mt-0.5 block text-xs text-fg-subtle">
          {skill.manaCost} mana · {skill.cooldown}s CD
        </span>
      </span>
    </>
  );
}

/**
 * One node on the branch. The short horizontal stub on the left is what visually ties the node
 * back to its tier's trunk, so the row reads as connected rather than as a loose grid of cards.
 */
function SkillNode({ skill, onSelect, onDelete, href }: {
  skill: SkillResponse;
  onSelect?: (skill: SkillResponse) => void;
  onDelete?: (skill: SkillResponse) => void;
  href?: string;
}) {
  const shell =
    "group flex items-center gap-3 rounded-lg border border-border bg-surface-2 p-2.5 pr-4 text-left " +
    "transition-colors hover:border-accent/50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent";

  return (
    <span className="flex items-center">
      <span className="h-px w-3 bg-border sm:w-4" aria-hidden />
      {onSelect ? (
        <button type="button" onClick={() => onSelect(skill)} className={`${shell} w-56 max-w-full`}>
          <NodeBody skill={skill} />
        </button>
      ) : href ? (
        <Link href={href} className={`${shell} w-56 max-w-full`}>
          <NodeBody skill={skill} />
        </Link>
      ) : (
        <span className={`${shell} w-56 max-w-full`}>
          <NodeBody skill={skill} />
        </span>
      )}
      {onDelete && (
        <button
          type="button"
          onClick={() => onDelete(skill)}
          aria-label={`Delete ${skill.name || skill.skillId}`}
          title="Delete skill"
          className="ml-1 flex h-7 w-7 shrink-0 items-center justify-center rounded-md text-fg-subtle transition-colors hover:bg-danger/10 hover:text-danger"
        >
          <Trash2 size={14} />
        </button>
      )}
    </span>
  );
}
