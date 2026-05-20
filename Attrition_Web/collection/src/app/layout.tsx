import './globals.css';
import { Playfair_Display, JetBrains_Mono } from 'next/font/google';
import { Be_Vietnam_Pro } from 'next/font/google';
import { AuthProvider } from '@/contexts/AuthProvider';
import ClientLayout from '@/components/ClientLayout';

const playfair = Playfair_Display({
  subsets: ['latin', 'vietnamese'],
  weight: ['400', '500', '600', '700'],
  variable: '--font-display',
  display: 'swap',
});

const beVietnam = Be_Vietnam_Pro({
  subsets: ['latin', 'vietnamese'],
  weight: ['300', '400', '500', '600'],
  variable: '--font-body',
  display: 'swap',
});

const jetbrains = JetBrains_Mono({
  subsets: ['latin'],
  weight: ['400', '500'],
  variable: '--font-mono',
  display: 'swap',
});

export const metadata = {
  title: 'Collection — Attrition OST',
  description:
    'Listen to the original soundtrack of Attrition. A dark fantasy souls-like roguelike 2D co-op experience.',
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="en" suppressHydrationWarning>
      <body
        className={`${playfair.variable} ${beVietnam.variable} ${jetbrains.variable}`}
      >
        <AuthProvider>
          <ClientLayout>{children}</ClientLayout>
        </AuthProvider>
      </body>
    </html>
  );
}