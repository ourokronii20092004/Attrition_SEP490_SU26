'use client';

import { ReactNode } from 'react';
import { PlayerProvider, usePlayer } from '@/contexts/PlayerContext';
import Sidebar from '@/components/Sidebar';
import PlayerBar from '@/components/PlayerBar';

function LayoutInner({ children }: { children: ReactNode }) {
  const { audioRef } = usePlayer();

  return (
    <div className="collection-layout">
      <Sidebar />
      <main className="collection-main">
        <div className="collection-main-inner">{children}</div>
      </main>
      <PlayerBar />
      {/* Hidden audio element controlled by PlayerContext */}
      <audio ref={audioRef} preload="auto" />
    </div>
  );
}

export default function ClientLayout({ children }: { children: ReactNode }) {
  return (
    <PlayerProvider>
      <LayoutInner>{children}</LayoutInner>
    </PlayerProvider>
  );
}
