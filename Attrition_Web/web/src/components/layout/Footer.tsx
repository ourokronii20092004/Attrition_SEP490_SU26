import Link from "next/link";
import styles from "./Footer.module.css";

export default function Footer() {
  const currentYear = new Date().getFullYear();

  return (
    <footer className={styles.footer}>
      <div className="container">
        <div className={styles.grid}>
          {/* Brand */}
          <div className={styles.brand}>
            <span className={styles.brandName}>ATTRITION</span>
            <p className={styles.brandDescription}>
              A dark-fantasy 2D co-op souls-like ARPG. Explore, fight, survive — together.
            </p>
          </div>

          {/* Navigation */}
          <div className={styles.column}>
            <h4 className={styles.columnTitle}>Explore</h4>
            <nav className={styles.links}>
              <Link href="/wiki">Wiki</Link>
              <Link href="/forum">Forum</Link>
              <Link href="/gallery">Gallery</Link>
              <Link href="/download">Download</Link>
            </nav>
          </div>

          {/* Community */}
          <div className={styles.column}>
            <h4 className={styles.columnTitle}>Community</h4>
            <nav className={styles.links}>
              <Link href="/about">About</Link>
              <Link href="/forum">Discussions</Link>
              <a href="/collection-relay" target="_blank" rel="noopener noreferrer">
                Soundtrack
              </a>
            </nav>
          </div>

          {/* Project */}
          <div className={styles.column}>
            <h4 className={styles.columnTitle}>Project</h4>
            <nav className={styles.links}>
              <span className={styles.meta}>SEP490 — FPT University</span>
              <span className={styles.meta}>Summer 2026</span>
              <span className={styles.meta}>Team 21</span>
            </nav>
          </div>
        </div>

        <div className={styles.bottom}>
          <span className={styles.copyright}>
            © {currentYear} Attrition. Built by SEP490 Team 21.
          </span>
        </div>
      </div>
    </footer>
  );
}
