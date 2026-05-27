import { themes as prismThemes } from 'prism-react-renderer';
import rehypePrettyCode from 'rehype-pretty-code';
import type { Config } from '@docusaurus/types';
import type * as Preset from '@docusaurus/preset-classic';

// This runs in Node.js - Don't use client-side code here (browser APIs, JSX...)

const config: Config = {
  title: 'Community.HotChocolate.Data.Grouping',
  tagline: 'GROUP BY aggregations for HotChocolate',
  favicon: 'img/favicon.svg',

  // Future flags, see https://docusaurus.io/docs/api/docusaurus-config#future
  future: {
    v4: true, // Improve compatibility with the upcoming Docusaurus v4
  },

  // Set the production url of your site here
  url: 'https://wassim-k.github.io',
  // GitHub Pages serves the site under `/<repo>/`; locally we use `/` so
  // `localhost:3001/` works without the prefix.
  baseUrl: process.env.NODE_ENV === 'production' ? '/Community.HotChocolate.Data.Grouping/' : '/',

  // GitHub pages deployment config.
  organizationName: 'wassim-k', // GitHub user/org.
  projectName: 'Community.HotChocolate.Data.Grouping', // Repo name.

  onBrokenLinks: 'throw',

  // Even if you don't use internationalization, you can use this field to set
  // useful metadata like html lang. For example, if your site is Chinese, you
  // may want to replace "en" with "zh-Hans".
  i18n: {
    defaultLocale: 'en',
    locales: ['en'],
  },

  presets: [
    [
      'classic',
      {
        docs: {
          sidebarPath: './sidebars.ts',
          routeBasePath: '/',
          editUrl:
            'https://github.com/wassim-k/Community.HotChocolate.Data.Grouping/tree/main/docs/',
          beforeDefaultRehypePlugins: [
            [
              rehypePrettyCode,
              {
                theme: { light: 'light-plus', dark: 'dark-plus' },
                keepBackground: false,
              },
            ],
          ],
        },
        blog: false,
        theme: {
          customCss: ['./src/css/custom.css', './src/css/shiki.css'],
        },
      } satisfies Preset.Options,
    ],
  ],

  themeConfig: {
    colorMode: {
      respectPrefersColorScheme: true,
    },
    navbar: {
      title: 'Community.HotChocolate.Data.Grouping',
      logo: {
        alt: 'Grouping Logo',
        src: 'img/logo.svg',
      },
      items: [
        {
          type: 'docSidebar',
          sidebarId: 'docsSidebar',
          position: 'left',
          label: 'Documentation',
        },
        {
          href: 'https://github.com/wassim-k/Community.HotChocolate.Data.Grouping',
          label: 'GitHub',
          position: 'right',
        },
      ],
    },
    footer: {
      style: 'light',
      links: [
        {
          title: 'Getting Started',
          items: [
            { label: 'Installation', to: '/getting-started/installation' },
            { label: 'Quick Start', to: '/getting-started/quick-start' },
          ],
        },
        {
          title: 'Configuration',
          items: [
            { label: 'Entity Configuration', to: '/configuration/entity-configuration' },
            { label: 'Convention', to: '/configuration/convention' },
            { label: 'HAVING Filtering', to: '/configuration/having-filtering' },
          ],
        },
        {
          title: 'Learn',
          items: [
            { label: 'Examples', to: '/examples/grouping' },
          ],
        },
        {
          title: 'More',
          items: [
            { label: 'Architecture', to: '/architecture/design-decisions' },
            { label: 'GitHub', href: 'https://github.com/wassim-k/Community.HotChocolate.Data.Grouping' },
          ],
        },
      ],
      copyright: `Copyright © ${new Date().getFullYear()} Wassim K. Built with Docusaurus.`,
    },
  } satisfies Preset.ThemeConfig,
};

export default config;
