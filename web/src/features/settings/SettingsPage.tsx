import { Stack, Tabs } from '@mantine/core';
import { IconBuildingBank, IconRefresh, IconShieldLock, IconUser } from '@tabler/icons-react';
import { useSearchParams } from 'react-router';
import { PageHeader } from '@/components/PageHeader';
import { AccountsTab } from './AccountsTab';
import { ProfileTab } from './ProfileTab';
import { ProxyTab } from './ProxyTab';
import { SyncTab } from './SyncTab';

const TABS = ['accounts', 'proxy', 'sync', 'profile'] as const;

type TabValue = (typeof TABS)[number];

export function SettingsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const raw = searchParams.get('tab');
  const tab: TabValue = TABS.includes(raw as TabValue) ? (raw as TabValue) : 'accounts';

  function select(value: string | null) {
    const params = new URLSearchParams(searchParams);

    if (value && value !== 'accounts') {
      params.set('tab', value);
    } else {
      params.delete('tab');
    }

    setSearchParams(params, { replace: true });
  }

  return (
    <Stack gap="md">
      <PageHeader
        title="Settings"
        description="Accounts and their keys, the egress proxy every exchange call leaves through, and what the sync has done."
      />

      <Tabs value={tab} onChange={select} keepMounted={false}>
        <Tabs.List mb="md">
          <Tabs.Tab value="accounts" leftSection={<IconBuildingBank size={16} />}>
            Accounts
          </Tabs.Tab>
          <Tabs.Tab value="proxy" leftSection={<IconShieldLock size={16} />}>
            Proxy
          </Tabs.Tab>
          <Tabs.Tab value="sync" leftSection={<IconRefresh size={16} />}>
            Sync
          </Tabs.Tab>
          <Tabs.Tab value="profile" leftSection={<IconUser size={16} />}>
            Profile
          </Tabs.Tab>
        </Tabs.List>

        <Tabs.Panel value="accounts">
          <AccountsTab onOpenProxy={() => select('proxy')} />
        </Tabs.Panel>
        <Tabs.Panel value="proxy">
          <ProxyTab />
        </Tabs.Panel>
        <Tabs.Panel value="sync">
          <SyncTab />
        </Tabs.Panel>
        <Tabs.Panel value="profile">
          <ProfileTab />
        </Tabs.Panel>
      </Tabs>
    </Stack>
  );
}
