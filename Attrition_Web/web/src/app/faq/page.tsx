import Breadcrumb from '@/components/Breadcrumb';

const FAQ_ITEMS = [
  {
    q: 'What is Attrition?',
    a: 'Attrition is a souls-like, roguelike 2D cooperative action game set in a dark fantasy world. Players explore procedurally generated dungeons, master challenging combat, and team up to overcome nightmarish bosses.',
  },
  {
    q: 'Is Attrition free to play?',
    a: 'Attrition is currently in development as a capstone project (SEP490). More details about pricing and availability will be announced as we approach release.',
  },
  {
    q: 'How do I contribute to the wiki?',
    a: 'Create an account, then visit the Wiki section and click "Contribute." You can submit edits to existing articles, which will be reviewed by admins before being published.',
  },
  {
    q: 'Can I play Attrition with friends?',
    a: 'Yes! Attrition features co-op multiplayer where you can team up with friends to tackle dungeons and bosses together.',
  },
  {
    q: 'How do I report a bug?',
    a: 'Use the Contact page to send us a bug report, or create a thread in the Bug Reports category on the forum.',
  },
  {
    q: 'What platforms will Attrition be available on?',
    a: 'Attrition is being developed for PC initially. We may expand to other platforms in the future.',
  },
  {
    q: 'How can I join the development team?',
    a: 'Attrition is a capstone project at FPT University. If you\'re interested in contributing, reach out via the Contact page.',
  },
];

export default function FAQPage() {
  return (
    <div className="container">
      <Breadcrumb items={[
        { label: 'Home', href: '/' },
        { label: 'FAQ' },
      ]} />

      <h1 className="mb-sm">❓ Frequently Asked Questions</h1>
      <p className="text-muted mb-2xl">Find answers to common questions about Attrition and the community hub.</p>

      <div className="flex-col gap-md" style={{ maxWidth: '800px' }}>
        {FAQ_ITEMS.map((item, i) => (
          <div key={i} className="glass-card-static">
            <h3 className="mb-sm text-ember">{item.q}</h3>
            <p className="text-muted" style={{ fontSize: '15px', lineHeight: '1.7' }}>{item.a}</p>
          </div>
        ))}
      </div>
    </div>
  );
}