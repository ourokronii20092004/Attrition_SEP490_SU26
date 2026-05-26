import Navbar from "@/components/layout/Navbar";
import Footer from "@/components/layout/Footer";
import PlayerBar from "@/components/layout/PlayerBar";
import { PlayerProvider } from "@/contexts/PlayerContext";

export default function MainLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <PlayerProvider>
      <Navbar />
      <main id="main-content">{children}</main>
      <Footer />
      <PlayerBar />
    </PlayerProvider>
  );
}
