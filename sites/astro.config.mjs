import { defineConfig } from 'astro/config';

// Static output — builds to plain HTML/CSS/JS served by nginx.
export default defineConfig({
  output: 'static',
  build: { format: 'directory' },
});
