import type { Metadata } from "next";
import Link from "next/link";
import { PageShell } from "@/components/ui/page-shell";
import { PageTitle } from "@/components/ui/page-title";

export const metadata: Metadata = {
  title: "Terms of Service",
  description: "The terms governing use of the Attrition companion platform, under Vietnamese law.",
};

export default function TermsPage() {
  return (
    <PageShell size="md">
      <PageTitle eyebrow="Legal" description="Last updated 4 July 2026.">
        Terms of Service
      </PageTitle>

      <div className="prose-content">
        <p>
          These Terms of Service (the &ldquo;Terms&rdquo;) govern your use of the Attrition companion
          platform (the &ldquo;Platform&rdquo;). Attrition is a non-commercial student capstone project
          (FPT University, course SEP490, Summer 2026), provided as-is for educational and community
          purposes. By creating an account or using the Platform, you agree to these Terms. If you do
          not agree, please do not use the Platform.
        </p>
        <p>
          These Terms are governed by the laws of Vietnam, including the Civil Code No. 91/2015/QH13,
          the Law on Cyber Security, Decree No. 147/2024/ND-CP on the management of internet services
          and online information, the Law on E-Transactions No. 20/2023/QH15, the Law on Protection of
          Consumer Rights No. 19/2023/QH15, and the Law on Intellectual Property.
        </p>

        <h2>1. Eligibility</h2>
        <ul>
          <li>You must have the civil capacity to enter into these Terms under Vietnamese law.</li>
          <li>Under Vietnamese law a child is a person under 16 years of age. If you are under 16, you may use the Platform only with the consent and supervision of a parent or guardian, who accepts these Terms on your behalf.</li>
        </ul>

        <h2>2. Your account</h2>
        <ul>
          <li>Provide accurate information when registering, and keep it up to date.</li>
          <li>You are responsible for all activity under your account and for keeping your credentials secure. Notify us promptly of any unauthorized use.</li>
          <li>One person per account. Do not impersonate any person or organization, or misrepresent your affiliation.</li>
          <li>You may be asked to verify your email address before contributing content.</li>
        </ul>

        <h2>3. Acceptable use and community conduct</h2>
        <p>You agree to use the Platform lawfully and respectfully. You must not post, upload, or transmit content that:</p>
        <ul>
          <li>violates the laws of Vietnam, or opposes the State, or incites violence, disorder, or discrimination;</li>
          <li>is obscene, pornographic, or harmful to minors;</li>
          <li>harasses, threatens, defames, or invades the privacy of any person;</li>
          <li>is false information, spam, fraud, or a scam;</li>
          <li>infringes the intellectual-property or other rights of any third party;</li>
          <li>contains malware, or attempts to gain unauthorized access to, disrupt, overload, or scrape the Platform or its systems.</li>
        </ul>

        <h2>4. Your content</h2>
        <ul>
          <li>You retain ownership of the content you create (forum posts, wiki contributions, profile details).</li>
          <li>By submitting content, you grant us a non-exclusive, royalty-free license to host, store, display, reproduce, adapt, and moderate it as part of operating the shared archive, and you confirm you have the rights to grant this license.</li>
          <li>Contributions to the wiki may be edited, combined, revised, and retained as part of the collaborative archive, including after your account is deleted (in de-identified form).</li>
          <li>You may delete your own content where the Platform provides that option; some copies may persist in backups or where retention is required by law.</li>
        </ul>

        <h2>5. Our intellectual property</h2>
        <p>
          The Platform, the Attrition game, and the world of Eldravir &mdash; including their software,
          text, artwork, audio, and branding &mdash; belong to the project team or its licensors and are
          protected by law. We grant you a limited, personal, non-transferable, revocable license to use
          the Platform for its intended purpose. You may not copy, redistribute, or create derivative
          works from our materials except as expressly permitted or allowed by law.
        </p>

        <h2>6. Moderation and enforcement</h2>
        <p>
          Moderators may review, edit, hide, or remove content, and may warn, suspend, or ban accounts
          that breach these Terms or the law. You can report content that violates these Terms using the
          reporting tools. We aim to act fairly and proportionately.
        </p>

        <h2>7. Game software</h2>
        <p>
          Any game client or download offered through the Platform is provided as-is, without warranty,
          and may be subject to an additional license presented with it. It may be unavailable,
          incomplete, or offered only for testing while the project is in development.
        </p>

        <h2>8. Privacy</h2>
        <p>
          Our handling of personal data is described in our <Link href="/privacy">Privacy Policy</Link>,
          which forms part of these Terms.
        </p>

        <h2>9. Availability and changes to the service</h2>
        <ul>
          <li>The Platform is provided as-is and as-available. It may change, be interrupted, or be discontinued at any time, with or without notice.</li>
          <li>Because this is a student project, we do not guarantee continuous availability, data preservation, or long-term support.</li>
        </ul>

        <h2>10. Disclaimers and limitation of liability</h2>
        <p>
          To the maximum extent permitted by Vietnamese law, the Platform is provided without warranties
          of any kind, and we are not liable for indirect or incidental loss arising from your use of the
          Platform. Nothing in these Terms excludes or limits liability that cannot be excluded or limited
          under Vietnamese law, including liability for intentional or grossly negligent conduct, and
          nothing limits the rights you have as a consumer under the Law on Protection of Consumer Rights.
        </p>

        <h2>11. Suspension and termination</h2>
        <p>
          You may stop using the Platform and delete your account at any time in{" "}
          <Link href="/settings">Settings</Link>. We may suspend or terminate access if you breach these
          Terms or the law, or where necessary to protect the Platform or its users.
        </p>

        <h2>12. Governing law and disputes</h2>
        <p>
          These Terms are governed by the laws of Vietnam. We encourage you to contact us first so we can
          resolve any dispute amicably. Disputes that cannot be resolved through negotiation shall be
          settled by the competent courts of Vietnam, without prejudice to any mandatory consumer-protection
          rights available to you.
        </p>

        <h2>13. Changes to these Terms</h2>
        <p>
          We may update these Terms from time to time. We will change the &ldquo;last updated&rdquo; date
          above and, for material changes, provide notice through the Platform. Continued use after an
          update constitutes acceptance of the revised Terms.
        </p>

        <h2>14. Contact</h2>
        <p>
          Questions about these Terms can be sent to <a href="mailto:legal@attrition.io.vn">legal@attrition.io.vn</a>{" "}
          or raised through the community <Link href="/forum">forum</Link>.
        </p>

        <p className="text-sm text-fg-subtle">
          Attrition is a non-commercial, educational student project provided as-is. These Terms are
          written to reflect Vietnamese law as of the date above; they are not legal advice.
        </p>
      </div>
    </PageShell>
  );
}
