import { createClient } from '@supabase/supabase-js'

const SUPABASE_URL = 'https://supabase.shabanejupi.tech'
// public anon key — RLS + grants keep the pos schema locked to authenticated users
const ANON_KEY =
  'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJyb2xlIjoiYW5vbiIsImlzcyI6InN1cGFiYXNlIiwiaWF0IjoxNzgyOTc2Njk3LCJleHAiOjIwOTgzMzY2OTd9.EOuDpESKK-h3ByFPvfTzG5FjATx6W16vZoP6vyI8r94'

export const supabase = createClient(SUPABASE_URL, ANON_KEY, {
  db: { schema: 'pos' },
})

export const eur = (n) => `${Number(n ?? 0).toFixed(2)} €`
