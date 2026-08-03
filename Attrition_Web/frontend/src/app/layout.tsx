import type { Metadata } from "next";
// Self-hosted fonts via @fontsource (woff2 bundled in node_modules) — no build-time fetch to
// fonts.gstatic.com, which is blocked on the deploy network and warned on every build.
import "@fontsource-variable/inter";
import "@fontsource-variable/plus-jakarta-sans";
import "@fontsource-variable/jetbrains-mono";
import "./globals.css";
import { Providers } from "@/components/providers";
import { AppFrame } from "@/components/app-frame";
import { ForcePasswordChange } from "@/components/force-password-change";
import { AuthTransition } from "@/components/auth-transition";
import { SITE_NAME, SITE_TAGLINE } from "@/lib/config";

export const metadata: Metadata = {
  title: { default: SITE_NAME, template: `%s | ${SITE_NAME}` },
  description: SITE_TAGLINE,
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" data-theme="light" data-accent="ember" suppressHydrationWarning>
      <body className="font-sans antialiased">
        {/* Apply the saved theme before first paint so nothing (incl. the boot splash) flashes the
            default dark/corruption theme before ThemeProvider's effects run. Mirrors the keys in
            theme-provider.tsx. */}
        <script
          dangerouslySetInnerHTML={{
            __html:
              "(function(){try{var r=document.documentElement;r.setAttribute('data-theme',localStorage.getItem('attrition:themeMode')||'light');r.setAttribute('data-accent',localStorage.getItem('attrition:themeAccent')||'ember');}catch(e){}})();",
          }}
        />
        <Providers>
          <a
            href="#main-content"
            className="sr-only focus:not-sr-only focus:fixed focus:left-4 focus:top-4 focus:z-[600] focus:rounded-lg focus:bg-accent focus:px-4 focus:py-2 focus:font-medium focus:text-accent-fg"
          >
            Skip to content
          </a>
          <AppFrame>{children}</AppFrame>
          <ForcePasswordChange />
          <AuthTransition />
        </Providers>
      </body>
    </html>
  );
}
