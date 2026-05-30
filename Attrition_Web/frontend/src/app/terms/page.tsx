import type { Metadata } from "next";
import { PageShell } from "@/components/ui/page-shell";
import { PageTitle } from "@/components/ui/page-title";

export const metadata: Metadata = {
  title: "Terms of Service",
  description: "The terms governing use of the Attrition companion platform.",
};

export default function TermsPage() {
  return (
    <PageShell size="md">
      <PageTitle eyebrow="Legal" description="Last updated 30 May 2026.">
        Terms of Service
      </PageTitle>

      <div className="prose-content">
        <p>
          By using the Attrition companion platform you agree to these terms. Attrition is a
          student capstone project provided as-is, without warranty, for educational and
          community purposes.
        </p>

        <h2>Accounts</h2>
        <ul>
          <li>You are responsible for activity under your account and for keeping your credentials secure.</li>
          <li>One person per account; do not impersonate others or share login details.</li>
        </ul>

        <h2>Community conduct</h2>
        <ul>
          <li>Wiki and forum contributions must be lawful, on-topic, and respectful.</li>
          <li>No harassment, spam, or malicious content. Moderators may remove content or ban accounts that breach these rules.</li>
          <li>Contributions you submit may be edited, displayed, and retained as part of the shared archive.</li>
        </ul>

        <h2>Availability</h2>
        <ul>
          <li>The service may change or pause without notice; it is not guaranteed to be available at all times.</li>
        </ul>

        <h2>Changes</h2>
        <p>These terms may be updated; continued use after changes constitutes acceptance.</p>
      </div>
    </PageShell>
  );
}
