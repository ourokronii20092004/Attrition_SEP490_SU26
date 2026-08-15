"use client";

import { Suspense } from "react";

import { PostsModeration } from "../_components/PostsModeration";

export default function AdminForumPostsPage() {
  // The list keeps its page in the URL, which needs a Suspense boundary.
  return (
    <Suspense fallback={null}>
      <PostsModeration />
    </Suspense>
  );
}
