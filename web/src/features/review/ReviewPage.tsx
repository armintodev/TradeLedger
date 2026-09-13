import { useMemo, useState } from 'react';
import { Button, Card, Grid, Group, Progress, Stack, Text } from '@mantine/core';
import { IconChecklist, IconChecks } from '@tabler/icons-react';
import { useNavigate } from 'react-router';
import { useInbox, useTrade } from '@/api/queries/trades';
import { PageHeader } from '@/components/PageHeader';
import { EmptyState, ErrorState, LoadingCards } from '@/components/States';
import { JournalForm } from '@/features/trades/JournalForm';
import { TradeFacts } from '@/features/trades/TradeFacts';
import { notifySuccess } from '@/lib/notify';

/**
 * The post-market loop, and the reason the product exists: turn closed
 * positions into journal rows without stopping to navigate.
 *
 * The queue is snapshotted into local state on first load. Saving invalidates
 * `['inbox']` so the nav badge drops, but the list under the cursor never
 * reorders — a refetch mid-session would move the next trade out from under the
 * user between one save and the next.
 */
export function ReviewPage() {
  const navigate = useNavigate();
  const inbox = useInbox({ refetchOnMount: 'always' });

  const [queue, setQueue] = useState<string[] | null>(null);
  const [cursor, setCursor] = useState(0);

  // Snapshotting during render, not in an effect: React re-renders immediately
  // with the new state and nothing flashes in between.
  if (queue === null && inbox.data) {
    setQueue(inbox.data.map((trade) => trade.id));
  }

  const currentId = queue?.[cursor];
  const trade = useTrade(currentId);

  const total = queue?.length ?? 0;
  const done = Math.min(cursor, total);

  const symbols = useMemo(() => {
    const map = new Map<string, string>();

    for (const item of inbox.data ?? []) {
      map.set(item.id, item.symbol);
    }

    return map;
  }, [inbox.data]);

  if (inbox.isPending && !inbox.data) {
    return <LoadingCards count={2} height={180} />;
  }

  if (inbox.isError) {
    return <ErrorState error={inbox.error} onRetry={() => void inbox.refetch()} />;
  }

  if (total === 0) {
    return (
      <Stack gap="md">
        <PageHeader title="Review" />
        <EmptyState
          icon={<IconChecks size={36} opacity={0.5} />}
          title="Nothing to review"
          description="Every closed trade already has its subjective half. New ones appear here as soon as a sync closes a position."
          action={
            <Button variant="light" onClick={() => void navigate('/trades')}>
              Open the journal
            </Button>
          }
        />
      </Stack>
    );
  }

  if (cursor >= total) {
    return (
      <Stack gap="md">
        <PageHeader title="Review" />
        <EmptyState
          icon={<IconChecks size={36} opacity={0.5} />}
          title="Inbox cleared"
          description={`You worked through ${total} trade${total === 1 ? '' : 's'}.`}
          action={
            <Group gap="sm">
              <Button variant="light" onClick={() => void navigate('/trades')}>
                Open the journal
              </Button>
              <Button
                variant="subtle"
                onClick={() => {
                  // An explicit reload: the queue is deliberately not refetched
                  // on its own while the user is working through it.
                  setQueue(null);
                  setCursor(0);
                  void inbox.refetch();
                }}
              >
                Check for more
              </Button>
            </Group>
          }
        />
      </Stack>
    );
  }

  function advance() {
    setCursor((current) => current + 1);
  }

  return (
    <Stack gap="md">
      <PageHeader
        title="Review"
        description={`${done + 1} of ${total} — ${symbols.get(currentId ?? '') ?? ''}`}
        actions={
          <Button
            variant="subtle"
            leftSection={<IconChecklist size={16} />}
            onClick={() => void navigate('/trades?reviewState=Unreviewed')}
          >
            See them as a list
          </Button>
        }
      />

      <Progress value={(done / total) * 100} size="sm" aria-label="Review progress" />

      {trade.isPending && <LoadingCards count={2} height={180} />}

      {trade.isError && (
        <Card padding="md">
          <Stack gap="sm">
            <ErrorState error={trade.error} onRetry={() => void trade.refetch()} />
            <Group>
              <Button variant="default" onClick={advance}>
                Skip this one
              </Button>
            </Group>
          </Stack>
        </Card>
      )}

      {trade.data && (
        <Grid gap="md">
          {/* Above the form on a phone, beside it on a desktop. */}
          <Grid.Col span={{ base: 12, lg: 5 }}>
            <TradeFacts trade={trade.data} compact />
          </Grid.Col>

          <Grid.Col span={{ base: 12, lg: 7 }}>
            <JournalForm
              key={trade.data.id}
              trade={trade.data}
              mode="review"
              shortcuts
              onSkip={advance}
              onSaved={(saved) => {
                notifySuccess(`${saved.symbol} reviewed.`);
                advance();
              }}
            />
          </Grid.Col>
        </Grid>
      )}

      <Text size="xs" c="dimmed">
        Ctrl/Cmd + Enter saves and advances · Esc skips · 1–5 set the rating
      </Text>
    </Stack>
  );
}
