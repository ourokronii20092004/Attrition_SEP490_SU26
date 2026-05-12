import GlassCard from '@/components/GlassCard';

export default function Changelog() {
  return (
    <div className="container" style={{ padding: 'var(--space-2xl) 0' }}>
      <h1 style={{ marginBottom: 'var(--space-xl)', textAlign: 'center' }}>Changelog</h1>
      
      <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-xl)' }}>
        <GlassCard>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 'var(--space-md)' }}>
            <h2 style={{ margin: 0 }}>v0.3.0 — The Multiplayer Update</h2>
            <span style={{ color: 'var(--text-muted)' }}>October 12, 2025</span>
          </div>
          <ul style={{ paddingLeft: 'var(--space-lg)', color: 'var(--text-secondary)' }}>
            <li>Added drop-in multiplayer lobby system</li>
            <li>Introduced 3 new PvP arenas</li>
            <li>Rebalanced the Greatsword and Twin Daggers</li>
            <li>Fixed a bug where players could clip through walls in the Abyssal Depths</li>
          </ul>
        </GlassCard>

        <GlassCard>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 'var(--space-md)' }}>
            <h2 style={{ margin: 0 }}>v0.2.5 — Biome Expansion</h2>
            <span style={{ color: 'var(--text-muted)' }}>August 28, 2025</span>
          </div>
          <ul style={{ paddingLeft: 'var(--space-lg)', color: 'var(--text-secondary)' }}>
            <li>Added new biome: The Weeping Forest</li>
            <li>Introduced 5 new enemy types</li>
            <li>Added the "Root Snare" boss fight</li>
            <li>Improved procedural generation algorithms to prevent dead ends</li>
          </ul>
        </GlassCard>

        <GlassCard>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 'var(--space-md)' }}>
            <h2 style={{ margin: 0 }}>v0.1.0 — Initial Alpha Release</h2>
            <span style={{ color: 'var(--text-muted)' }}>June 1, 2025</span>
          </div>
          <ul style={{ paddingLeft: 'var(--space-lg)', color: 'var(--text-secondary)' }}>
            <li>First playable alpha build available to founders</li>
            <li>Core combat loop and movement mechanics implemented</li>
            <li>3 starting weapons and 1 complete biome</li>
          </ul>
        </GlassCard>
      </div>
    </div>
  );
}