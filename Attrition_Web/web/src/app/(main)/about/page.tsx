import Link from "next/link";
import styles from "./about.module.css";

export const metadata = {
  title: "About",
  description:
    "Learn about Attrition — a 2D roguelike co-op souls-like ARPG. Meet the team, explore the vision, and discover the world.",
};

const TEAM = [
  {
    name: "Phan Phuc Binh",
    role: "Project Leader, Creative Director, Level Designer, Network Programmer",
    initial: "PB",
  },
  {
    name: "Nguyen Nhat Dang",
    role: "Combat Designer, Enemy Designer, Gameplay & AI Programmer",
    initial: "ND",
  },
  {
    name: "Tran Thien Dang",
    role: "QA Tester",
    initial: "TD",
  },
  {
    name: "Le Trung Hau",
    role: "Narrative Designer, UX/UI Designer, System Designer, Backend & Frontend Dev",
    initial: "LH",
  },
];

export default function AboutPage() {
  return (
    <div className="page">
      <div className="container container-narrow">
        <div className="page-header">
          <div className="breadcrumb">
            <Link href="/">Home</Link>
            <span className="breadcrumb-separator">›</span>
            <span className="breadcrumb-current">About</span>
          </div>
          <h1>About Attrition</h1>
        </div>

        {/* The Game */}
        <section className={styles.section}>
          <h2>The Game</h2>
          <p>
            <strong>Attrition</strong> is a <strong>2D roguelike co-op souls-like action RPG</strong> built
            in Unity. Developed as the SE Capstone Project (<strong>SEP490</strong>) by Group 21 at
            FPT University, Summer 2026, under Instructor NhonNT9.
          </p>
          <p>
            The game challenges players to explore interconnected maps, solve environmental
            puzzles, and defeat formidable bosses — either solo or with a partner. While fully
            playable alone, a second player can dynamically join to share the experience, with
            intelligent difficulty scaling that keeps the challenge honest.
          </p>
        </section>

        {/* The Story */}
        <section className={styles.section}>
          <h2>The Story</h2>
          <div className={styles.loreBlock}>
            <p className={styles.loreQuote}>
              &ldquo;Ren is already dead.&rdquo;
            </p>
            <p>
              He doesn&apos;t remember how, but now he is... not exactly alive. Revived by
              Iris through a contract he doesn&apos;t recall signing, Ren is bound to a single
              purpose: destroy a world that has already fallen.
            </p>
            <p>
              Through the journey, he slowly regains memories — of a previous life, or
              perhaps of the original owner of the body he now inhabits. For every objective
              claimed, for every corrupted hero slain, he grows stronger.
            </p>
            <p>
              And then, as the contract demands, he must shatter a protecting artifact so
              that Iris&apos;s power can unmake this fragment of a world. Is she truly good, or
              something else entirely? It doesn&apos;t matter — he is just a pawn on the chessboard.
            </p>
            <p>
              He fulfills his side. He is promised true revival — a second life in the real
              world. But the memories remain. The corrupted heroes, the people he killed,
              everything. And her voice... Iris&apos;s voice... is still there.
            </p>
            <p className={styles.loreEnding}>
              He doesn&apos;t know if his reality is truly a reality, or just another trick
              placed on his mind.
            </p>
          </div>
        </section>

        {/* Key Features */}
        <section className={styles.section}>
          <h2>Key Features</h2>
          <div className={styles.featureList}>
            <div className={styles.feature}>
              <span className={styles.featureIcon}>⚔</span>
              <div>
                <h4>2-Player Co-op</h4>
                <p>
                  Fully playable solo, but a second player can join any session.
                  Both players must rest together, revive each other when downed,
                  and coordinate to solve co-op puzzles.
                </p>
              </div>
            </div>
            <div className={styles.feature}>
              <span className={styles.featureIcon}>🗡</span>
              <div>
                <h4>Souls-like Combat</h4>
                <p>
                  Precision-based combat with stamina management, dodge mechanics,
                  and punishing boss encounters with unique attack patterns.
                </p>
              </div>
            </div>
            <div className={styles.feature}>
              <span className={styles.featureIcon}>🔄</span>
              <div>
                <h4>Dynamic Difficulty</h4>
                <p>
                  An intelligent scaling engine detects the second player and adjusts
                  enemy health, damage, and stagger resistance — keeping co-op
                  just as demanding as solo.
                </p>
              </div>
            </div>
            <div className={styles.feature}>
              <span className={styles.featureIcon}>🗺</span>
              <div>
                <h4>Interconnected World</h4>
                <p>
                  Hand-designed maps with environmental hazards, traversal puzzles,
                  rest points for saving progress, and boss encounters guarding progression.
                </p>
              </div>
            </div>
            <div className={styles.feature}>
              <span className={styles.featureIcon}>💀</span>
              <div>
                <h4>Revive &amp; Rescue</h4>
                <p>
                  When downed, players enter a bleed-out state instead of dying
                  instantly — your partner can perform a high-risk rescue, but if
                  both fall, it&apos;s over.
                </p>
              </div>
            </div>
            <div className={styles.feature}>
              <span className={styles.featureIcon}>📖</span>
              <div>
                <h4>Companion Wiki</h4>
                <p>
                  This website — a centralized wiki database, game downloads,
                  character gallery, and community features under one roof.
                </p>
              </div>
            </div>
          </div>
        </section>

        {/* The Team */}
        <section className={styles.section}>
          <h2>The Team</h2>
          <p>
            We&apos;re <strong>Group SEP490_21</strong> — four students at FPT University.
          </p>
          <div className={styles.teamGrid}>
            {TEAM.map((member) => (
              <div key={member.name} className={styles.teamCard}>
                <div className="avatar avatar-xl">{member.initial}</div>
                <h4 className={styles.memberName}>{member.name}</h4>
                <p className={styles.memberRole}>{member.role}</p>
              </div>
            ))}
          </div>
        </section>

        {/* Tech Stack */}
        <section className={styles.section}>
          <h2>Tech Stack</h2>
          <div className={styles.techGrid}>
            {[
              { label: "Game Engine", value: "Unity 2D" },
              { label: "Networking", value: "Photon Fusion" },
              { label: "Backend", value: "C# / .NET Core 8" },
              { label: "Frontend", value: "Next.js + TypeScript" },
              { label: "Database", value: "PostgreSQL + Redis" },
              { label: "Real-time", value: "SignalR WebSocket" },
              { label: "Auth", value: "JWT + Google OAuth" },
              { label: "Deployment", value: "Docker / GCP" },
            ].map((item) => (
              <div key={item.label} className={styles.techItem}>
                <span className={styles.techLabel}>{item.label}</span>
                <span className={styles.techValue}>{item.value}</span>
              </div>
            ))}
          </div>
        </section>
      </div>
    </div>
  );
}
