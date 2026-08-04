"use client";

import { Suspense } from "react";

import { CategoriesAdmin } from "../_components/CategoriesAdmin";

export default function AdminForumCategoriesPage() {
  // The list keeps its page + filters in the URL, which needs a Suspense boundary.
  return (
    <Suspense fallback={null}>
      <CategoriesAdmin />
    </Suspense>
  );
}
