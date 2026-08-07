"use client";

import { Suspense } from "react";

import { ReportsWorkspace } from "../../_components/ReportsWorkspace";
import { ReportsQueue } from "../_components/ReportsQueue";

export default function AdminForumReportsPage() {
  // Post reports and user reports share one tabbed workspace; this route presets the posts tab.
  // The list keeps its page in the URL, which needs a Suspense boundary.
  return (
    <Suspense fallback={null}>
      <ReportsWorkspace tab="posts">
        <ReportsQueue />
      </ReportsWorkspace>
    </Suspense>
  );
}
