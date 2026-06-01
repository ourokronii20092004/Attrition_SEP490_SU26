"use client";

import { Card } from "@/components/ui/card";

export function SettingsCard({ title, children, danger }: { title: string; children: React.ReactNode; danger?: boolean }) {
  return (
    <Card className="p-5 sm:p-6">
      <h2 className={`text-lg font-semibold ${danger ? "text-danger" : "text-fg"}`}>{title}</h2>
      <div className="mt-4">{children}</div>
    </Card>
  );
}
