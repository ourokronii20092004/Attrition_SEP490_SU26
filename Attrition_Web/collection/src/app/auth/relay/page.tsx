'use client';

import { Suspense } from 'react';
import RelayHandler from './RelayHandler';

export default function AuthRelayPage() {
  return (
    <div className="relay-page">
      <Suspense
        fallback={
          <div>
            <div className="relay-spinner" />
            <p style={{ color: 'var(--text-secondary)' }}>Authenticating...</p>
          </div>
        }
      >
        <RelayHandler />
      </Suspense>
    </div>
  );
}
