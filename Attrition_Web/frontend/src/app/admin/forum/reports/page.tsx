"use client";

import { Suspense } from "react";

import { ReportsQueue } from "../_components/ReportsQueue";

export default function AdminForumReportsPage() {
  // The list keeps its page in the URL, which needs a Suspense boundary.
  return (
    <Suspense fallback={null}>
      <ReportsQueue />
    </Suspense>
  );
}
