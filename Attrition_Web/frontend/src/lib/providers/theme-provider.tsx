"use client";

import { useEffect } from "react";
import { useAuth } from "./auth-provider";

export function ThemeProvider({ children }: { children: React.ReactNode }) {
  const { user } = useAuth();

  useEffect(() => {
    const root = document.documentElement;
    const mode = user?.themeMode ?? "dark";
    const accent = user?.themeAccent ?? "corruption";

    root.setAttribute("data-theme", mode);
    root.setAttribute("data-accent", accent);
  }, [user?.themeMode, user?.themeAccent]);

  return <>{children}</>;
}
