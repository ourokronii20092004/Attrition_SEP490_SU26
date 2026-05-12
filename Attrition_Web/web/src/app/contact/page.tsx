import GlassCard from '@/components/GlassCard';
import Button from '@/components/Button';
import Input from '@/components/Input';
import { FiMail, FiMessageSquare, FiTwitter } from 'react-icons/fi';

export default function Contact() {
  return (
    <div className="container" style={{ padding: 'var(--space-2xl) 0', maxWidth: '800px' }}>
      <h1 style={{ marginBottom: 'var(--space-xl)', textAlign: 'center' }}>Contact Us</h1>
      
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))', gap: 'var(--space-lg)' }}>
        <GlassCard>
          <h2 style={{ marginBottom: 'var(--space-md)' }}>Get in Touch</h2>
          <p style={{ color: 'var(--text-secondary)', marginBottom: 'var(--space-lg)' }}>
            Have a question, feedback, or business inquiry? We'd love to hear from you.
          </p>
          
          <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-md)' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-sm)' }}>
              <FiMail style={{ color: 'var(--accent)' }} size={24} />
              <span>hello@attrition-game.com</span>
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-sm)' }}>
              <FiMessageSquare style={{ color: 'var(--accent)' }} size={24} />
              <a href="#" style={{ color: 'var(--text-primary)' }}>Join our Discord Server</a>
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-sm)' }}>
              <FiTwitter style={{ color: 'var(--accent)' }} size={24} />
              <a href="#" style={{ color: 'var(--text-primary)' }}>@AttritionGame</a>
            </div>
          </div>
        </GlassCard>

        <GlassCard>
          <h2 style={{ marginBottom: 'var(--space-md)' }}>Send a Message</h2>
          <form style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-md)' }}>
            <Input label="Name" placeholder="Your name" />
            <Input label="Email" type="email" placeholder="Your email address" />
            <Input label="Message" multiline placeholder="How can we help?" />
            <Button style={{ marginTop: 'var(--space-sm)' }}>Send Message</Button>
          </form>
        </GlassCard>
      </div>
    </div>
  );
}