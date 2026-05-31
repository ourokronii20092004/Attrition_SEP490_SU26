"use client";

import { useState } from "react";
import { ArticlesAdmin } from "./_components/ArticlesAdmin";
import { ContributionQueue } from "./_components/ContributionQueue";
import { CategoriesAdmin } from "./_components/CategoriesAdmin";

type Tab = "articles" | "queue" | "categories";

export default function AdminWikiPage() {
  const [tab, setTab] = useState<Tab>("articles");
  return (
    <div className="mx-auto max-w-6xl">
      <h1 className="font-display text-3xl font-bold text-fg">Wiki Management</h1>
      <div className="mt-4 flex gap-1 border-b border-border">
        {(["articles", "queue", "categories"] as Tab[]).map((t) => (
          <button
            key={t}
            onClick={() => setTab(t)}
            className={`relative px-4 py-2.5 text-sm font-medium capitalize transition-colors ${tab === t ? "text-accent" : "text-fg-muted hover:text-fg"}`}
          >
            {t === "queue" ? "Contribution Queue" : t}
            {tab === t && <span className="absolute inset-x-2 -bottom-px h-0.5 rounded-full bg-accent" />}
          </button>
        ))}
      </div>
      <div className="mt-6">
        {tab === "articles" && <ArticlesAdmin />}
        {tab === "queue" && <ContributionQueue />}
        {tab === "categories" && <CategoriesAdmin />}
      </div>
    </div>
  );
}
