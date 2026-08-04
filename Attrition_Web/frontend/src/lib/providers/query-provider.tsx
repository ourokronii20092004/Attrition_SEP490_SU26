"use client";

import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useState, type ReactNode } from "react";

export function QueryProvider({ children }: { children: ReactNode }) {
  const [client] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            staleTime: 60_000,
            retry: 1,
            // Live updates, part 1: refresh on returning to the tab and on regaining a
            // connection. This alone makes every page in the app self-updating, without each
            // call site opting in — coming back to a tab never shows a stale screen.
            // Pages whose data changes while you watch add an explicit refetchInterval from
            // lib/live.ts on top of this.
            refetchOnWindowFocus: true,
            refetchOnReconnect: true,
          },
        },
      }),
  );

  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}
