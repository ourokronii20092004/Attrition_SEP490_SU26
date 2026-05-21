import type { Metadata } from "next";
import { Inter, Plus_Jakarta_Sans, JetBrains_Mono } from "next/font/google";
import { ThemeProvider } from "@/contexts/ThemeContext";
import { ToastProvider } from "@/contexts/ToastContext";
import { AuthProvider } from "@/contexts/AuthContext";
import { PlayerProvider } from "@/contexts/PlayerContext";
import ClientLayout from "@/components/ClientLayout";
import "./globals.css";

const inter = Inter({ subsets: ["latin"], variable: "--font-inter", display: "swap" });
const plusJakarta = Plus_Jakarta_Sans({ subsets: ["latin"], variable: "--font-jakarta", display: "swap" });
const jetbrainsMono = JetBrains_Mono({ subsets: ["latin"], variable: "--font-jetbrains", display: "swap" });

export const metadata: Metadata = {
  title: {
    default: "Collection — Attrition OST",
    template: "%s | Collection",
  },
  description: "Listen to the Attrition original soundtrack — atmospheric compositions from a dark-fantasy world.",
};

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
    var colors = {ember:'#e85d3a',soul:'#6366f1',gold:'#d4a053',verdant:'#10b981',crimson:'#dc2626',frost:'#06b6d4'};
    document.documentElement.style.setProperty('--accent', colors[accent] || colors.ember);
  } catch(e) {}
})();
`;

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html
      lang="en"
      suppressHydrationWarning
      className={`${inter.variable} ${plusJakarta.variable} ${jetbrainsMono.variable}`}
    >
      <head>
        <meta name="theme-color" content="#09090b" />
        <script dangerouslySetInnerHTML={{ __html: themeScript }} />
      </head>
      <body>
        <ThemeProvider>
          <AuthProvider>
            <ToastProvider>
              <PlayerProvider>
                <ClientLayout>{children}</ClientLayout>
              </PlayerProvider>
            </ToastProvider>
          </AuthProvider>
        </ThemeProvider>
      </body>
    </html>
  );
}
