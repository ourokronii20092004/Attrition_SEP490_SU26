import Link from 'next/link';
import { FiExternalLink, FiMusic, FiDisc } from 'react-icons/fi';

export default function AboutPage() {
  return (
    <div className="about-page animate-fade-in-up">
      <h1>About Collection</h1>

      <p>
        <strong>Collection</strong> is the official music portal for{' '}
        <em>Attrition</em> — a dark fantasy souls-like roguelike 2D co-op game.
        Here you can explore and listen to the original soundtrack that
        accompanies your journey through the abyss.
      </p>

      <p>
        Each album represents a chapter in the Attrition universe — from the
        haunting ambience of the Void Wastes to the intense battle themes of the
        Ember Citadel. The music is composed to immerse you in the atmosphere of
        the game, whether you&apos;re strategizing with your co-op partner or facing
        a relentless boss alone.
      </p>

      <p>
        Login with your Attrition account to save your favorite tracks and keep
        your personal playlist across sessions. Your listening experience
        seamlessly connects with the main game platform.
      </p>

      <div className="about-links">
        <a
          href="https://attrition.hault.io.vn"
          className="btn btn-primary btn-round"
          target="_blank"
          rel="noopener noreferrer"
        >
          <FiExternalLink /> Visit Main Site
        </a>
        <Link href="/albums" className="btn btn-secondary btn-round">
          <FiDisc /> Browse Albums
        </Link>
        <Link href="/" className="btn btn-ghost btn-round">
          <FiMusic /> Home
        </Link>
      </div>
    </div>
  );
}