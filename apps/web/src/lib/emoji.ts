/**
 * Curated avatar-emoji set for the profile picker (profile-addendum §2.2).
 *
 * Deliberately a fixed, hand-picked list with keyword search rather than a full emoji
 * dataset/library (e.g. frimousse / emoji-picker-element): the root rule is "no new
 * dependencies without stating why" and ADR-0019 offers the choice explicitly. A shipped
 * dataset adds ~tens-to-hundreds of KB and a transitive dep for what is a cosmetic,
 * trusted-household feature — the API is the authority that validates the stored value
 * either way (single grapheme, ADR-0006/0019), so an exhaustive client list buys nothing.
 * If real demand for "any emoji" appears, swapping this for a library is a local change.
 *
 * Keywords are lowercase; search matches the emoji itself or any keyword substring, so a
 * Swedish or English term both land (e.g. "räv" and "fox" → 🦊).
 */
export interface EmojiEntry {
  emoji: string
  keywords: string[]
}

export const AVATAR_EMOJIS: readonly EmojiEntry[] = [
  // Faces & people
  { emoji: '😀', keywords: ['smile', 'happy', 'glad', 'leende'] },
  { emoji: '😎', keywords: ['cool', 'sunglasses', 'solglasögon'] },
  { emoji: '🤓', keywords: ['nerd', 'glasses', 'plugg'] },
  { emoji: '🥳', keywords: ['party', 'fest', 'celebrate'] },
  { emoji: '😴', keywords: ['sleep', 'sömn', 'trött', 'tired'] },
  { emoji: '👻', keywords: ['ghost', 'spöke'] },
  { emoji: '🤖', keywords: ['robot'] },
  { emoji: '👽', keywords: ['alien', 'ufo'] },
  // Animals
  { emoji: '🦊', keywords: ['fox', 'räv'] },
  { emoji: '🐧', keywords: ['penguin', 'pingvin'] },
  { emoji: '🐈', keywords: ['cat', 'katt'] },
  { emoji: '🐶', keywords: ['dog', 'hund'] },
  { emoji: '🐻', keywords: ['bear', 'björn'] },
  { emoji: '🦉', keywords: ['owl', 'uggla'] },
  { emoji: '🐝', keywords: ['bee', 'bi'] },
  { emoji: '🦆', keywords: ['duck', 'anka'] },
  { emoji: '🐢', keywords: ['turtle', 'sköldpadda'] },
  { emoji: '🦭', keywords: ['seal', 'säl'] },
  { emoji: '🐙', keywords: ['octopus', 'bläckfisk'] },
  { emoji: '🦄', keywords: ['unicorn', 'enhörning'] },
  // Nature
  { emoji: '🌿', keywords: ['leaf', 'plant', 'växt', 'natur'] },
  { emoji: '🌸', keywords: ['flower', 'blomma', 'blossom'] },
  { emoji: '🌻', keywords: ['sunflower', 'solros'] },
  { emoji: '🍀', keywords: ['clover', 'lucky', 'klöver', 'tur'] },
  { emoji: '🌵', keywords: ['cactus', 'kaktus'] },
  { emoji: '🌙', keywords: ['moon', 'måne'] },
  { emoji: '⭐', keywords: ['star', 'stjärna'] },
  { emoji: '🔥', keywords: ['fire', 'eld', 'hot'] },
  { emoji: '🌈', keywords: ['rainbow', 'regnbåge'] },
  { emoji: '❄️', keywords: ['snow', 'snö', 'winter', 'vinter'] },
  { emoji: '🏔️', keywords: ['mountain', 'berg', 'fjäll'] },
  { emoji: '🌊', keywords: ['wave', 'sea', 'hav', 'våg'] },
  // Food & drink
  { emoji: '🍋', keywords: ['lemon', 'citron'] },
  { emoji: '🍎', keywords: ['apple', 'äpple'] },
  { emoji: '🍓', keywords: ['strawberry', 'jordgubbe'] },
  { emoji: '🍕', keywords: ['pizza'] },
  { emoji: '☕', keywords: ['coffee', 'kaffe', 'tea', 'te'] },
  { emoji: '🍺', keywords: ['beer', 'öl'] },
  { emoji: '🍰', keywords: ['cake', 'tårta', 'kaka'] },
  { emoji: '🥑', keywords: ['avocado'] },
  // Activities & travel
  { emoji: '🎧', keywords: ['music', 'headphones', 'musik', 'hörlurar'] },
  { emoji: '🎸', keywords: ['guitar', 'gitarr', 'rock'] },
  { emoji: '🎮', keywords: ['game', 'gaming', 'spel'] },
  { emoji: '⚽', keywords: ['football', 'soccer', 'fotboll'] },
  { emoji: '🏀', keywords: ['basketball', 'basket'] },
  { emoji: '🚲', keywords: ['bike', 'cycle', 'cykel'] },
  { emoji: '✈️', keywords: ['plane', 'travel', 'flyg', 'resa'] },
  { emoji: '🚀', keywords: ['rocket', 'raket', 'space'] },
  { emoji: '⛺', keywords: ['tent', 'camp', 'tält'] },
  { emoji: '📚', keywords: ['book', 'books', 'bok', 'läsa'] },
  { emoji: '🎨', keywords: ['art', 'paint', 'konst', 'måla'] },
  { emoji: '📷', keywords: ['camera', 'photo', 'kamera', 'foto'] },
  // Symbols & objects
  { emoji: '❤️', keywords: ['heart', 'love', 'hjärta', 'kärlek'] },
  { emoji: '✨', keywords: ['sparkle', 'glitter', 'gnistra'] },
  { emoji: '💡', keywords: ['idea', 'light', 'idé', 'lampa'] },
  { emoji: '🏡', keywords: ['home', 'house', 'hem', 'hus'] },
  { emoji: '🔑', keywords: ['key', 'nyckel'] },
  { emoji: '🎯', keywords: ['target', 'dart', 'mål'] },
  { emoji: '💎', keywords: ['diamond', 'gem', 'diamant'] },
  { emoji: '🧩', keywords: ['puzzle', 'pussel'] },
]

/** Filter the curated set by a query (matches the emoji or any keyword substring). */
export function searchEmojis(query: string): readonly EmojiEntry[] {
  const q = query.trim().toLowerCase()
  if (!q) return AVATAR_EMOJIS
  return AVATAR_EMOJIS.filter(
    (e) => e.emoji === q || e.keywords.some((k) => k.includes(q)),
  )
}
