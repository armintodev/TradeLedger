import { Alert, Badge, Code, Group, Loader, Stack, Text } from '@mantine/core';
import { IconAlertTriangle, IconCircleCheck } from '@tabler/icons-react';
import type { UseQueryResult } from '@tanstack/react-query';
import { warmupSpanLabel } from '@/lib/marketData';
import type { CandleInterval, RuleValidationResponse } from '@/api/types';
import type { Diagnostic } from '@/lib/rules/validate';

export interface RuleValidationBarProps {
  /** Structural problems found client-side; these preempt the round trip. */
  localErrors: Diagnostic[];
  validation: UseQueryResult<RuleValidationResponse, Error>;
  /** Used only to turn a warmup bar count into a span the user can picture. */
  interval?: CandleInterval;
}

/**
 * The verdict line.
 *
 * The server is the authority — it is the only thing that proves the serialiser
 * and the real parser agree, and the only source of the warmup count. Local
 * diagnostics are shown first because they are instant and land on the node,
 * and because sending a document already known to be broken teaches nothing.
 */
export function RuleValidationBar({ localErrors, validation, interval }: RuleValidationBarProps) {
  if (localErrors.length > 0) {
    return (
      <Alert color="red" variant="light" icon={<IconAlertTriangle size={18} />}>
        <Stack gap={2}>
          <Text size="sm" fw={500}>
            {localErrors.length} problem{localErrors.length === 1 ? '' : 's'} to fix
          </Text>
          {localErrors.slice(0, 4).map((error, index) => (
            <Text key={`${error.nodeId}-${index}`} size="xs">
              {error.message}
            </Text>
          ))}
          {localErrors.length > 4 && (
            <Text size="xs" c="dimmed">
              and {localErrors.length - 4} more, marked in the tree
            </Text>
          )}
        </Stack>
      </Alert>
    );
  }

  if (validation.isPending || validation.isFetching) {
    return (
      <Group gap="xs">
        <Loader size="xs" />
        <Text size="sm" c="dimmed">
          Checking the rule…
        </Text>
      </Group>
    );
  }

  if (validation.isError) {
    return (
      <Alert color="orange" variant="light" icon={<IconAlertTriangle size={18} />}>
        The rule could not be checked — the API did not answer. It will be validated again on save.
      </Alert>
    );
  }

  const result = validation.data;

  if (!result) {
    return null;
  }

  if (!result.isValid) {
    return (
      <Alert color="red" variant="light" icon={<IconAlertTriangle size={18} />}>
        <Stack gap={4}>
          <Text size="sm">{result.reason}</Text>
          {result.path && (
            <Text size="xs" c="dimmed">
              at <Code>{result.path}</Code>
            </Text>
          )}
        </Stack>
      </Alert>
    );
  }

  return (
    <Alert color="teal" variant="light" icon={<IconCircleCheck size={18} />}>
      <Group gap="sm" wrap="wrap">
        <Text size="sm" fw={500}>
          Valid
        </Text>

        {result.warmupBars !== null && (
          <Text size="sm">
            {/* Warmup silently extends how much history a run needs: a
                200-period EMA on 4h candles cannot signal for 600 bars. */}
            needs {result.warmupBars} bars of history before the first signal
            {interval
              ? ` — ${warmupSpanLabel(result.warmupBars, interval)} on ${interval === 'OneDay' ? 'daily' : 'these'} candles`
              : ''}
          </Text>
        )}

        <Group gap={4}>
          {result.hasLongEntry && (
            <Badge size="sm" variant="light" color="teal">
              Long
            </Badge>
          )}
          {result.hasShortEntry && (
            <Badge size="sm" variant="light" color="red">
              Short
            </Badge>
          )}
          {result.indicators?.map((indicator) => (
            <Badge key={indicator} size="sm" variant="light" color="gray">
              {indicator}
            </Badge>
          ))}
        </Group>
      </Group>
    </Alert>
  );
}
