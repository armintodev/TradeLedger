import {
  AppShell as MantineAppShell,
  Badge,
  Burger,
  Group,
  NavLink,
  ScrollArea,
  Text,
  Tooltip,
  UnstyledButton,
  useMantineColorScheme,
} from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import {
  IconChartHistogram,
  IconFlask,
  IconChecklist,
  IconLayoutDashboard,
  IconLogout,
  IconMoon,
  IconNotebook,
  IconSettings,
  IconSun,
  IconTargetArrow,
  IconWallet,
} from '@tabler/icons-react';
import { NavLink as RouterNavLink, Outlet, useLocation, useNavigate } from 'react-router';
import { useInbox } from '@/api/queries/trades';
import { useAuth } from '@/auth/useAuth';
import { writeStoredTheme } from '@/lib/theme';

const NAV_ITEMS = [
  { to: '/', label: 'Dashboard', icon: IconLayoutDashboard, end: true },
  { to: '/trades', label: 'Journal', icon: IconNotebook, end: false },
  { to: '/review', label: 'Review', icon: IconChecklist, end: false },
  { to: '/analytics', label: 'Analytics', icon: IconChartHistogram, end: false },
  { to: '/portfolio', label: 'Portfolio', icon: IconWallet, end: false },
  { to: '/plans', label: 'Plans', icon: IconTargetArrow, end: false },
  { to: '/backtests', label: 'Backtests', icon: IconFlask, end: false },
  { to: '/settings', label: 'Settings', icon: IconSettings, end: false },
];

export function AppShell() {
  const [opened, { toggle, close }] = useDisclosure(false);
  const location = useLocation();
  const navigate = useNavigate();
  const { auth, signOut } = useAuth();
  const { colorScheme, setColorScheme } = useMantineColorScheme();

  // The badge is the reason to open the review queue at all. It shares the
  // ['inbox'] key with the queue, which copies the list into local state on
  // mount so an invalidation here never reorders the trade under the cursor.
  const inbox = useInbox();
  const unreviewed = inbox.data?.length ?? 0;

  function toggleTheme() {
    const next = colorScheme === 'dark' ? 'light' : 'dark';
    setColorScheme(next);
    writeStoredTheme(next);
  }

  function handleSignOut() {
    signOut();
    void navigate('/login', { replace: true });
  }

  return (
    <MantineAppShell
      header={{ height: 56 }}
      navbar={{ width: 230, breakpoint: 'sm', collapsed: { mobile: !opened } }}
      padding="md"
    >
      <MantineAppShell.Header>
        <Group h="100%" px="md" justify="space-between" wrap="nowrap">
          <Group gap="sm" wrap="nowrap">
            <Burger opened={opened} onClick={toggle} hiddenFrom="sm" size="sm" />
            <Text fw={700} size="lg" style={{ letterSpacing: '-0.02em' }}>
              TradeLedger
            </Text>
          </Group>

          <Group gap="xs" wrap="nowrap">
            <Text size="sm" c="dimmed" visibleFrom="sm">
              {auth?.displayName ?? auth?.email}
            </Text>

            <Tooltip
              label={colorScheme === 'dark' ? 'Switch to light' : 'Switch to dark'}
              withArrow
            >
              <UnstyledButton onClick={toggleTheme} aria-label="Toggle colour scheme" p={6}>
                {colorScheme === 'dark' ? <IconSun size={18} /> : <IconMoon size={18} />}
              </UnstyledButton>
            </Tooltip>

            <Tooltip label="Sign out" withArrow>
              <UnstyledButton onClick={handleSignOut} aria-label="Sign out" p={6}>
                <IconLogout size={18} />
              </UnstyledButton>
            </Tooltip>
          </Group>
        </Group>
      </MantineAppShell.Header>

      <MantineAppShell.Navbar p="xs">
        <ScrollArea>
          {NAV_ITEMS.map((item) => {
            const Icon = item.icon;
            const active = item.end
              ? location.pathname === item.to
              : location.pathname.startsWith(item.to);

            return (
              <NavLink
                key={item.to}
                component={RouterNavLink}
                to={item.to}
                label={item.label}
                active={active}
                onClick={close}
                leftSection={<Icon size={18} />}
                rightSection={
                  item.to === '/review' && unreviewed > 0 ? (
                    <Badge size="sm" circle variant="filled" color="yellow">
                      {unreviewed}
                    </Badge>
                  ) : null
                }
              />
            );
          })}
        </ScrollArea>
      </MantineAppShell.Navbar>

      <MantineAppShell.Main>
        <Outlet />
      </MantineAppShell.Main>
    </MantineAppShell>
  );
}
