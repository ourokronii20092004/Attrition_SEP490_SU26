"use client";

import { useEffect } from "react";
import { usePathname } from "next/navigation";
import { setAdminPageLabel } from "@/app/admin/admin-routes";

/**
 * Detail pages call this with the resolved entity name (e.g. a username or article title) so the
 * admin breadcrumb and recent-pages chips show a human label instead of a raw GUID/numeric id.
 * Pass null while the name is still loading — it's a no-op until a real value arrives.
 */
export function useAdminPageLabel(label: string | null | undefined) {
  const pathname = usePathname();
  useEffect(() => {
    if (label) setAdminPageLabel(pathname, label);
  }, [pathname, label]);
}
