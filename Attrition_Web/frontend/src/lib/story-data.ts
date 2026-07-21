// Eldravir story network — the lore of Attrition, structured for the /story pages.
// Source: the Eldravir manuscript + worldbuilding notes. Content here is the
// premise/setting and character truths; ending specifics are flagged `spoiler`
// so the UI can gate them behind a reveal.

export type StoryCategory = "character" | "world" | "concept" | "stratum";

export interface StoryLink {
  slug: string;
  label: string;
}

export interface StoryEntry {
  slug: string;
  name: string;
  category: StoryCategory;
  /** Short epigraph shown under the title and on cards. */
  tagline: string;
  /** One-line role/kind label, e.g. "The Shield · defend". */
  kicker?: string;
  /** Body paragraphs (plain prose). */
  body: string[];
  /** Spoiler paragraphs, hidden until the reader opts in. */
  spoiler?: string[];
  /** Related entries — the network edges. */
  related: string[];
}

export const LOGLINE =
  "A living man, last of a dead world, is hired by a god to walk down through the five strata of his world's failure — meeting each of the five who could not let it end — and at the bottom must choose what to do with a world that won't die.";

const CHARACTERS: StoryEntry[] = [
  {
    slug: "ren",
    name: "Ren",
    category: "character",
    kicker: "The living one · last native",
    tagline: "The one living thing in a world of the undying — the only creature in Eldravir that can still truly die.",
    body: [
      "Ren wakes nameless on broken paving beneath a bruised violet sky, with no past, no face, and no one to remember him. Heartbeat, breath, warmth, fear, blood — all real, all his. In a fragment where nothing is allowed to end, his mortality is the rarest thing there is.",
      "He is the last child of Eldravir: he did not survive the end of his world, he slept through it, held in a stasis deeper than the seal at the bottom of the world. His defining move is to understand rather than fight — to find the true third path for each of the Five Pillars rather than beat them.",
      "Under the method is a hunger he doesn't like admitting: the wish to be someone, to matter to one other living thing, to not have been no-one. It is his want and his flaw both — he grows proud of being the one who finds the answer, and the pride is what blinds him.",
    ],
    related: ["iris", "the-rules", "the-third-thing", "the-painless-thing", "the-fifth", "five-pillars"],
  },
  {
    slug: "iris",
    name: "Iris",
    category: "character",
    kicker: "The god · the Lightkeeper",
    tagline: "A god who guards a breach in the Void — and the only memory a dissolved world ever gets.",
    body: [
      "Iris is a god of another world, one that was destroyed. She survived its end and now keeps the most volatile breach between the living world and the Void. Her power is effectively unbounded; the question is never whether she can, but whether she chooses to.",
      "She is a keeper of endings, not a griever of homes. What she does is witness dying worlds so they end remembered instead of erased. But she shatters a fragment if she enters it — she can hold the shape of a dying world, its obituary, but never the truth of it from within.",
      "That is what she needs Ren for. A voice in his ear the whole way down, never a body — clinical, precise, unhurried, dry. She refuses to be a tutorial, and answers only what she feels like answering.",
    ],
    spoiler: [
      "Her true offer is not the descent but the work that follows it: world after world, walking inside the dying ones where she cannot go — the loneliest and most necessary task there is. Eldravir is the origin of the Lightkeeper's method.",
    ],
    related: ["ren", "lightkeeper", "carry-them", "the-void", "the-prior-agents"],
  },
  {
    slug: "karr-drennan",
    name: "Karr Drennan",
    category: "character",
    kicker: "The Shield · defend",
    tagline: "Still mounting a defense against an enemy that already won — because no one ever came to tell him he could stop.",
    body: [
      "The Shield: the one a people leans on to hold, to make the line that doesn't break. He held the wall in the Void War long enough for some of the city to escape through the gate. He won — and then the world ended mid-battle and froze him in the one second his whole soul was built around, with no horn, no officer, no word to sound the stand-down.",
      "His son was among the recruits — too young, too soft. Karr drilled him hardest of all, savage with love, certain hard would carry him through. The boy died anyway. Now Karr drills the dead — really he drills one soldier, his son, over and over, trying to get it right enough that the cruelty would have been worth it.",
      "Ren cannot out-fight him. He frees him by delivering the after-action report in the one register Karr can still hear: the evacuation is complete, they got out, you won, you may stand down. An ending given as an order.",
    ],
    related: ["the-wall", "five-pillars", "maren", "the-third-thing"],
  },
  {
    slug: "talwyn",
    name: "Talwyn",
    category: "character",
    kicker: "The Hearth · heal",
    tagline: "In her ward, no one is allowed to die — and that is the cruelty, not the mercy.",
    body: [
      "The Hearth: the one who heals, who keeps the people whole. She lost her own daughter on the first day of the Void War — a fever she, the best healer in the quarter, could not break. She rose from that little bed and swore: not one more, not ever.",
      "Since then she has never let a soul go. But keeping the living means easing them so completely that nothing remains behind their eyes. She wins, every time. She never has to feel that dawn again.",
      "This is where the painless thing bites hardest — her kindness and the rot's offer become one voice. Ren refuses the ease and gives her permission to grieve and let go: that losing her daughter was not her failure. The savable ones come back.",
    ],
    related: ["the-ward", "five-pillars", "the-painless-thing", "the-third-thing"],
  },
  {
    slug: "bran",
    name: "Bran",
    category: "character",
    kicker: "The Flame · hope",
    tagline: "He keeps a congregation joyful and unafraid, forever one hour short of a rescue that is never coming.",
    body: [
      "The Flame: the one who keeps hope. In his cathedral the dead sing, radiant, certain that help is on the road, held in the last hour before they would have understood it was not. The cruelest stratum, because the victims are grateful.",
      "In the first days of the war, Bran — then a nobody, a keeper of candles — was shown a true vision: the eastern gate opening, help coming through. He spoke it, and three days later it came true, and hundreds lived. That one real miracle is the hook he is caught on.",
      "The trap is reversed: the people are happy, and the truth can only wound them. Ren listens beneath the song — the hope was never about the gate, it was about holding each other in the dark — and speaks the hardest truth in the story while handing back the true thing the lie protected: you were right to hope; it's how you stayed human.",
    ],
    related: ["the-cathedral", "five-pillars", "the-third-thing", "maren"],
  },
  {
    slug: "tomas",
    name: "Tomas",
    category: "character",
    kicker: "The Memory · remember",
    tagline: "He hoards the souls of the dead so none are ever forgotten — and in keeping them, imprisons them.",
    body: [
      "The Memory: the one who keeps the record, so a people is not lost. His archive is an edgeless vault of kept souls, each looping a single frozen moment, filed and labeled and held.",
      "He was born no one — beneath being recorded, in an uncounted year, in a quarter that burned unremembered. He taught himself to exist by being unforgettable: the clerk who never errs. When the world ended and everyone who might have remembered him died, he kept them all — not from love, but as a wall against his own erasure.",
      "Ren can't promise Tomas he'll be remembered. Instead he draws the distinction that becomes the heart of the ending: keeping is not the same as carrying. He'll carry the whole world out in a living mind that goes on living — Tomas included.",
    ],
    related: ["the-archive", "five-pillars", "carry-them", "the-prior-agents"],
  },
  {
    slug: "maren",
    name: "Maren",
    category: "character",
    kicker: "The Crown · decide",
    tagline: "She raised the Seal rather than let the Void dissolve her people — and has sat at the bottom of that choice ever since.",
    body: [
      "The Crown: the one whose function is to decide. The only figure in Eldravir who is not gray, not looping — present, whole, unbearably tired, knowing exactly what she did.",
      "In the last hour of the Void War she had two unbearable doors: let the Void assimilate the city, dissolving every soul into nothing forever, or raise the Seal — saving them, keeping every soul whole and itself, but trapped in the moment of dying. She chose the seal. Every frozen step in the world above is downstream of her one decision.",
      "She isn't fooled — there's no truth to deliver that she doesn't know. What she lacks is permission to have failed. Ren, the last living child of the people she sealed, gives it: she didn't fail at the impossible thing, she succeeded at the real one — holding the door shut until the right hands came.",
    ],
    related: ["the-throne", "the-seal", "five-pillars", "the-rot"],
  },
  {
    slug: "the-fifth",
    name: "The Fifth",
    category: "character",
    kicker: "The predator · the thing that walks",
    tagline: "The prior agent who took the painless thing completely. Now hunger wearing a face — the bad ending, walking.",
    body: [
      "Of the five living souls Iris sent down before Ren, the fifth went furthest of all — the one she was proudest to send, the closest she had ever come to her hands. At the deep of it, tired past telling, it surrendered all at once and entire. There was nothing left to file. Only hunger, wearing its face.",
      "It is what Ren becomes if he takes the painless thing: the first door, walking. And it breaks his method — his whole tool is to find the wound and name the true thing, but the Fifth has no wound left to name, no person inside. Understanding fails against it.",
    ],
    spoiler: [
      "Tomas's filing was the last cage holding it. When Ren frees Tomas — flawlessly, the way he always wins — his pride makes him overlook the empty fifth case, and he takes the door off that cage. On the stair it catches him, his method fails, and it eats one of the souls he carries. It cannot be carried: the one thing too far gone, the proof that the painless thing is a true death, not a rest.",
    ],
    related: ["the-prior-agents", "the-painless-thing", "ren", "iris"],
  },
  {
    slug: "the-prior-agents",
    name: "The Prior Agents",
    category: "character",
    kicker: "The five before · the lost ones",
    tagline: "The five living souls Iris sent into Eldravir before Ren. Each one was lost.",
    body: [
      "Five living people, from outside the dead world, each sent in under the same deal Iris offered Ren: go down, understand it, and your way out comes with the ending. None reached the bottom.",
      "Four of the five faded gently. Each stopped at a stratum, and the thing that stopped each was not the dead but the choice the stratum asked — offered the easier version, and they took it, and the rot took them an inch at a time. Then Tomas found what was left and filed it among the kept.",
      "They raise the true stakes of Ren's own deal: he is not a chosen one, just the latest. He resolves to carry them out — and the fifth, the one that did not fade gently, becomes the predator that hunts him down.",
    ],
    related: ["the-fifth", "iris", "tomas", "carry-them"],
  },
];

