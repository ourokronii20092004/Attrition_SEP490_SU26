import { redirect } from "next/navigation";

// Wiki management is split into sidebar routes (Articles / Queue / Categories) — UIBD-7.
export default function AdminWikiPage() {
  redirect("/admin/wiki/articles");
}
