import { redirect } from "next/navigation";

// Music management is split into Albums and Tracks pages (each its own list) — packing both big
// lists onto one page is not allowed. Land on Albums by default.
export default function AdminMusicPage() {
  redirect("/admin/music/albums");
}
