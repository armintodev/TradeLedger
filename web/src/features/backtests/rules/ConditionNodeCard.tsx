import {
  ActionIcon,
  Badge,
  Card,
  Group,
  Menu,
  NumberInput,
  Select,
  Stack,
  Text,
  Tooltip,
} from '@mantine/core';
import { IconDots, IconPlus, IconTrash } from '@tabler/icons-react';
import {
  ALL_OPERATORS,
  FAMILY_OF,
  LIMITS,
  OPERATOR_LABELS,
  type ConditionDraft,
  type IndicatorDraft,
  type NodeId,
  type OperandDraft,
  type RuleOperator,
} from '@/lib/rules/types';
import type { Diagnostic } from '@/lib/rules/validate';
import { OperandEditor } from './OperandEditor';

export interface ConditionNodeCardProps {
  node: ConditionDraft;
  depth: number;
  indicators: IndicatorDraft[];
  diagnostics: Map<NodeId, Diagnostic[]>;
  /** Paths the server rejected, resolved onto nodes. */
  serverErrors: Map<NodeId, string>;
  onChangeOperator: (id: NodeId, op: RuleOperator) => void;
  onReplaceOperand: (operandId: NodeId, next: OperandDraft) => void;
  onUpdateNode: (id: NodeId, patch: Partial<ConditionDraft>) => void;
  onAddChild: (id: NodeId) => void;
  onRemove: (id: NodeId) => void;
  onWrap: (id: NodeId, op: 'And' | 'Or' | 'Not') => void;
  /** The root cannot be removed from inside itself; the side toggle owns that. */
  isRoot?: boolean;
}

const OPERATOR_GROUPS = [
  { group: 'Compare', items: ALL_OPERATORS.filter((op) => FAMILY_OF[op] === 'compare') },
  { group: 'Range', items: ALL_OPERATORS.filter((op) => FAMILY_OF[op] === 'between') },
  { group: 'Trend', items: ALL_OPERATORS.filter((op) => FAMILY_OF[op] === 'trend') },
  { group: 'Combine', items: ALL_OPERATORS.filter((op) => FAMILY_OF[op] === 'logical') },
  { group: 'Invert', items: ALL_OPERATORS.filter((op) => FAMILY_OF[op] === 'not') },
];

const OPERATOR_DATA = OPERATOR_GROUPS.map((group) => ({
  group: group.group,
  items: group.items.map((op) => ({ value: op, label: OPERATOR_LABELS[op] })),
}));

/**
 * One node of the condition tree.
 *
 * A total five-way switch on `family`. Nothing else in the app branches on
 * whether a node's child lives under `operands`, `operand`, `left`/`right` or
 * `left`/`low`/`high` — that mapping exists only in `serialise.ts`.
 */
