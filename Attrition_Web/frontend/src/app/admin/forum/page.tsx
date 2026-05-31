"use client";

import { useState } from "react";
import { ReportsQueue } from "./_components/ReportsQueue";
import { ThreadsAdmin } from "./_components/ThreadsAdmin";
import { CategoriesAdmin } from "./_components/CategoriesAdmin";

type Tab = "reports" | "threads" | "categories";

export default function AdminForumPage() {
  const [tab, setTab] = useState<Tab>("reports");
  return (
    <div className="mx-auto max-w-6xl">
      <h1 className="font-display text-3xl font-bold text-fg">Forum Management</h1>
      <div className="mt-4 flex gap-1 border-b border-border">
        {(["reports", "threads", "categories"] as Tab[]).map((t) => (
          <button
            key={t}
            onClick={() => setTab(t)}
            className={`relative px-4 py-2.5 text-sm font-medium capitalize transition-colors ${tab === t ? "text-accent" : "text-fg-muted hover:text-fg"}`}
          >
            {t}
            {tab === t && <span className="absolute inset-x-2 -bottom-px h-0.5 rounded-full bg-accent" />}
          </button>
        ))}
      </div>
      <div className="mt-6">
        {tab === "reports" && <ReportsQueue />}
        {tab === "threads" && <ThreadsAdmin />}
        {tab === "categories" && <CategoriesAdmin />}
      </div>
    </div>
  );
}
