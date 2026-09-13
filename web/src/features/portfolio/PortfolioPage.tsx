import { Stack, Tabs } from '@mantine/core';
import { IconArrowsExchange, IconCamera, IconCoins } from '@tabler/icons-react';
import { useSearchParams } from 'react-router';
import { PageHeader } from '@/components/PageHeader';
import { HoldingsTab } from './HoldingsTab';
import { SnapshotsTab } from './SnapshotsTab';
import { TransfersTab } from './TransfersTab';

const TABS = ['holdings', 'transfers', 'snapshots'] as const;

type TabValue = (typeof TABS)[number];

export function PortfolioPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const raw = searchParams.get('tab');
  const tab: TabValue = TABS.includes(raw as TabValue) ? (raw as TabValue) : 'holdings';

  return (
    <Stack gap="md">
      <PageHeader
        title="Portfolio"
        description="Everything that is not a round-trip trade: bags, wallets, pools, and the money moving between them."
      />

      <Tabs
        value={tab}
        onChange={(value) => {
          const params = new URLSearchParams(searchParams);

          if (value && value !== 'holdings') {
            params.set('tab', value);
          } else {
            params.delete('tab');
          }

          setSearchParams(params, { replace: true });
        }}
        keepMounted={false}
      >
        <Tabs.List mb="md">
          <Tabs.Tab value="holdings" leftSection={<IconCoins size={16} />}>
            Holdings
          </Tabs.Tab>
          <Tabs.Tab value="transfers" leftSection={<IconArrowsExchange size={16} />}>
            Transfers
          </Tabs.Tab>
          <Tabs.Tab value="snapshots" leftSection={<IconCamera size={16} />}>
            Snapshots
          </Tabs.Tab>
        </Tabs.List>

        <Tabs.Panel value="holdings">
          <HoldingsTab />
        </Tabs.Panel>
        <Tabs.Panel value="transfers">
          <TransfersTab />
        </Tabs.Panel>
        <Tabs.Panel value="snapshots">
          <SnapshotsTab />
        </Tabs.Panel>
      </Tabs>
    </Stack>
  );
}
