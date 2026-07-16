// Content for each brand. `BRAND` env var (set at build time via Docker build
// arg) selects which one gets rendered. Edit freely — this is your real code.
export interface Service {
  title: string;
  body: string;
}
export interface Site {
  brand: string;
  domain: string;
  name: string;
  tagline: string;
  accent: string; // primary color
  intro: string;
  services: Service[];
  email: string;
}

export const SITES: Record<string, Site> = {
  shabanejupi: {
    brand: 'shabanejupi',
    domain: 'shabanejupi.tech',
    name: 'Shaban Ejupi',
    tagline: 'Software engineer & builder.',
    accent: '#4f7cff',
    intro:
      'I design and ship web apps, infrastructure, and automation. This site is built with Astro — fully custom code, no page-builder limits.',
    services: [
      { title: 'Web Development', body: 'Modern, fast websites and apps built from scratch.' },
      { title: 'Cloud & DevOps', body: 'Docker, tunnels, CI/CD, and self-hosted infrastructure.' },
      { title: 'Automation', body: 'Scripts and tooling that remove repetitive work.' },
    ],
    email: 'shaban.ejupi@student.uni-pr.edu',
  },
  enisi: {
    brand: 'enisi',
    domain: 'enisi.tech',
    name: 'Enisi',
    tagline: 'Ideas, engineered.',
    accent: '#e8622c',
    intro:
      'Enisi is a workspace for projects and experiments. Built with Astro so every pixel and component is yours to control.',
    services: [
      { title: 'Projects', body: 'A home for products and prototypes as they grow.' },
      { title: 'Writing', body: 'Notes and articles on what we are building.' },
      { title: 'Contact', body: 'Reach out for collaboration or questions.' },
    ],
    email: 'hello@enisi.tech',
  },
  halalbank: {
    brand: 'halalbank',
    domain: 'halalbankkosova.me',
    name: 'Halal Bank Kosova',
    tagline: 'Ethical, interest-free finance.',
    accent: '#0f9d58',
    intro:
      'Halal Bank Kosova is a concept for Sharia-compliant financial services in Kosovo — transparent, interest-free, and community-focused.',
    services: [
      { title: 'Halal Savings', body: 'Interest-free accounts aligned with Islamic principles.' },
      { title: 'Ethical Financing', body: 'Profit-sharing and asset-based financing, not riba.' },
      { title: 'Community', body: 'Financial services built around trust and fairness.' },
    ],
    email: 'info@halalbankkosova.me',
  },
};