export function ConditionNodeCard(props: ConditionNodeCardProps) {
  const { node, depth, diagnostics, serverErrors, isRoot } = props;

  const problems = diagnostics.get(node.id) ?? [];
  const serverError = serverErrors.get(node.id);
  const nodeError = problems.find((p) => !p.field)?.message ?? serverError;

  const atDepthLimit = depth >= LIMITS.maxDepth;

  return (
    <Card
      padding="sm"
      withBorder
      style={{
        borderColor: nodeError ? 'var(--mantine-color-red-6)' : undefined,
        borderLeftWidth: 3,
      }}
    >
      <Stack gap="xs">
        <Group justify="space-between" wrap="nowrap" gap="xs">
          <Select
            size="xs"
            data={OPERATOR_DATA}
            value={node.op}
            onChange={(value) => value && props.onChangeOperator(node.id, value as RuleOperator)}
            allowDeselect={false}
            w={190}
          />

          <Group gap={4} wrap="nowrap">
            {node.family === 'logical' && (
              <Tooltip
                label={
                  node.children.length >= LIMITS.maxOperands
                    ? `At most ${LIMITS.maxOperands} conditions`
                    : 'Add a condition'
                }
                withArrow
              >
                <ActionIcon
                  size="sm"
                  variant="subtle"
                  disabled={node.children.length >= LIMITS.maxOperands}
                  onClick={() => props.onAddChild(node.id)}
                  aria-label="Add a condition"
                >
                  <IconPlus size={14} />
                </ActionIcon>
              </Tooltip>
            )}

            <Menu position="bottom-end" withinPortal>
              <Menu.Target>
                <ActionIcon size="sm" variant="subtle" aria-label="More">
                  <IconDots size={14} />
                </ActionIcon>
              </Menu.Target>
              <Menu.Dropdown>
                <Menu.Label>Wrap this in</Menu.Label>
                <Menu.Item disabled={atDepthLimit} onClick={() => props.onWrap(node.id, 'And')}>
                  All of (And)
                </Menu.Item>
                <Menu.Item disabled={atDepthLimit} onClick={() => props.onWrap(node.id, 'Or')}>
                  Any of (Or)
                </Menu.Item>
                <Menu.Item disabled={atDepthLimit} onClick={() => props.onWrap(node.id, 'Not')}>
                  Not
                </Menu.Item>
                {atDepthLimit && (
                  <Menu.Label>Nesting is capped at {LIMITS.maxDepth} levels</Menu.Label>
                )}
                {!isRoot && (
                  <>
                    <Menu.Divider />
                    <Menu.Item
                      color="red"
                      leftSection={<IconTrash size={14} />}
                      onClick={() => props.onRemove(node.id)}
                    >
                      Remove
                    </Menu.Item>
                  </>
                )}
              </Menu.Dropdown>
            </Menu>
          </Group>
        </Group>

        {nodeError && (
          <Text size="xs" c="red">
            {nodeError}
          </Text>
        )}

        {renderBody(props)}
      </Stack>
    </Card>
  );
}

function renderBody(props: ConditionNodeCardProps) {
  const { node, indicators, diagnostics } = props;

  const operand = (value: OperandDraft, label?: string) => (
    <OperandEditor
      key={value.id}
      operand={value}
      indicators={indicators}
      diagnostics={diagnostics}
      onChange={(next) => props.onReplaceOperand(value.id, next)}
      label={label}
    />
  );

  switch (node.family) {
    case 'logical':
      return (
        <Stack
          gap="xs"
          pl="sm"
          style={{ borderLeft: '2px solid var(--mantine-color-default-border)' }}
        >
          {node.children.map((child) => (
            <ConditionNodeCard
              key={child.id}
              {...props}
              node={child}
              depth={props.depth + 1}
              isRoot={false}
            />
          ))}

          {node.children.length < LIMITS.minOperands && (
            <Text size="xs" c="red">
              {node.op} needs at least {LIMITS.minOperands} conditions.
            </Text>
          )}
        </Stack>
      );

    case 'not':
      return (
        <Stack
          gap="xs"
          pl="sm"
          style={{ borderLeft: '2px solid var(--mantine-color-default-border)' }}
        >
          <ConditionNodeCard {...props} node={node.child} depth={props.depth + 1} isRoot={false} />
        </Stack>
      );

    case 'compare':
      return (
        <Stack gap={6}>
          {operand(node.left, 'when')}
          {operand(node.right, 'the')}
        </Stack>
      );

    case 'between':
      return (
        <Stack gap={6}>
          {operand(node.left, 'when')}
          {operand(node.low, 'from')}
          {operand(node.high, 'to')}
        </Stack>
      );

    case 'trend':
      return (
        <Stack gap={6}>
          {operand(node.value, 'when')}
          <Group gap={6} align="center">
            <Text size="xs" c="dimmed" w={44}>
              for
            </Text>
            <NumberInput
              size="xs"
              value={node.bars}
              onChange={(value) =>
                props.onUpdateNode(node.id, { bars: Number(value) || 1 } as Partial<ConditionDraft>)
              }
              min={LIMITS.minBars}
              max={LIMITS.maxBars}
              error={diagnostics.get(node.id)?.find((p) => p.field === 'bars')?.message}
              w={100}
              hideControls
            />
            <Badge size="xs" variant="light" color="gray">
              bars
            </Badge>
          </Group>
        </Stack>
      );
  }
}
