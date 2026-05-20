'use client';
import { useState } from 'react';
import Breadcrumb from '@/components/Breadcrumb';
import { api } from '@/lib/api';
import { useToast } from '@/contexts/ToastContext';

export default function ContactPage() {
  const { showToast } = useToast();
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [subject, setSubject] = useState('');
  const [message, setMessage] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim() || !message.trim()) {
      showToast('Please fill in all required fields', 'error');
      return;
    }

    setIsSubmitting(true);
    try {
      await api.post('/api/contact', { name, email, subject, message });
      showToast('Message sent! We\'ll get back to you soon.', 'success');
      setName('');
      setEmail('');
      setSubject('');
      setMessage('');
    } catch {
      showToast('Failed to send message. Please try again.', 'error');
    }
    setIsSubmitting(false);
  };

  return (
    <div className="container">
      <Breadcrumb items={[
        { label: 'Home', href: '/' },
        { label: 'Contact' },
      ]} />

      <div className="glass-card-static" style={{ maxWidth: '600px', margin: '0 auto' }}>
        <h1 className="mb-sm">📬 Contact Us</h1>
        <p className="text-muted mb-xl">
          Have a question, bug report, or suggestion? Drop us a message and we&apos;ll get back to you.
        </p>

        <form className="auth-form" onSubmit={handleSubmit}>
          <div className="input-group">
            <label>Name *</label>
            <input
              className="input"
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="Your name"
              required
            />
          </div>

          <div className="input-group">
            <label>Email</label>
            <input
              className="input"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="your@email.com (optional)"
            />
          </div>

          <div className="input-group">
            <label>Subject</label>
            <input
              className="input"
              type="text"
              value={subject}
              onChange={(e) => setSubject(e.target.value)}
              placeholder="What is this about?"
            />
          </div>

          <div className="input-group">
            <label>Message *</label>
            <textarea
              className="input"
              value={message}
              onChange={(e) => setMessage(e.target.value)}
              placeholder="Your message..."
              rows={6}
              required
            />
          </div>

          <button type="submit" className="btn btn-primary" disabled={isSubmitting}>
            {isSubmitting ? 'Sending...' : '📤 Send Message'}
          </button>
        </form>
      </div>
    </div>
  );
}