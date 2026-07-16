import { defineConfig } from 'astro/config';

// Static output; all banking logic runs client-side via @supabase/supabase-js,
// secured by Postgres Row-Level Security. No server needed.
export default defineConfig({
  output: 'static',
  build: { format: 'directory' },
});
