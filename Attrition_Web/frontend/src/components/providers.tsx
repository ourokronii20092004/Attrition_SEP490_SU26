"use client";

import { QueryProvider, AuthProvider, ThemeProvider, ToastProvider, ConfirmProvider } from "@/lib/providers";

export function Providers({ children }: { children: React.ReactNode }) {
  return (
    <QueryProvider>
      {/* ToastProvider wraps AuthProvider so auth can surface toasts (e.g. "session expired"). */}
      <ToastProvider>
        <AuthProvider>
          <ThemeProvider>
            <ConfirmProvider>{children}</ConfirmProvider>
          </ThemeProvider>
        </AuthProvider>
      </ToastProvider>
    </QueryProvider>
  );
}
