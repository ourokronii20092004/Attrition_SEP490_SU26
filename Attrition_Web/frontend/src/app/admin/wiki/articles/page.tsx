"use client";

import { Suspense } from "react";

import { ArticlesAdmin } from "../_components/ArticlesAdmin";

export default function AdminWikiArticlesPage() {
  // The list keeps its page + filters in the URL, which needs a Suspense boundary.
  return (
    <Suspense fallback={null}>
      <ArticlesAdmin />
    </Suspense>
  );
}