const WORLD: StoryEntry[] = [
  {
    slug: "eldravir",
    name: "Eldravir",
    category: "world",
    kicker: "The fragment · the dead world",
    tagline: "A shard of a dead world, broken off and sealed in stasis inside the Void, slowly being digested by it.",
    body: [
      "Not a dungeon — the last preserved piece of a civilization, caught at the instant of its end and looping there ever since. Once a great power with four hundred years of history, until its end came as the Void War, the war with the creatures from the sky.",
      "Now it is caught mid-death, held by the Seal. Nothing is allowed to finish — the dead don't stay dead, they loop or lean toward warmth. Above it there is no sky, only the bruised violet lid of the Void. Below it the strata descend to the Throne at the core.",
      "It is not fully dead. Pockets of life hold out: survivors Talwyn keeps, and Ren himself, the last child, who slept through the end. The savable can still be given back.",
    ],
    related: ["the-void", "the-seal", "the-rot", "the-strata", "five-pillars"],
  },
  {
    slug: "the-void",
    name: "The Void",
    category: "world",
    kicker: "The dark between worlds",
    tagline: "Not evil — vast, indifferent, and wrong. The space between worlds, where the ordinary rules don't hold.",
    body: [
      "The expanse between realities. It is not a hell or an army of malice — it is alien, not wicked. But it hates the endless change of living worlds, and wants the noise of them stilled into uniform nothing. It corrupts the nature of mana itself, and what it corrupts, it absorbs: death stops working, and everything is slowly digested back toward nothing.",
      "The sky is the Void. Above Eldravir there is no real sky — only a seamless bruised violet-black lid, close, attentive, pressing down. That ceiling is the thing trying to eat the world, and proximity to it frays the mind.",
      "In its dark drift the leviathans — entities larger than a capital city, world-killers that clawed the outer walls. The only reason they haven't come is that Iris holds them off.",
    ],
    related: ["eldravir", "the-rot", "mana-and-the-leash", "iris"],
  },
  {
    slug: "the-rot",
    name: "The Rot",
    category: "world",
    kicker: "The corruption · assimilation",
    tagline: "It looks like the Void leaking in past the Seal. It isn't. It's the seal's own souring.",
    body: [
      "The seal holds — the Void has never gotten through. The Rot is the seal itself, doing the only thing a thing can do when held against its own ending too long: life that cannot complete itself — cannot finish dying, healing, hoping, anything — sours, and turns, and rots in place.",
      "Maren saved her people from being assimilated by the Void from without, and in the saving made a slower, homemade assimilation from within. It works at Ren throughout — his left arm goes quiet, then dead to the shoulder.",
      "But it never attacks. It offers: the painless thing — an end to pain, fear, the ability to die. The whole fragment is one offer echoing up from the bottom: be kept, never end.",
    ],
    related: ["the-seal", "the-void", "maren", "the-painless-thing"],
  },
  {
    slug: "the-seal",
    name: "The Seal",
    category: "world",
    kicker: "Maren's choice",
    tagline: "The working Maren raised in the last hour of the war — the choice that froze the world and made everything else.",
    body: [
      "In the final hour, Maren the Crown had two doors: let the Void assimilate the city, every soul dissolved into nothing forever, or raise a seal the Void could not cross. The seal saved them from assimilation — kept every soul whole and itself — but it could not stop the dying already underway. So it froze them in the dying: an endless half-death, themselves but never finishing being themselves.",
      "Everything in Eldravir is downstream of the seal: the frozen square, the looping dead, the Five Pillars caught mid-function. And over an age it has soured into the Rot. The seal is why the fragment is a half-death and not a grave.",
    ],
    related: ["maren", "the-rot", "the-void", "the-throne"],
  },
  {
    slug: "mana-and-the-leash",
    name: "Mana & the Leash",
    category: "world",
    kicker: "The magic, and its limit",
    tagline: "Iris lends Ren power — but capped, because spending too much draws the leviathans of the Void.",
    body: [
      "Ren can't run on a mortal's own fuel, so Iris lends him mana to cast with. Magic is wanting, made exact: a desire held to a fixed size, shape, and aim. Anyone can want; almost no one can want precisely. The exactness is the whole skill.",
      "The cap is deliberate — the leash. Spend small and the leviathans outside keep not looking. Spend big and you put a light in the window, and something the size of a city turns its head. Iris holds them off, but won't test how much she can hold.",
      "So Ren's growth is in skill and cleverness, never raw power — he can never just blast through. The ceiling isn't an XP bar; it's don't make a noise loud enough to wake the dark.",
    ],
    related: ["iris", "ren", "the-void"],
  },
  {
    slug: "lightkeeper",
    name: "The Lightkeeper",
    category: "world",
    kicker: "Iris's role · keeper of the breach",
    tagline: "The sentinel who guards the most volatile breach into the Void — and witnesses the worlds that drift against her own.",
    body: [
      "The role Iris holds: guarding the breach, annihilating what tries to come through, and witnessing the dying worlds that drift against it so they end remembered instead of erased.",
      "Eldravir is the origin of her method — how she came to do this work with living hands inside the fragments, where she herself cannot go.",
    ],
    related: ["iris", "the-void", "carry-them"],
  },
];

