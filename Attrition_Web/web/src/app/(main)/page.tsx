import Link from "next/link";
import styles from "./page.module.css";

export default function LandingPage() {
  return (
    <div className={styles.landing}>
      {/* Hero */}
      <section className={styles.hero}>
        <div className={styles.heroContent}>
          <h1 className={styles.heroTitle}>ATTRITION</h1>
          <p className={styles.heroSubtitle}>
            A 2D Co-op Souls-like ARPG
          </p>
          <p className={styles.heroTagline}>
            Explore interconnected worlds. Defeat formidable bosses. Uncover the truth — alone or with a partner.
          </p>
          <div className={styles.heroCtas}>
            <Link href="/download" className="btn btn-primary btn-lg">
              Download
            </Link>
            <Link href="/wiki" className="btn btn-secondary btn-lg">
              Explore Wiki
            </Link>
          </div>
        </div>
        <div className={styles.scrollHint} aria-hidden="true">
          <span className={styles.scrollChevron}>↓</span>
        </div>
      </section>

      {/* Story Teaser */}
      <section className={styles.story}>
        <div className="container">
          <div className={styles.storyContent}>
            <p className={styles.storyLead}>
              Ren is already dead.
            </p>
            <p className={styles.storyBody}>
              Revived through a contract he doesn&apos;t remember signing, he wakes in a fractured world 
              that&apos;s already fallen. Guided by the enigmatic Iris, his only choice is forward — 
              through corrupted heroes, forgotten ruins, and a truth that may unravel everything he 
              thought was real.
            </p>
          </div>
        </div>
      </section>

      {/* Features */}
      <section className={styles.features}>
        <div className="container">
          <div className={styles.featuresGrid}>
            <div className={styles.featureCard}>
              <span className={styles.featureIcon}>⚔</span>
              <h3 className={styles.featureTitle}>Co-op Combat</h3>
              <p className={styles.featureDescription}>
                Fight alongside a partner with coordinated strategies, shared revive mechanics, 
                and dynamic difficulty that scales to your team.
              </p>
            </div>

            <div className={styles.featureCard}>
              <span className={styles.featureIcon}>🗡</span>
              <h3 className={styles.featureTitle}>Souls-like Challenge</h3>
              <p className={styles.featureDescription}>
                Precision combat, punishing boss encounters, and deliberate character progression. 
                Every victory is earned.
              </p>
            </div>

            <div className={styles.featureCard}>
              <span className={styles.featureIcon}>📖</span>
              <h3 className={styles.featureTitle}>Community Wiki</h3>
              <p className={styles.featureDescription}>
                A living knowledge base maintained by the community. Items, enemies, skills, lore — 
                all documented and searchable.
              </p>
            </div>
          </div>
        </div>
      </section>

      {/* Collection CTA */}
      <section className={styles.collectionCta}>
        <div className="container">
          <div className={styles.ctaCard}>
            <div className={styles.ctaContent}>
              <span className={styles.ctaLabel}>🎵 Soundtrack</span>
              <h2 className={styles.ctaTitle}>Listen to the Collection</h2>
              <p className={styles.ctaDescription}>
                Explore the original soundtrack — atmospheric compositions that bring 
                Attrition&apos;s dark-fantasy world to life.
              </p>
              <Link
                href="/collection"
                className="btn btn-accent-subtle btn-lg"
              >
                Open Collection →
              </Link>
            </div>
          </div>
        </div>
      </section>

      {/* Quick Links */}
      <section className={styles.quickLinks}>
        <div className="container">
          <div className={styles.quickLinksGrid}>
            <Link href="/about" className={styles.quickLink}>
              <span className={styles.quickLinkIcon}>→</span>
              <div>
                <h4>About the Game</h4>
                <p>Learn about Attrition, the team, and the vision.</p>
              </div>
            </Link>
            <Link href="/forum" className={styles.quickLink}>
              <span className={styles.quickLinkIcon}>→</span>
              <div>
                <h4>Join the Forum</h4>
                <p>Discuss strategies, report bugs, share your journey.</p>
              </div>
            </Link>
            <Link href="/collection" className={styles.quickLink}>
              <span className={styles.quickLinkIcon}>→</span>
              <div>
                <h4>Collection</h4>
                <p>Soundtrack, concept art, and game assets.</p>
              </div>
            </Link>
          </div>
        </div>
      </section>
    </div>
  );
}
