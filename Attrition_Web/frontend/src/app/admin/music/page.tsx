import { redirect } from "next/navigation";

// Music lives under Albums — tracks are managed inside each album's detail page (play, edit,
// upload, delete). Land on the album list directly.
export default function AdminMusicPage() {
  redirect("/admin/music/albums");
}