const CONCEPTS: StoryEntry[] = [
  {
    slug: "the-rules",
    name: "The Rules",
    category: "concept",
    kicker: "Ren's self-made laws",
    tagline: "The laws Ren builds, one per stratum, to keep a self in a place designed to erase him.",
    body: [
      "The Void dissolves selves; Ren builds one from rules, like driving stakes into ground that won't hold anything else. By the descent to the core he says them aloud as a railing in the dark — a shape, the one thing the unmaking can't stand. They accrete as he goes:",
      "Don't freeze. · Don't lie to myself. · Don't take the painless thing. · Don't lie to be kind. · Carry them, don't keep them. · And one he can't make in advance — found only at the Throne: to choose an ending is not to lose.",
    ],
    related: ["ren", "the-third-thing", "the-painless-thing", "carry-them"],
  },
  {
    slug: "the-third-thing",
    name: "The Third Thing",
    category: "concept",
    kicker: "How each pillar is freed",
    tagline: "Not the cruel answer, not the kind lie — the true third path that neither condemns nor comforts.",
    body: [
      "Every stratum presents two obvious doors, both wrong: a cruelty dressed as honesty, and a comfort built on a lie. The work is to find the third thing — the true sentence that also sets the person free.",
      "The fight is never the fight. The weapon is understanding, all the way down to the part that's unbearable, because the unbearable part is the true part, and the true part is the only thing that ends the loop.",
    ],
    related: ["five-pillars", "the-rules", "ren"],
  },
  {
    slug: "the-painless-thing",
    name: "The Painless Thing",
    category: "concept",
    kicker: "The rot's offer",
    tagline: "Not power, but relief. The temptation that took the prior agents and nearly takes Ren.",
    body: [
      "The corruption never attacks; it offers. Stop hurting. Stop being afraid. Stop being able to die. It asks for almost nothing at a time — first just the fingers, then the hand — and each piece it takes stops hurting, so giving it feels like relief, not loss.",
      "That is why it beats nearly everyone: the price never feels like a price until the self is gone. The danger isn't that Ren reaches for power — it's that he reaches for numbness and calls it strength. It is the same offer at every scale: Talwyn's easing, Tomas's filing, Maren's seal. Be kept, never end.",
    ],
    related: ["the-rot", "ren", "talwyn", "the-prior-agents", "the-rules"],
  },
  {
    slug: "carry-them",
    name: "Carry Them, Don't Keep Them",
    category: "concept",
    kicker: "The heart of the resolution",
    tagline: "The distinction between keeping a thing stopped and carrying it forward.",
    body: [
      "Being filed isn't being kept; it's being stopped, frozen one breath short of finishing. Memory moves — a living thing carrying a dead one forward, letting it change and mean new things. Keeping is the opposite: stopping time on what you love so it can't leave you, which is the same as the painless thing and Maren's seal.",
      "The whole of Eldravir is a place that keeps. Ren ends it by carrying — walking a whole dead world out in a living mind that goes on living.",
    ],
    spoiler: [
      "It turns out to describe Iris herself: she is the memory a dissolved world gets, but can only keep its shape from the breach. She needs Ren to carry worlds out from within. This is the engine of the true ending.",
    ],
    related: ["tomas", "the-archive", "the-rules", "the-painless-thing", "iris"],
  },
];

