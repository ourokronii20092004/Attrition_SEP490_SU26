"use client";

/**
 * Minimal line-based diff (LCS) for the wiki contribution review (PROB-1). Returns a list of
 * lines tagged equal/added/removed so admins see what a suggested edit changes in red/green,
 * without pulling in a diff dependency. Theme-aware via semantic color tokens.
 */
type DiffRow = { type: "equal" | "added" | "removed"; text: string };

function diffLines(oldText: string, newText: string): DiffRow[] {
  const a = oldText.split("\n");
  const b = newText.split("\n");
  const n = a.length, m = b.length;
  // LCS table.
  const lcs: number[][] = Array.from({ length: n + 1 }, () => new Array(m + 1).fill(0));
  for (let i = n - 1; i >= 0; i--) {
    for (let j = m - 1; j >= 0; j--) {
      lcs[i][j] = a[i] === b[j] ? lcs[i + 1][j + 1] + 1 : Math.max(lcs[i + 1][j], lcs[i][j + 1]);
    }
  }
  const rows: DiffRow[] = [];
  let i = 0, j = 0;
  while (i < n && j < m) {
    if (a[i] === b[j]) { rows.push({ type: "equal", text: a[i] }); i++; j++; }
    else if (lcs[i + 1][j] >= lcs[i][j + 1]) { rows.push({ type: "removed", text: a[i] }); i++; }
    else { rows.push({ type: "added", text: b[j] }); j++; }
  }
  while (i < n) { rows.push({ type: "removed", text: a[i] }); i++; }
  while (j < m) { rows.push({ type: "added", text: b[j] }); j++; }
  return rows;
}

export function LineDiff({ oldText, newText }: { oldText: string; newText: string }) {
  const rows = diffLines(oldText, newText);
  const added = rows.filter((r) => r.type === "added").length;
  const removed = rows.filter((r) => r.type === "removed").length;

  return (
    <div className="overflow-hidden rounded-lg border border-border">
      <div className="flex items-center gap-3 border-b border-border bg-surface-2/50 px-3 py-1.5 text-xs">
        <span className="text-success">+{added} added</span>
        <span className="text-danger">−{removed} removed</span>
      </div>
      <pre className="max-h-96 overflow-auto bg-surface text-xs leading-relaxed">
        {rows.map((r, idx) => (
          <div
            key={idx}
            className={
              r.type === "added"
                ? "bg-success/10 text-success"
                : r.type === "removed"
                ? "bg-danger/10 text-danger"
                : "text-fg-muted"
            }
          >
            <span className="select-none px-2 text-fg-subtle">
              {r.type === "added" ? "+" : r.type === "removed" ? "−" : " "}
            </span>
            <span className="whitespace-pre-wrap break-words">{r.text || " "}</span>
          </div>
        ))}
      </pre>
    </div>
  );
}
