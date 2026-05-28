import type { Metadata } from "next";
import { Inter, Plus_Jakarta_Sans, JetBrains_Mono } from "next/font/google";
import { ThemeProvider } from "@/contexts/ThemeContext";
import { ToastProvider } from "@/contexts/ToastContext";
import { AuthProvider } from "@/contexts/AuthContext";
import "./globals.css";


// ─── Fonts ─────────────────────────────────────────────────

const inter = Inter({
  subsets: ["latin"],
  variable: "--font-inter",
  display: "swap",
});

const plusJakarta = Plus_Jakarta_Sans({
  subsets: ["latin"],
  variable: "--font-jakarta",
  display: "swap",
});

const jetbrainsMono = JetBrains_Mono({
  subsets: ["latin"],
  variable: "--font-jetbrains",
  display: "swap",
});

// ─── Metadata ──────────────────────────────────────────────

export const metadata: Metadata = {
  title: {
    default: "Attrition — A 2D Co-op Souls-like ARPG",
    template: "%s | Attrition",
  },
  description:
    "Attrition is a dark-fantasy 2D cooperative souls-like ARPG. Explore interconnected worlds, defeat formidable bosses, and uncover the truth with a partner.",
  keywords: [
    "Attrition",
    "2D ARPG",
    "souls-like",
    "co-op",
    "indie game",
    "dark fantasy",
    "roguelike",
  ],
  authors: [{ name: "SEP490 Team 21 — FPT University" }],
  openGraph: {
    type: "website",
    locale: "en_US",
    siteName: "Attrition",
    title: "Attrition — A 2D Co-op Souls-like ARPG",
    description: "Explore interconnected worlds, defeat formidable bosses, and uncover the truth.",
  },
};

// ─── Theme Script ──────────────────────────────────────────
// Inline script that runs before React hydrates to prevent
// flash of wrong theme (FOWT). Sets data-theme on <html>
// before any CSS is applied.

const themeScript = `
(function() {
  try {
    function getCookie(name) {
      var match = document.cookie.match(new RegExp('(^| )' + name + '=([^;]+)'));
      return match ? match[2] : null;
    }
    var mode = getCookie('attrition-theme-mode') || localStorage.getItem('attrition-theme-mode') || 'system';
    var resolved = mode;
    if (mode === 'system') {
      resolved = window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    }
    document.documentElement.setAttribute('data-theme', resolved);
    
    var accent = getCookie('attrition-accent') || localStorage.getItem('attrition-accent') || 'ember';
    var colors = {
      ember: '#e85d3a', soul: '#6366f1', gold: '#d4a053',
      verdant: '#10b981', crimson: '#dc2626', frost: '#06b6d4'
    };
    var hex = colors[accent] || colors.ember;
    document.documentElement.style.setProperty('--accent', hex);
  } catch(e) {}
})();
`;

// ─── Root Layout ───────────────────────────────────────────

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html
      lang="en"
      suppressHydrationWarning
      className={`${inter.variable} ${plusJakarta.variable} ${jetbrainsMono.variable}`}
    >
      <head>
        <meta name="theme-color" content="#f7f7f8" />
        <script dangerouslySetInnerHTML={{ __html: themeScript }} />
      </head>
      <body>
        <a href="#main-content" className="skip-to-content">
          Skip to content
        </a>
        <ThemeProvider>
          <AuthProvider>
            <ToastProvider>
              {children}
            </ToastProvider>
          </AuthProvider>
        </ThemeProvider>
      </body>
    </html>
  );
}
