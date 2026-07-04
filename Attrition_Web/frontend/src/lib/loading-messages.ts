/** Tongue-in-cheek status lines cycled by the loading screen. Order preserved as authored. */
export const LOADING_MESSAGES = [
  "Pirating MATLAB",
  "Six-sevening",
  "Collecting your data",
  "Selling your data",
  "Chudmaxxing",
  "Ignoring GPL",
  "Increasing RAM prices",
  "Hallucinating",
  "Outsourcing to Mossad",
  "Gambing your money away",
  "Asking a Chinese guy",
  "Increasing shareholder value",
  "Ordering 4000 pounds of meat",
  "Calling the White House",
  "Negotiating with the White House",
  "Curing Ganser Syndrome",
  "Praying to God",
  "Demanding for answers from ChatGPT",
  "Claudemaxxing",
  "Gaslighting the compiler",
  "Mining Bitcoin on your smart fridge",
] as const;

/** A random message, optionally different from the current one (avoids back-to-back repeats). */
export function randomLoadingMessage(exclude?: string): string {
  const pool = exclude ? LOADING_MESSAGES.filter((m) => m !== exclude) : LOADING_MESSAGES;
  return pool[Math.floor(Math.random() * pool.length)] ?? LOADING_MESSAGES[0];
}
