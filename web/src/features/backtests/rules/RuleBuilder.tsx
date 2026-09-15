import { useMemo, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Card,
  Grid,
  Group,
  Select,
  Stack,
  Switch,
  Tabs,
  Text,
  Textarea,
  Title,
} from '@mantine/core';
import { IconAlertTriangle, IconArrowBackUp, IconBraces, IconSitemap } from '@tabler/icons-react';
import { useRuleValidation } from '@/api/queries/backtests';
import { intervalOptions } from '@/lib/marketData';
import { resolveServerPath } from '@/lib/rules/serialise';
import { starterCondition } from '@/lib/rules/starter';
import {
  addChild,
  changeOperator,
  mapNode,
  removeNodeById,
  replaceOperand,
  wrapNode,
} from '@/lib/rules/tree';
import { byNode, documentDiagnostics } from '@/lib/rules/validate';
import type {
  ConditionDraft,
  EntrySide,
  IndicatorDraft,
  NodeId,
  OperandDraft,
  RuleDraft,
  RuleOperator,
  StopLossDraft,
} from '@/lib/rules/types';
import type { CandleInterval } from '@/api/types';
import { ConditionNodeCard } from './ConditionNodeCard';
import { IndicatorList } from './IndicatorList';
import { RuleValidationBar } from './RuleValidationBar';
import { StopLossEditor } from './StopLossEditor';
import type { RuleEditor } from './useRuleEditor';

export interface RuleBuilderProps {
  editor: RuleEditor;
  /** JSON paths the server rejected on save, e.g. `$.indicators[0].params.period`. */
  serverErrors?: Record<string, string[]>;
  interval?: CandleInterval;
}

