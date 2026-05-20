import './globals.css';
import { Playfair_Display, JetBrains_Mono } from 'next/font/google';
import { AuthProvider } from '@/contexts/AuthContext';
import { ToastProvider } from '@/contexts/ToastContext';
import Navbar from '@/components/Navbar';
import Footer from '@/components/Footer';
import ToastContainer from '@/components/ToastContainer';

const playfair = Playfair_Display({
  subsets: ['latin', 'vietnamese'],
  variable: '--font-display',
  weight: ['400', '500', '600', '700'],
});

const jetbrains = JetBrains_Mono({
  subsets: ['latin', 'vietnamese'],
  variable: '--font-mono',
  weight: ['400', '500'],
});

export const metadata = {
  title: 'Attrition — 2D Roguelike Multiplayer',
  description: 'The official community hub for Attrition. Explore the wiki, join the forum, and connect with other players.',
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" suppressHydrationWarning>
      <body className={`${playfair.variable} ${jetbrains.variable}`}>
        <div className="ember-particles" />
        <div className="fog-overlay" />
        <AuthProvider>
          <ToastProvider>
            <Navbar />
            <main className="page-content">{children}</main>
            <Footer />
            <ToastContainer />
          </ToastProvider>
        </AuthProvider>
      </body>
    </html>
  );
}