'use client';
import { useEffect, useRef, useState } from 'react';
import { FiVolume2, FiVolumeX } from 'react-icons/fi';

export default function MusicPlayer() {
  const audioRef = useRef<HTMLAudioElement>(null);
  const [isPlaying, setIsPlaying] = useState(true);
  const [hasInteracted, setHasInteracted] = useState(false);

  useEffect(() => {
    // Load saved preference
    const saved = localStorage.getItem('attrition-music');
    if (saved === 'off') setIsPlaying(false);

    // Autoplay requires user interaction on most browsers.
    // Start playing on first user interaction with the page.
    const handleInteraction = () => {
      if (!hasInteracted) {
        setHasInteracted(true);
        const audio = audioRef.current;
        if (audio && isPlaying) {
          audio.volume = 0.3;
          audio.play().catch(() => {});
        }
        document.removeEventListener('click', handleInteraction);
        document.removeEventListener('keydown', handleInteraction);
      }
    };

    document.addEventListener('click', handleInteraction);
    document.addEventListener('keydown', handleInteraction);

    return () => {
      document.removeEventListener('click', handleInteraction);
      document.removeEventListener('keydown', handleInteraction);
    };
  }, [hasInteracted, isPlaying]);

  useEffect(() => {
    const audio = audioRef.current;
    if (!audio) return;
    audio.volume = 0.3;
    if (isPlaying && hasInteracted) {
      audio.play().catch(() => {});
    } else {
      audio.pause();
    }
  }, [isPlaying, hasInteracted]);

  const toggle = () => {
    const next = !isPlaying;
    setIsPlaying(next);
    setHasInteracted(true);
    localStorage.setItem('attrition-music', next ? 'on' : 'off');
  };

  return (
    <>
      <audio ref={audioRef} src="/audio/friday-night.mp3" loop preload="auto" />
      <button
        onClick={toggle}
        className="music-toggle"
        aria-label={isPlaying ? 'Mute music' : 'Unmute music'}
        title={isPlaying ? 'Mute music' : 'Unmute music'}
      >
        {isPlaying ? <FiVolume2 size={20} /> : <FiVolumeX size={20} />}
      </button>
    </>
  );
}