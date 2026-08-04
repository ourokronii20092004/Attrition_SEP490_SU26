"use client";

import { Suspense } from "react";

import { ContributionQueue } from "../_components/ContributionQueue";

export default function AdminWikiQueuePage() {
  // The list keeps its page + filters in the URL, which needs a Suspense boundary.
  return (
    <Suspense fallback={null}>
      <ContributionQueue />
    </Suspense>
  );
}
