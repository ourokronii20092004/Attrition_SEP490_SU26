"use client";

import { Suspense } from "react";

import { ThreadsAdmin } from "../_components/ThreadsAdmin";

export default function AdminForumThreadsPage() {
  // The list keeps its page in the URL, which needs a Suspense boundary.
  return (
    <Suspense fallback={null}>
      <ThreadsAdmin />
    </Suspense>
  );
}
