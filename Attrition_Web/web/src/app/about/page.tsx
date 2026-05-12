import GlassCard from '@/components/GlassCard';

export default function About() {
  return (
    <div className="container" style={{ padding: 'var(--space-2xl) 0' }}>
      <h1 style={{ marginBottom: 'var(--space-xl)', textAlign: 'center' }}>About Attrition</h1>
      
      <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-xl)' }}>
        <GlassCard>
          <h2>The Game</h2>
          <p style={{ marginTop: 'var(--space-md)', color: 'var(--text-secondary)' }}>
            Attrition is a challenging 2D roguelike action platformer inspired by classics like Dead Cells and Hollow Knight. 
            Players explore a vast, procedurally generated world full of branching paths, secrets, and deadly bosses. 
            Unlike traditional roguelikes, Attrition features a unique drop-in multiplayer system allowing you to 
            team up with friends or challenge others in PvP arenas seamlessly integrated into the world.
          </p>
          <p style={{ marginTop: 'var(--space-sm)', color: 'var(--text-secondary)' }}>
            With dozens of weapons, hundreds of modifiers, and an intricate skill tree, no two runs will ever be exactly alike.
          </p>
        </GlassCard>

        <GlassCard>
          <h2>Key Features</h2>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: 'var(--space-md)', marginTop: 'var(--space-md)' }}>
            <div>
              <h3>🎲 Procedural Dungeons</h3>
              <p style={{ fontSize: '14px', color: 'var(--text-secondary)' }}>A new castle every run.</p>
            </div>
            <div>
              <h3>🤝 Multiplayer Co-op</h3>
              <p style={{ fontSize: '14px', color: 'var(--text-secondary)' }}>Drop-in, drop-out support.</p>
            </div>
            <div>
              <h3>⚔️ Boss Fights</h3>
              <p style={{ fontSize: '14px', color: 'var(--text-secondary)' }}>Challenging, pattern-based combat.</p>
            </div>
            <div>
              <h3>🔮 Build Variety</h3>
              <p style={{ fontSize: '14px', color: 'var(--text-secondary)' }}>Synergize weapons and relics.</p>
            </div>
            <div>
              <h3>🌳 Skill Trees</h3>
              <p style={{ fontSize: '14px', color: 'var(--text-secondary)' }}>Persistent progression system.</p>
            </div>
            <div>
              <h3>🧩 Environmental Puzzles</h3>
              <p style={{ fontSize: '14px', color: 'var(--text-secondary)' }}>Use movement to uncover secrets.</p>
            </div>
          </div>
        </GlassCard>

        <GlassCard>
          <h2>The Team</h2>
          <p style={{ marginTop: 'var(--space-md)', color: 'var(--text-secondary)' }}>
            We are a small indie team dedicated to creating the best 2D action experience possible.
          </p>
          <ul style={{ marginTop: 'var(--space-sm)', paddingLeft: 'var(--space-lg)', color: 'var(--text-secondary)' }}>
            <li><strong>Alex Vance</strong> — Lead Designer & Programmer</li>
            <li><strong>Sarah Chen</strong> — Art Director & Animator</li>
            <li><strong>Marcus Cole</strong> — Audio Engineer & Composer</li>
          </ul>
        </GlassCard>
      </div>
    </div>
  );
}