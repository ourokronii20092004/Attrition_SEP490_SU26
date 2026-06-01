import { redirect } from "next/navigation";

// Forum management is split into sidebar routes (Reports / Threads / Categories) — UIBD-7.
export default function AdminForumPage() {
  redirect("/admin/forum/reports");
}
