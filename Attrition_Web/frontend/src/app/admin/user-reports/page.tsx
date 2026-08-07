"use client";

import { Suspense } from "react";

import { ReportsWorkspace } from "../_components/ReportsWorkspace";
import { UserReportsQueue } from "./_components/UserReportsQueue";

export default function AdminUserReportsPage() {
  // Same tabbed workspace as /admin/forum/reports; this route presets the users tab.
  return (
    <Suspense fallback={null}>
      <ReportsWorkspace tab="users">
        <UserReportsQueue />
      </ReportsWorkspace>
    </Suspense>
  );
}
