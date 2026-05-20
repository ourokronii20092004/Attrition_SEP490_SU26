import Breadcrumb from '@/components/Breadcrumb';

const TEAM = [
  {
    name: 'Phan Phuc Binh',
    role: 'Lead Client, Game Design, Level Design',
    desc: 'Visionary architect of the Attrition world. Designs levels that test your limits and rewards those who dare to explore.',
    icon: '👑',
  },
  {
    name: 'Nguyen Nhat Dang',
    role: 'Game Logic, Enemy Design',
    desc: 'The mind behind every enemy encounter. Crafts AI behaviors and combat systems that demand mastery.',
    icon: '⚔️',
  },
  {
    name: 'Tran Thien Dang',
    role: 'Quality Assurance',
    desc: 'Guardian of game quality. Ensures every mechanic, quest, and feature meets the highest standards.',
    icon: '🛡️',
  },
  {
    name: 'Le Trung Hau',
    role: 'Backend, Frontend, Game Server',
    desc: 'Full-stack engineer powering the entire infrastructure — from the web platform to real-time multiplayer servers.',
    icon: '🔥',
  },
];

export default function AboutPage() {
  return (
    <div className="container">
      <Breadcrumb items={[
        { label: 'Home', href: '/' },
        { label: 'About' },
      ]} />

      {/* About the Game */}
      <section className="mb-2xl">
        <div className="glass-card-static" style={{ maxWidth: '800px', margin: '0 auto', textAlign: 'center', padding: 'var(--space-2xl)' }}>
          <h1 className="mb-md">About Attrition</h1>
          <p className="text-muted" style={{ fontSize: '16px', lineHeight: '1.8', marginBottom: 'var(--space-lg)' }}>
            Attrition is a souls-like, roguelike 2D cooperative action game set in a dark fantasy world where
            ancient evils stir beneath crumbling kingdoms. Players must navigate procedurally generated
            dungeons, master challenging combat systems, and work together to overcome nightmarish
            bosses that guard the secrets of the abyss.
          </p>
          <p className="text-muted" style={{ fontSize: '16px', lineHeight: '1.8' }}>
            Every run is different. Every death teaches. Every victory is earned. Attrition rewards
            persistence, skill, and cooperation — the bonds forged in darkness are the strongest of all.
          </p>
        </div>
      </section>

      {/* Key Features */}
      <section className="mb-2xl">
        <h2 className="text-center mb-xl">Key Features</h2>
        <div className="features-grid">
          <div className="feature-card">
            <span className="feature-icon">🗡️</span>
            <h3>Souls-like Combat</h3>
            <p>Precise, punishing combat where timing and positioning matter. Every weapon has unique movesets and upgrade paths.</p>
          </div>
          <div className="feature-card">
            <span className="feature-icon">🎲</span>
            <h3>Roguelike Runs</h3>
            <p>Procedurally generated levels ensure no two runs are the same. Discover new paths, enemies, and rewards each time.</p>
          </div>
          <div className="feature-card">
            <span className="feature-icon">👥</span>
            <h3>Co-op Multiplayer</h3>
            <p>Team up with friends to tackle the hardest content. Coordination and strategy are key to survival.</p>
          </div>
        </div>
      </section>

      {/* Dev Team */}
      <section className="mb-2xl">
        <h2 className="text-center mb-xl">🔥 The Development Team</h2>
        <div className="team-grid">
          {TEAM.map((member) => (
            <div key={member.name} className="team-card">
              <div style={{ fontSize: '2.5rem', marginBottom: 'var(--space-md)' }}>{member.icon}</div>
              <h3>{member.name}</h3>
              <div className="team-role">{member.role}</div>
              <p className="team-desc">{member.desc}</p>
            </div>
          ))}
        </div>
      </section>

      {/* Project Info */}
      <section>
        <div className="cta-banner">
          <h2>SEP490 — Summer 2026</h2>
          <p>Attrition is a capstone project developed as part of the SEP490 course at FPT University.</p>
        </div>
      </section>
    </div>
  );
}