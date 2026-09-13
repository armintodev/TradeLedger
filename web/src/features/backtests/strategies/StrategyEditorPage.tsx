import { useState } from 'react';
import {
  Badge,
  Button,
  Card,
  Code,
  Grid,
  Group,
  Stack,
  Text,
  Textarea,
  TextInput,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { IconArrowLeft, IconDeviceFloppy } from '@tabler/icons-react';
import { useNavigate, useParams } from 'react-router';
import { useSaveBacktestStrategy, useBacktestStrategy } from '@/api/queries/backtests';
import { ApiError } from '@/api/problem';
import { PageHeader } from '@/components/PageHeader';
import { ErrorState, LoadingCards } from '@/components/States';
import { parseRuleDocument } from '@/lib/rules/parse';
import { blankToNull } from '@/lib/forms';
import { notifySuccess } from '@/lib/notify';
import { RuleBuilder } from '../rules/RuleBuilder';
import { useRuleEditor } from '../rules/useRuleEditor';

export function StrategyEditorPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const existing = useBacktestStrategy(id);
  const save = useSaveBacktestStrategy();
  const editor = useRuleEditor();

  const [serverErrors, setServerErrors] = useState<Record<string, string[]> | undefined>();

  const form = useForm({
    initialValues: { name: '', description: '' },
    validate: { name: (value) => (value.trim() ? null : 'Name the strategy') },
  });

  // `rule` is populated only by the by-id fetch, so the builder cannot be
  // seeded until it lands. Re-seeding on `updatedAt` means a save refreshes the
  // draft rather than fighting it.
  const [seeded, setSeeded] = useState<string | null>(null);

  // Seeding during render rather than in an effect: React re-renders
  // immediately with the loaded values, so the builder never flashes the
  // starter document before the real one arrives.
  if (existing.data && seeded !== existing.data.updatedAt) {
    const strategy = existing.data;

    setSeeded(strategy.updatedAt);
    form.setValues({ name: strategy.name, description: strategy.description ?? '' });

    if (strategy.rule) {
      editor.reset(parseRuleDocument(strategy.rule));
    }
  }

  if (id && existing.isPending) {
    return <LoadingCards count={3} height={140} />;
  }

  if (id && existing.isError) {
    return <ErrorState error={existing.error} onRetry={() => void existing.refetch()} />;
  }

  function submit(values: { name: string; description: string }) {
    setServerErrors(undefined);

    save.mutate(
      {
        id,
        body: {
          name: values.name.trim(),
          description: blankToNull(values.description),
          rule: editor.document,
        },
      },
      {
        onSuccess: (strategy) => {
          notifySuccess(`${strategy.name} saved as v${strategy.version}.`);
          void navigate('/backtests?tab=strategies');
        },
        onError: (error) => {
          // The keys here are JSON paths into the rule tree, not field names, so
          // `applyServerErrors` cannot match them. The builder resolves them
          // onto nodes instead.
          if (error instanceof ApiError && error.errors) {
            setServerErrors(error.errors);
          }
        },
      },
    );
  }

  const localErrors = editor.diagnostics.filter((d) => d.severity === 'error');

  return (
    <Stack gap="md">
      <Group>
        <Button
          variant="subtle"
          leftSection={<IconArrowLeft size={16} />}
          onClick={() => void navigate('/backtests?tab=strategies')}
        >
          Strategies
        </Button>
      </Group>

      <PageHeader
        title={id ? 'Edit strategy' : 'New strategy'}
        description="Entry conditions only. Exits are the run's business: a position closes on its stop, on a target derived at the run's risk-to-reward ratio, or on liquidation."
        actions={
          existing.data && (
            <Group gap="xs">
              <Badge variant="light">v{existing.data.version}</Badge>
              <Code>{existing.data.ruleHash.slice(0, 8)}</Code>
            </Group>
          )
        }
      />

      <form onSubmit={form.onSubmit(submit)}>
        <Stack gap="md">
          <Card padding="md">
            <Grid gap="sm">
              <Grid.Col span={{ base: 12, sm: 5 }}>
                <TextInput
                  label="Name"
                  placeholder="EMA cross — 4h"
                  {...form.getInputProps('name')}
                />
              </Grid.Col>
              <Grid.Col span={{ base: 12, sm: 7 }}>
                <Textarea
                  label="Description"
                  placeholder="What it is meant to catch, and when it does not work."
                  autosize
                  minRows={1}
                  {...form.getInputProps('description')}
                />
              </Grid.Col>
            </Grid>
          </Card>

          <RuleBuilder editor={editor} serverErrors={serverErrors} />

          <Group justify="space-between" wrap="wrap" gap="sm">
            <Text size="xs" c="dimmed" maw={520}>
              {id
                ? 'Saving bumps the version. Finished runs keep their own copy of the rules, so an old result stays explainable.'
                : 'The rule is validated before it is stored, so an unusable strategy never reaches a run.'}
            </Text>

            <Button
              type="submit"
              leftSection={<IconDeviceFloppy size={16} />}
              loading={save.isPending}
              disabled={localErrors.length > 0 || editor.jsonError !== null}
            >
              {id ? 'Save changes' : 'Create strategy'}
            </Button>
          </Group>
        </Stack>
      </form>
    </Stack>
  );
}
