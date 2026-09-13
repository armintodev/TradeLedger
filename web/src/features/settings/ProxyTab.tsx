import { useEffect, useState } from 'react';
import {
  Alert,
  Badge,
  Button,
  Card,
  Grid,
  Group,
  NumberInput,
  PasswordInput,
  Select,
  Stack,
  Switch,
  Text,
  TextInput,
  Title,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { IconShieldCheck, IconShieldLock } from '@tabler/icons-react';
import { useDeleteProxy, useProxy, useSetProxy } from '@/api/queries/proxy';
import { ErrorState, LoadingCards } from '@/components/States';
import { applyServerErrors, blankToNull } from '@/lib/forms';
import { notifySuccess } from '@/lib/notify';
import type { ProxyScheme } from '@/api/types';

const SCHEMES: { value: ProxyScheme; label: string }[] = [
  { value: 'Socks5', label: 'SOCKS5' },
  { value: 'Socks4', label: 'SOCKS4' },
  { value: 'Socks4a', label: 'SOCKS4a' },
  { value: 'Http', label: 'HTTP' },
  { value: 'Https', label: 'HTTPS' },
];

/**
 * Every request to Bitunix leaves through this proxy, because the API key is
 * allowlisted to one static IP. `Proxy:Required` defaults to true, so with
 * nothing resolved the client refuses the call rather than leaking your own
 * address — that refusal is the feature.
 */
export function ProxyTab() {
  const proxy = useProxy();
  const save = useSetProxy();
  const remove = useDeleteProxy();
  const [clearPassword, setClearPassword] = useState(false);

  const form = useForm({
    initialValues: {
      scheme: 'Socks5' as ProxyScheme,
      host: '',
      port: 1080 as number | string,
      username: '',
      password: '',
      enabled: true,
    },
    validate: {
      host: (value) => (value.trim() ? null : 'Enter the proxy host'),
      port: (value) => (Number(value) > 0 ? null : 'Enter the port'),
    },
  });

  useEffect(() => {
    if (!proxy.data?.configured) {
      return;
    }

    form.setValues({
      scheme: proxy.data.scheme ?? 'Socks5',
      host: proxy.data.host ?? '',
      port: proxy.data.port ?? 1080,
      username: proxy.data.username ?? '',
      // Write-only: the password is AES-GCM encrypted at rest and no endpoint
      // returns it, so there is nothing to prefill and nothing to leak.
      password: '',
      enabled: proxy.data.enabled,
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [proxy.data]);

  if (proxy.isPending) {
    return <LoadingCards count={1} height={260} />;
  }

  if (proxy.isError) {
    return <ErrorState error={proxy.error} onRetry={() => void proxy.refetch()} />;
  }

  const current = proxy.data;

  return (
    <Stack gap="md">
      <Card padding="md">
        <Group justify="space-between" wrap="wrap" gap="sm">
          <Group gap="sm">
            {current.configured && current.enabled ? (
              <IconShieldCheck size={22} color="var(--mantine-color-teal-6)" />
            ) : (
              <IconShieldLock size={22} color="var(--mantine-color-orange-6)" />
            )}
            <Stack gap={2}>
              <Group gap="xs">
                <Text size="sm" fw={600}>
                  {current.configured
                    ? `${current.scheme?.toLowerCase()}://${current.host}:${current.port}`
                    : 'No per-user proxy'}
                </Text>
                {current.configured && (
                  <Badge size="sm" variant="light" color={current.enabled ? 'teal' : 'gray'}>
                    {current.enabled ? 'Enabled' : 'Disabled'}
                  </Badge>
                )}
                {current.hasPassword && (
                  <Badge size="sm" variant="light" color="gray">
                    Password set
                  </Badge>
                )}
              </Group>
              <Text size="xs" c="dimmed">
                {current.configurationFallback
                  ? `Falling back to configuration: ${current.configurationFallback}`
                  : current.configured
                    ? 'Resolved from your user record.'
                    : 'Nothing resolves — exchange calls will be refused.'}
              </Text>
            </Stack>
          </Group>

          <Badge variant="light" color={current.required ? 'red' : 'gray'}>
            {current.required ? 'Proxy required' : 'Proxy optional'}
          </Badge>
        </Group>
      </Card>

      {current.required && !current.configured && !current.configurationFallback && (
        <Alert color="orange" title="Exchange calls are being refused">
          `Proxy:Required` is on and nothing resolves, so every Bitunix request — including
          verifying an API key — fails with `proxy_required` before a socket is opened. That is
          deliberate: a single request from the wrong address can get the key flagged.
        </Alert>
      )}

      <Card padding="md" maw={640}>
        <form
          onSubmit={form.onSubmit((values) =>
            save.mutate(
              {
                scheme: values.scheme,
                host: values.host.trim(),
                port: Number(values.port),
                username: blankToNull(values.username),
                password: clearPassword ? null : blankToNull(values.password),
                enabled: values.enabled,
                clearPassword: clearPassword ? true : null,
              },
              {
                onSuccess: (response) => {
                  notifySuccess(`Egress set to ${response.egress}.`);
                  form.setFieldValue('password', '');
                  setClearPassword(false);
                },
                onError: (error) => applyServerErrors(form, error),
              },
            ),
          )}
        >
          <Stack gap="sm">
            <Title order={5}>Egress proxy</Title>

            <Grid gap="sm">
              <Grid.Col span={{ base: 12, sm: 4 }}>
                <Select
                  label="Scheme"
                  description="Must be right — a SOCKS proxy given http:// silently never engages"
                  data={SCHEMES}
                  allowDeselect={false}
                  {...form.getInputProps('scheme')}
                />
              </Grid.Col>
              <Grid.Col span={{ base: 12, sm: 5 }}>
                <TextInput
                  label="Host"
                  placeholder="proxy.example.com"
                  {...form.getInputProps('host')}
                />
              </Grid.Col>
              <Grid.Col span={{ base: 12, sm: 3 }}>
                <NumberInput
                  label="Port"
                  hideControls
                  min={1}
                  max={65535}
                  {...form.getInputProps('port')}
                />
              </Grid.Col>

              <Grid.Col span={{ base: 12, sm: 6 }}>
                <TextInput
                  label="Username"
                  autoComplete="off"
                  {...form.getInputProps('username')}
                />
              </Grid.Col>
              <Grid.Col span={{ base: 12, sm: 6 }}>
                <PasswordInput
                  label="Password"
                  description={
                    current.hasPassword
                      ? 'A password is stored. Leave blank to keep it.'
                      : 'Encrypted at rest; never returned.'
                  }
                  autoComplete="new-password"
                  disabled={clearPassword}
                  {...form.getInputProps('password')}
                />
              </Grid.Col>

              <Grid.Col span={12}>
                <Stack gap="xs">
                  <Switch
                    label="Enabled"
                    description="Off means the configuration proxy is used instead, if one is set."
                    {...form.getInputProps('enabled', { type: 'checkbox' })}
                  />

                  {current.hasPassword && (
                    <Switch
                      label="Clear the stored password"
                      checked={clearPassword}
                      onChange={(event) => setClearPassword(event.currentTarget.checked)}
                    />
                  )}
                </Stack>
              </Grid.Col>
            </Grid>

            <Group justify="space-between">
              <Button
                variant="subtle"
                color="red"
                disabled={!current.configured}
                loading={remove.isPending}
                onClick={() =>
                  remove.mutate(undefined, {
                    onSuccess: () => {
                      notifySuccess('Proxy removed.');
                      form.reset();
                    },
                  })
                }
              >
                Remove proxy
              </Button>

              <Button type="submit" loading={save.isPending}>
                Save proxy
              </Button>
            </Group>
          </Stack>
        </form>
      </Card>
    </Stack>
  );
}
