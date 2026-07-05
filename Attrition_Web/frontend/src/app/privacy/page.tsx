import type { Metadata } from "next";
import Link from "next/link";
import { PageShell } from "@/components/ui/page-shell";
import { PageTitle } from "@/components/ui/page-title";

export const metadata: Metadata = {
  title: "Privacy Policy",
  description: "How Attrition collects, uses, and protects your personal data under Vietnamese law.",
};

export default function PrivacyPage() {
  return (
    <PageShell size="md">
      <PageTitle eyebrow="Legal" description="Last updated 4 July 2026.">
        Privacy Policy
      </PageTitle>

      <div className="prose-content">
        <p>
          This Privacy Policy explains how the Attrition companion platform (the &ldquo;Platform&rdquo;,
          &ldquo;we&rdquo;, &ldquo;us&rdquo;, or &ldquo;our&rdquo;) collects, uses, stores, discloses, and
          protects your personal data. Attrition is a non-commercial student capstone project
          (FPT University, course SEP490, Summer 2026) built around a co-op game set in the world of
          Eldravir. We are committed to processing personal data lawfully and transparently.
        </p>
        <p>
          We process personal data in accordance with the laws of Vietnam, including the Law on
          Personal Data Protection No. 91/2025/QH15 (in force from 1 January 2026), Decree No.
          13/2023/ND-CP and its guiding Decree No. 356/2025/ND-CP, the Law on Data No. 60/2024/QH15,
          the Law on Cyber Security and Decree No. 53/2022/ND-CP, Decree No. 147/2024/ND-CP on the
          management of internet services and online information, the Law on E-Transactions No.
          20/2023/QH15, and the Civil Code No. 91/2015/QH13.
        </p>

        <h2>1. Who is responsible for your data</h2>
        <p>
          The data controller is the Attrition project team. If you have any question or request
          regarding your personal data, contact us at <a href="mailto:privacy@attrition.io.vn">privacy@attrition.io.vn</a>{" "}
          or through the community <Link href="/forum">forum</Link>. We aim to respond within the
          time limits set by the Law on Personal Data Protection.
        </p>

        <h2>2. Personal data we collect</h2>
        <ul>
          <li><strong>Account data:</strong> your username, email address, and a securely hashed password; or, if you sign in with Google, your Google account identifier, email, and public profile name and avatar.</li>
          <li><strong>Profile data:</strong> the display name, avatar, cover image, biography, theme preferences, and notification settings you choose to provide.</li>
          <li><strong>Content and activity:</strong> forum threads, posts, replies, reactions, reports; wiki articles and suggested edits; music play activity; and character progress synced from the game client.</li>
          <li><strong>Technical and security data:</strong> IP address, sign-in timestamps, failed-login and account-lock records, session and CSRF cookies, and basic device/browser information contained in ordinary web requests.</li>
        </ul>
        <p>
          We do not intentionally collect &ldquo;sensitive personal data&rdquo; as defined by the Law on
          Personal Data Protection (such as health, religious, political, biometric, or financial data).
          Please do not submit such data through the Platform.
        </p>

        <h2>3. How and why we use your data, and our legal basis</h2>
        <ul>
          <li><strong>To provide the service</strong> (create and operate your account, authenticate sessions, display your contributions, sync characters) &mdash; on the basis of performing the service you request and your consent.</li>
          <li><strong>To send essential account email</strong> (email verification, password resets, and important notices) via our email provider &mdash; necessary to operate your account.</li>
          <li><strong>To keep the Platform safe</strong> (rate limiting, abuse prevention, moderation, and enforcing bans) &mdash; on the basis of our legitimate interest in security and a lawful community, and to comply with legal obligations.</li>
          <li><strong>To comply with the law</strong> and respond to lawful requests from competent State authorities.</li>
        </ul>

        <h2>4. Consent and its withdrawal</h2>
        <p>
          Where we rely on your consent, you give it when you create an account and accept this policy.
          You may withdraw consent at any time by adjusting your settings, deleting your account, or
          contacting us. Withdrawal does not affect processing carried out before withdrawal, and some
          data may be retained where the law permits or requires it.
        </p>

        <h2>5. Sharing and disclosure</h2>
        <p>We do not sell your personal data. We share it only with:</p>
        <ul>
          <li><strong>Service providers acting on our behalf</strong>, such as Google (for sign-in), our email/SMTP provider (for account email), and our hosting and network/content-delivery providers (including Cloudflare).</li>
          <li><strong>Competent State authorities</strong>, where disclosure is required by Vietnamese law or by a valid legal request.</li>
        </ul>
        <p>Contributions you post (such as forum and wiki content, and your public profile) are, by their nature, visible to other users and the public.</p>

        <h2>6. Cross-border transfers</h2>
        <p>
          Some of our service providers may process data on servers located outside Vietnam. Where a
          cross-border transfer of personal data occurs, we rely on your consent and apply the measures
          required by the Law on Personal Data Protection, including maintaining the required transfer
          impact-assessment records.
        </p>

        <h2>7. Cookies and similar technologies</h2>
        <ul>
          <li><strong>Essential cookies:</strong> secure, HTTP-only session cookies and a CSRF-protection cookie that are required to sign you in and keep your session safe.</li>
          <li><strong>Local preferences:</strong> small values stored in your browser to remember choices such as your theme.</li>
        </ul>
        <p>We do not use advertising or third-party tracking cookies. Blocking essential cookies will prevent you from signing in.</p>

        <h2>8. How long we keep your data</h2>
        <ul>
          <li><strong>Account data</strong> is kept while your account is active. When you delete your account, it enters a short recovery window (about 90 days) and is then permanently removed or anonymized.</li>
          <li><strong>Public contributions</strong> may remain part of the shared archive after account deletion, in de-identified form, to preserve the integrity of discussions and articles.</li>
          <li><strong>Security logs</strong> are kept only as long as needed for security and legal compliance.</li>
        </ul>

        <h2>9. Security</h2>
        <p>
          We apply reasonable technical and organizational measures to protect personal data, including
          password hashing, encrypted (HTTPS/TLS) transport, access controls, and CSRF protection. No
          system is perfectly secure. If a personal-data breach occurs, we will notify the competent
          State authority (the specialized cyber-security unit of the Ministry of Public Security) and
          affected users within the time limits and in the manner required by law.
        </p>

        <h2>10. Your rights</h2>
        <p>Subject to the Law on Personal Data Protection, you have the right to:</p>
        <ul>
          <li>be informed about how your data is processed;</li>
          <li>give, and withdraw, your consent;</li>
          <li>access your data and obtain a copy;</li>
          <li>correct inaccurate data;</li>
          <li>request deletion of your data;</li>
          <li>restrict or object to certain processing;</li>
          <li>request portability of the data you provided;</li>
          <li>complain to, or lodge a denunciation with, the competent authority, and to initiate legal proceedings and claim compensation for damage.</li>
        </ul>
        <p>
          You can exercise many of these rights directly in <Link href="/settings">Settings</Link> (update your
          profile, or delete your account). For other requests, contact us at{" "}
          <a href="mailto:privacy@attrition.io.vn">privacy@attrition.io.vn</a>. We may need to verify your identity first.
        </p>

        <h2>11. Children</h2>
        <p>
          Under Vietnamese law a child is a person under 16 years of age. The Platform is not directed at
          children under 16. If you are under 16, please use the Platform only with the consent and
          supervision of a parent or guardian. We do not knowingly collect the personal data of a child
          without the consent required by law; if we learn that we have, we will delete it.
        </p>

        <h2>12. Changes to this policy</h2>
        <p>
          We may update this policy from time to time. We will change the &ldquo;last updated&rdquo; date above
          and, for material changes, provide notice through the Platform. Continued use after an update
          means you have read the revised policy.
        </p>

        <h2>13. Complaints</h2>
        <p>
          If you believe your data has been handled unlawfully, please contact us first so we can help.
          You also have the right to lodge a complaint with the competent State authority responsible
          for personal data protection in Vietnam.
        </p>

        <p className="text-sm text-fg-subtle">
          Attrition is a non-commercial, educational student project provided as-is. This policy is
          written to reflect Vietnamese law as of the date above; it is not legal advice.
        </p>
      </div>
    </PageShell>
  );
}