const STRATA: StoryEntry[] = [
  {
    slug: "the-square",
    name: "The Square",
    category: "stratum",
    kicker: "Stratum I · surface",
    tagline: "Where Ren wakes. The frozen fleeing dead, and the clawed outer wall.",
    body: [
      "A square of dressed paving, heaved up and cracked from below, under the bruised violet lid of the Void. Scattered across it: people frozen mid-motion — a hand reaching for something no longer there, an arm flung up against a blow that never landed. Not a ruin. Nothing here was allowed to end.",
      "Ren wakes here, the only warm thing for a dead world in every direction, and makes his first rule facing the first dead thing that turns toward him: don't freeze.",
    ],
    related: ["ren", "eldravir", "the-rules"],
  },
  {
    slug: "the-cistern",
    name: "The Cistern",
    category: "stratum",
    kicker: "Stratum II",
    tagline: "A broken underground reservoir, where Iris teaches the leash.",
    body: [
      "Beneath the square, a shattered reservoir. Here Ren learns to cast on a coin of light — and feels the edge of the leash once, when an over-bright light makes the violet sky flinch. The lesson lands: spend small, don't put a light in the window.",
    ],
    related: ["mana-and-the-leash", "iris", "ren"],
  },
  {
    slug: "the-wall",
    name: "The Wall",
    category: "stratum",
    kicker: "Stratum III · the Shield",
    tagline: "A parade ground still fighting a war that was already won.",
    body: [
      "Karr Drennan's stratum: a drill that never ends, an after-action report that was never delivered. The first of the Pillars Ren must free not by force but by understanding.",
    ],
    related: ["karr-drennan", "five-pillars", "the-third-thing"],
  },
  {
    slug: "the-ward",
    name: "The Ward",
    category: "stratum",
    kicker: "Stratum IV · the Hearth",
    tagline: "A hospital where no one is allowed to die.",
    body: [
      "Talwyn's stratum, and where the painless thing bites hardest — her kindness and the rot's offer become a single voice. Ren refuses the ease and makes the rule that nearly saves him: don't take the painless thing.",
    ],
    related: ["talwyn", "the-painless-thing", "five-pillars"],
  },
  {
    slug: "the-cathedral",
    name: "The Cathedral",
    category: "stratum",
    kicker: "Stratum V · the Flame",
    tagline: "The joyful dead, waiting forever for a rescue that is never coming.",
    body: [
      "Bran's stratum, and the midpoint of the descent — the cruelest, because the victims are grateful. Here Ren speaks the hardest truth in the story: no one is coming. And hands back the true thing the lie protected.",
    ],
    related: ["bran", "five-pillars", "the-third-thing"],
  },
  {
    slug: "the-archive",
    name: "The Archive",
    category: "stratum",
    kicker: "Stratum VI · the Memory",
    tagline: "An edgeless vault of kept souls — and one empty case.",
    body: [
      "Tomas's stratum, where keeping and carrying are finally told apart, and where Ren finds the four prior agents filed among the kept. He wins flawlessly — and in his pride overlooks the empty fifth case.",
    ],
    related: ["tomas", "the-prior-agents", "the-fifth", "carry-them"],
  },
  {
    slug: "the-throne",
    name: "The Throne",
    category: "stratum",
    kicker: "Stratum VII · the core",
    tagline: "Where the world died, and keeps dying. The violet thickest, the leash most dangerous.",
    body: [
      "The core, where Maren sits at the bottom of her choice. The last rule cannot be made in advance — it is found only here, in front of the Crown: to choose an ending is not to lose.",
    ],
    related: ["maren", "the-seal", "five-pillars"],
  },
  {
    slug: "five-pillars",
    name: "The Five Pillars",
    category: "concept",
    kicker: "Defend · heal · hope · remember · decide",
    tagline: "The five who rose in the Void War — the five things a living world is, made into persons.",
    body: [
      "Not five random heroes, but the five functions a civilization needs in its worst hour. Each is now frozen mid-function, doing the right thing past the moment it was right — all stuck the same way, all downstream of one decision at the bottom.",
      "Shield (Karr Drennan) — fighting a won war. Hearth (Talwyn) — healing past death. Flame (Bran) — awaiting a rescue that won't come. Memory (Tomas) — remembering by imprisoning. Crown (Maren) — the choice that froze them all.",
      "The first four are consequences; the Crown is the cause. Each is freed not by a fight but by the third thing, and each gives Ren one of his rules.",
    ],
    related: ["karr-drennan", "talwyn", "bran", "tomas", "maren", "the-third-thing"],
  },
];

export const STORY_ENTRIES: StoryEntry[] = [...CHARACTERS, ...WORLD, ...CONCEPTS, ...STRATA];

const BY_SLUG = new Map(STORY_ENTRIES.map((e) => [e.slug, e]));

export function getStoryEntry(slug: string): StoryEntry | undefined {
  return BY_SLUG.get(slug);
}

export function storyLink(slug: string): StoryLink | null {
  const e = BY_SLUG.get(slug);
  return e ? { slug: e.slug, label: e.name } : null;
}

export function entriesByCategory(category: StoryCategory): StoryEntry[] {
  return STORY_ENTRIES.filter((e) => e.category === category);
}

// The strata in descent order, for the "Descent" rail. (Excludes five-pillars, a concept.)
export const DESCENT_ORDER = [
  "the-square", "the-cistern", "the-wall", "the-ward", "the-cathedral", "the-archive", "the-throne",
];
