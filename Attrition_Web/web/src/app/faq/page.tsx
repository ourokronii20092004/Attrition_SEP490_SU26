'use client';
import { useState } from 'react';
import GlassCard from '@/components/GlassCard';
import { FiChevronDown, FiChevronUp } from 'react-icons/fi';

const faqs = [
  {
    q: 'What is Attrition?',
    a: 'Attrition is a 2D roguelike action platformer featuring procedural generation, tight combat, and a unique integrated multiplayer system for both co-op and PvP.'
  },
  {
    q: 'When will the game be released?',
    a: 'We are currently in alpha testing. We plan to release into Early Access in Q2 2026.'
  },
  {
    q: 'How does multiplayer work?',
    a: 'Players can drop a "summon sign" in specific safe rooms between biomes. Friends or random players can interact with these signs to join your world for co-op. PvP happens in dedicated, separate arena instances accessible from the hub area.'
  },
  {
    q: 'What platforms will the game be available on?',
    a: 'Initially PC (Windows/Linux via Steam), with plans for console ports (Switch, PS5, Xbox Series X/S) post-launch.'
  },
  {
    q: 'Who can edit the wiki?',
    a: 'Any registered user can suggest edits to wiki articles. These suggestions go into a pending queue where our community moderators and admins review and approve them.'
  },
  {
    q: 'I found a bug, where do I report it?',
    a: 'Please use the "Bug Reports" category in the community forum. Make sure to check if someone else has already reported it first!'
  }
];

export default function FAQ() {
  const [openIndex, setOpenIndex] = useState<number | null>(0);

  return (
    <div className="container" style={{ padding: 'var(--space-2xl) 0', maxWidth: '800px' }}>
      <h1 style={{ marginBottom: 'var(--space-xl)', textAlign: 'center' }}>Frequently Asked Questions</h1>
      
      <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-md)' }}>
        {faqs.map((faq, index) => (
          <GlassCard key={index} style={{ padding: 'var(--space-md)' }}>
            <button
              onClick={() => setOpenIndex(openIndex === index ? null : index)}
              style={{
                width: '100%', display: 'flex', justifyContent: 'space-between', alignItems: 'center',
                background: 'none', border: 'none', cursor: 'pointer', color: 'var(--text-primary)',
                fontFamily: 'inherit', fontSize: '1.1rem', fontWeight: 600, textAlign: 'left'
              }}
            >
              {faq.q}
              {openIndex === index ? <FiChevronUp /> : <FiChevronDown />}
            </button>
            
            {openIndex === index && (
              <p style={{ marginTop: 'var(--space-md)', color: 'var(--text-secondary)', lineHeight: 1.6 }}>
                {faq.a}
              </p>
            )}
          </GlassCard>
        ))}
      </div>
    </div>
  );
}