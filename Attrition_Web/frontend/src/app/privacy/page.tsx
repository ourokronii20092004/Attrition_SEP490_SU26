import type { Metadata } from "next";
import { PageShell } from "@/components/ui/page-shell";
import { PageTitle } from "@/components/ui/page-title";

export const metadata: Metadata = {
  title: "Privacy Policy",
  description: "How Attrition collects, uses, and protects your data.",
};

export default function PrivacyPage() {
  return (
    <PageShell size="md">
      <PageTitle eyebrow="Legal" description="Last updated 30 May 2026.">
        Privacy Policy
      </PageTitle>

      <div className="prose-content">
        <p>
          This policy explains what data the Attrition companion platform collects and how it
          is used. Attrition is a student capstone project (FPT University, SEP490 SU2026);
          data is handled accordingly and not sold to third parties.
        </p>

        <h2>Information we collect</h2>
        <ul>
          <li><strong>Account data:</strong> username, email, and a hashed password, or a Google account identifier if you sign in with Google.</li>
          <li><strong>Profile data:</strong> display name, avatar, bio, and theme preferences you choose to set.</li>
          <li><strong>Activity:</strong> wiki contributions, forum posts, and character snapshots synced from the game client.</li>
        </ul>

        <h2>How we use it</h2>
        <ul>
          <li>To operate your account, authenticate sessions, and display your contributions.</li>
          <li>To send essential account email (verification, password resets) via our SMTP provider.</li>
          <li>To enforce community moderation and account bans.</li>
        </ul>

        <h2>Your choices</h2>
        <ul>
          <li>Update your profile and theme any time in <a href="/settings">Settings</a>.</li>
          <li>Delete your account from Settings; this removes your personal data from our records.</li>
        </ul>

        <h2>Contact</h2>
        <p>Questions about this policy can be raised through the community <a href="/forum">forum</a>.</p>
      </div>
    </PageShell>
  );
}
