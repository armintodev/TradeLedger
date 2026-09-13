import { Alert, Card, Group, Stack, Text, Title } from '@mantine/core';
import { IconInfoCircle } from '@tabler/icons-react';
import { useMe } from '@/api/queries/auth';
import { Instant } from '@/components/Instant';
import { Money } from '@/components/Money';
import { ErrorState, LoadingCards } from '@/components/States';
import { formatFractionAsPercent } from '@/lib/format';
import { resolveTimeZone } from '@/lib/time';

/**
 * Read-only, because no endpoint writes any of it. `timeZoneId` is the one that
 * stings: every instant in the UI is formatted with it and the only way to
 * change it today is an UPDATE against Postgres.
 */
export function ProfileTab() {
  const me = useMe();

  if (me.isPending) {
    return <LoadingCards count={1} height={220} />;
  }

  if (me.isError) {
    return <ErrorState error={me.error} onRetry={() => void me.refetch()} />;
  }

  const profile = me.data;
  const effectiveZone = resolveTimeZone(profile.timeZoneId);
  const zoneFellBack = effectiveZone !== profile.timeZoneId;

  return (
    <Stack gap="md">
      <Alert icon={<IconInfoCircle size={18} />} color="gray" variant="light">
        These values are configured server-side and seeded at startup. No endpoint writes them yet,
        so nothing here is editable.
      </Alert>

      <Card padding="md" maw={640}>
        <Stack gap="sm">
          <Title order={5}>Profile</Title>

          <Row label="Display name" value={profile.displayName ?? '—'} />
          <Row label="Email" value={profile.email} />
          <Row label="Starting balance" value={<Money value={profile.startingBalance} />} />
          <Row
            label="Journal started"
            value={<Instant value={profile.journalStartedAt} dateOnly />}
          />
          <Row
            label="Default risk per trade"
            value={formatFractionAsPercent(profile.defaultRiskPerTrade)}
          />
          <Row label="Time zone" value={profile.timeZoneId} />

          {zoneFellBack && (
            <Text size="xs" c="orange">
              Your browser cannot resolve “{profile.timeZoneId}”, so instants are being rendered in{' '}
              {effectiveZone} instead. A Windows zone id is valid server-side but not in a browser,
              which only knows IANA names.
            </Text>
          )}
        </Stack>
      </Card>
    </Stack>
  );
}

function Row({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <Group justify="space-between" gap="sm" wrap="nowrap">
      <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
        {label}
      </Text>
      <Text size="sm" component="div">
        {value}
      </Text>
    </Group>
  );
}
