/**
 * Entry category display metadata (ADR-0012 / docs/specs/entry-categories.md).
 * Icons mirror the design addendum's keyword→icon map; labels are Swedish,
 * matching the rest of the app.
 */
import {
  Armchair,
  Car,
  Gift,
  Home,
  type LucideIcon,
  Music,
  ReceiptText,
  ShoppingCart,
  Sparkles,
  Ticket,
  Tv,
  Utensils,
  Wifi,
  Zap,
} from 'lucide-react'
import type { EntryCategory } from '@/lib/api'

export const CATEGORY_ICON: Record<EntryCategory, LucideIcon> = {
  cleaning: Sparkles,
  restaurant: Utensils,
  event: Ticket,
  furniture: Armchair,
  groceries: ShoppingCart,
  transport: Car,
  internet: Wifi,
  rent: Home,
  music: Music,
  streaming: Tv,
  electricity: Zap,
  gift: Gift,
  other: ReceiptText,
}

export const CATEGORY_LABEL: Record<EntryCategory, string> = {
  cleaning: 'Städ',
  restaurant: 'Restaurang',
  event: 'Nöje',
  furniture: 'Möbler',
  groceries: 'Mat',
  transport: 'Transport',
  internet: 'Internet',
  rent: 'Hyra',
  music: 'Musik',
  streaming: 'Streaming',
  electricity: 'El',
  gift: 'Present',
  other: 'Övrigt',
}

export const CATEGORY_ORDER: EntryCategory[] = [
  'groceries',
  'restaurant',
  'rent',
  'electricity',
  'internet',
  'streaming',
  'music',
  'transport',
  'cleaning',
  'furniture',
  'event',
  'gift',
  'other',
]
