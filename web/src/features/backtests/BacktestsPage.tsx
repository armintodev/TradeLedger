import { Stack, Tabs } from '@mantine/core';
import { IconChartDots, IconDatabase, IconPlayerPlay, IconWallet } from '@tabler/icons-react';
import { useSearchParams } from 'react-router';
import { PageHeader } from '@/components/PageHeader';
import { AccountsTab } from './accounts/AccountsTab';
import { MarketDataTab } from './marketData/MarketDataTab';
import { RunsTab } from './runs/RunsTab';
import { StrategiesTab } from './strategies/StrategiesTab';

const TABS = ['runs', 'accounts', 'strategies', 'market-data'] as const;

type TabValue = (typeof TABS)[number];

export function BacktestsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const raw = searchParams.get('tab');
  const tab: TabValue = TABS.includes(raw as TabValue) ? (raw as TabValue) : 'runs';

  function select(value: string | null) {
    const params = new URLSearchParams(searchParams);

    if (value && value !== 'runs') {
      params.set('tab', value);
    } else {
      params.delete('tab');
    }

    // Filters belong to the tab that owns them; carrying them across is noise.
    for (const key of ['accountId', 'status', 'page', 'pageSize']) {
      params.delete(key);
    }

    setSearchParams(params, { replace: true });
  }

  return (
    <Stack gap="md">
      <PageHeader
        title="Backtests"
        description="Simulate a rule strategy over stored candles. Nothing here can reach the exchange or your live equity curve."
      />

      <Tabs value={tab} onChange={select} keepMounted={false}>
        <Tabs.List mb="md">
          <Tabs.Tab value="runs" leftSection={<IconPlayerPlay size={16} />}>
            Runs
          </Tabs.Tab>
          <Tabs.Tab value="accounts" leftSection={<IconWallet size={16} />}>
            Accounts
          </Tabs.Tab>
          <Tabs.Tab value="strategies" leftSection={<IconChartDots size={16} />}>
            Strategies
          </Tabs.Tab>
          <Tabs.Tab value="market-data" leftSection={<IconDatabase size={16} />}>
            Market data
          </Tabs.Tab>
        </Tabs.List>

        <Tabs.Panel value="runs">
          <RunsTab />
        </Tabs.Panel>
        <Tabs.Panel value="accounts">
          <AccountsTab />
        </Tabs.Panel>
        <Tabs.Panel value="strategies">
          <StrategiesTab />
        </Tabs.Panel>
        <Tabs.Panel value="market-data">
          <MarketDataTab />
        </Tabs.Panel>
      </Tabs>
    </Stack>
  );
}
