import { createTheme, type MantineColorScheme } from '@mantine/core';

export const THEME_STORAGE_KEY = 'tradeledger.theme';

export type StoredTheme = 'dark' | 'light';

/** Dark by default: this is a tool used after the market closes. */
export const DEFAULT_COLOR_SCHEME: StoredTheme = 'dark';

export const theme = createTheme({
  primaryColor: 'indigo',
  defaultRadius: 'md',
  fontFamilyMonospace:
    'ui-monospace, SFMono-Regular, "SF Mono", Menlo, Consolas, "Liberation Mono", monospace',
  headings: {
    fontWeight: '600',
  },
  components: {
    Table: {
      defaultProps: {
        highlightOnHover: true,
        verticalSpacing: 'sm',
        horizontalSpacing: 'md',
      },
    },
    Card: {
      defaultProps: {
        withBorder: true,
        radius: 'md',
      },
    },
    Paper: {
      defaultProps: {
        withBorder: true,
        radius: 'md',
      },
    },
  },
});

export function readStoredTheme(): StoredTheme {
  try {
    const raw = window.localStorage.getItem(THEME_STORAGE_KEY);

    return raw === 'light' || raw === 'dark' ? raw : DEFAULT_COLOR_SCHEME;
  } catch {
    return DEFAULT_COLOR_SCHEME;
  }
}

export function writeStoredTheme(scheme: MantineColorScheme): void {
  if (scheme !== 'light' && scheme !== 'dark') {
    return;
  }

  try {
    window.localStorage.setItem(THEME_STORAGE_KEY, scheme);
  } catch {
    // Ignore: the choice just will not survive a reload.
  }
}

/** Green up, red down — the sign is rendered too, so colour is never alone. */
export const PNL_COLORS = {
  positive: 'teal',
  negative: 'red',
  zero: 'dimmed',
} as const;