export function RuleBuilder({ editor, serverErrors, interval }: RuleBuilderProps) {
  const { draft, apply, firstRef } = editor;

  // Client-only, never serialised. A strategy is written without knowing what interval it
  // will be run at, but warmup and timeframe compatibility mean nothing until one is
  // assumed, so the builder asks for one and keeps it out of the document.
  const [previewOverride, setPreviewOverride] = useState<CandleInterval | null>(null);
  const previewInterval = previewOverride ?? interval ?? 'FourHours';

  const localErrors = editor.diagnostics.filter((d) => d.severity === 'error');
  const diagnosticsByNode = useMemo(() => byNode(editor.diagnostics), [editor.diagnostics]);
  const docErrors = documentDiagnostics(editor.diagnostics);

  // Only ask the server once the document is structurally sound — a round trip
  // to be told what is already marked in the tree teaches nothing.
  const validation = useRuleValidation(
    editor.documentText,
    localErrors.length === 0,
    previewInterval,
  );

  /**
   * Save-time errors arrive keyed by JSON path, which `applyServerErrors`
   * cannot match — its field matching would never find
   * `$.indicators[0].params.period`. They are resolved onto nodes here instead.
   */
  const { resolved, unresolved } = useMemo(() => {
    const byNodeId = new Map<NodeId, string>();
    const orphans: string[] = [];

    for (const [path, messages] of Object.entries(serverErrors ?? {})) {
      const hit = resolveServerPath(path, editor.serialised.pathToNode);

      if (hit) {
        byNodeId.set(hit.nodeId, messages.join(' '));
      } else {
        orphans.push(`${path}: ${messages.join(' ')}`);
      }
    }

    return { resolved: byNodeId, unresolved: orphans };
  }, [serverErrors, editor.serialised.pathToNode]);

  function setIndicators(indicators: IndicatorDraft[]) {
    apply((current) => ({ ...current, indicators }));
  }

  function setStop(stopLoss: StopLossDraft) {
    apply((current) => ({ ...current, stopLoss }));
  }

  function toggleSide(side: EntrySide, on: boolean) {
    apply((current) => ({
      ...current,
      entry: { ...current.entry, [side]: on ? starterCondition(firstRef) : null },
    }));
  }

  /** Every tree action is the same shape: map over the side that owns the node. */
  function editSide(side: EntrySide, fn: (root: ConditionDraft) => ConditionDraft | null) {
    apply((current) => {
      const root = current.entry[side];

      return root ? { ...current, entry: { ...current.entry, [side]: fn(root) } } : current;
    });
  }

  function handlersFor(side: EntrySide) {
    return {
      onChangeOperator: (id: NodeId, op: RuleOperator) =>
        editSide(side, (root) => mapNode(root, id, (node) => changeOperator(node, op, firstRef))),
      onReplaceOperand: (operandId: NodeId, next: OperandDraft) =>
        editSide(side, (root) => replaceOperand(root, operandId, next)),
      onUpdateNode: (id: NodeId, patch: Partial<ConditionDraft>) =>
        editSide(side, (root) =>
          mapNode(root, id, (node) => ({ ...node, ...patch }) as ConditionDraft),
        ),
      onAddChild: (id: NodeId) => editSide(side, (root) => addChild(root, id, firstRef)),
      onRemove: (id: NodeId) => editSide(side, (root) => removeNodeById(root, id)),
      onWrap: (id: NodeId, op: 'And' | 'Or' | 'Not') =>
        editSide(side, (root) => wrapNode(root, id, op, firstRef)),
    };
  }

  return (
    <Stack gap="md">
      <Group justify="space-between" align="flex-end" wrap="wrap" gap="sm">
        <Box style={{ flex: '1 1 320px' }}>
          <RuleValidationBar
            localErrors={localErrors}
            validation={validation}
            interval={previewInterval}
          />
        </Box>

        <Select
          size="xs"
          label="Preview at"
          description="Not part of the rule"
          data={intervalOptions()}
          value={previewInterval}
          onChange={(value) => setPreviewOverride((value as CandleInterval) ?? null)}
          allowDeselect={false}
          w={150}
        />
      </Group>

      {unresolved.length > 0 && (
        <Alert color="red" variant="light" icon={<IconAlertTriangle size={18} />}>
          <Stack gap={2}>
            {unresolved.map((message) => (
              <Text key={message} size="sm">
                {message}
              </Text>
            ))}
          </Stack>
        </Alert>
      )}

      {docErrors.length > 0 && (
        <Alert color="orange" variant="light" icon={<IconAlertTriangle size={18} />}>
          <Stack gap={2}>
            {docErrors.map((error, index) => (
              <Text key={index} size="sm">
                {error.message}
              </Text>
            ))}
          </Stack>
        </Alert>
      )}

      <Tabs
        value={editor.mode}
        onChange={(value) => editor.setMode(value === 'json' ? 'json' : 'visual')}
      >
        <Tabs.List mb="md">
          <Tabs.Tab value="visual" leftSection={<IconSitemap size={16} />}>
            Builder
          </Tabs.Tab>
          <Tabs.Tab value="json" leftSection={<IconBraces size={16} />}>
            JSON
            {editor.jsonAuthoritative && ' ·'}
          </Tabs.Tab>
        </Tabs.List>

        <Tabs.Panel value="visual">
          <Stack gap="md">
            {editor.jsonAuthoritative && (
              <Alert color="blue" variant="light">
                This rule came from the JSON tab and will be saved exactly as written there. Editing
                below takes over and rewrites it — which normalises the document and changes its
                rule hash.
              </Alert>
            )}

            <IndicatorList
              indicators={draft.indicators}
              diagnostics={diagnosticsByNode}
              serverErrors={resolved}
              previewInterval={previewInterval}
              onChange={setIndicators}
            />

            <Grid gap="md">
              {(['long', 'short'] as const).map((side) => (
                <Grid.Col key={side} span={{ base: 12, lg: 6 }}>
                  <Card padding="md" h="100%">
                    <Stack gap="sm">
                      <Group justify="space-between" wrap="nowrap">
                        <Title order={5} tt="capitalize">
                          {side} entry
                        </Title>
                        <Switch
                          checked={draft.entry[side] !== null}
                          onChange={(event) => toggleSide(side, event.currentTarget.checked)}
                          aria-label={`Enable the ${side} entry`}
                        />
                      </Group>

                      {draft.entry[side] ? (
                        <ConditionNodeCard
                          node={draft.entry[side]!}
                          depth={1}
                          isRoot
                          indicators={draft.indicators}
                          diagnostics={diagnosticsByNode}
                          serverErrors={resolved}
                          {...handlersFor(side)}
                        />
                      ) : (
                        <Text size="sm" c="dimmed">
                          This strategy will not open {side} positions.
                        </Text>
                      )}
                    </Stack>
                  </Card>
                </Grid.Col>
              ))}
            </Grid>

            <StopLossEditor
              stop={draft.stopLoss}
              indicators={draft.indicators}
              diagnostics={editor.diagnostics}
              onChange={setStop}
            />
          </Stack>
        </Tabs.Panel>

        <Tabs.Panel value="json">
          <Stack gap="sm">
            {editor.jsonError && (
              <Alert color="red" variant="light" icon={<IconAlertTriangle size={18} />}>
                {editor.jsonError} — the builder still holds the last version that parsed.
              </Alert>
            )}

            <Textarea
              value={editor.jsonText}
              onChange={(event) => editor.setJsonText(event.currentTarget.value)}
              autosize
              minRows={20}
              maxRows={40}
              spellCheck={false}
              styles={{
                input: { fontFamily: 'var(--mantine-font-family-monospace)', fontSize: 13 },
              }}
            />

            <Group justify="space-between" wrap="wrap" gap="sm">
              <Text size="xs" c="dimmed" maw={460}>
                Saved exactly as written. Key order and whitespace do not matter, but expanding a
                bare number or adding a default field changes the rule hash that identifies which
                rule a finished run used.
              </Text>

              <Button
                size="compact-sm"
                variant="subtle"
                leftSection={<IconArrowBackUp size={14} />}
                disabled={!editor.jsonAuthoritative}
                onClick={editor.revertJson}
              >
                Revert to the builder&apos;s version
              </Button>
            </Group>
          </Stack>
        </Tabs.Panel>
      </Tabs>
    </Stack>
  );
}

export type { RuleDraft };
