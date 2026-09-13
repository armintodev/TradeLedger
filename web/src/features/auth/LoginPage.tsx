import { useEffect, useState } from 'react';
import {
  Alert,
  Button,
  Card,
  Center,
  PasswordInput,
  Stack,
  Text,
  TextInput,
  Title,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { IconAlertTriangle } from '@tabler/icons-react';
import { useNavigate, useSearchParams } from 'react-router';
import { useLogin } from '@/api/queries/auth';
import { ApiError } from '@/api/problem';
import { useAuth } from '@/auth/useAuth';

/**
 * Public signup is disabled by design — the owner account is seeded at startup
 * from configuration, so this form is the entire auth surface.
 */
export function LoginPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const { isAuthenticated, signIn } = useAuth();
  const login = useLogin();

  const [formError, setFormError] = useState<string | null>(null);

  const next = searchParams.get('next');
  const destination = next && next.startsWith('/') && !next.startsWith('//') ? next : '/';

  const form = useForm({
    initialValues: { email: '82.arminhabibi@gmail.com', password: '12345678' },
    validate: {
      email: (value) => (value.trim().length > 0 ? null : 'Enter your email'),
      password: (value) => (value.length > 0 ? null : 'Enter your password'),
    },
  });

  useEffect(() => {
    if (isAuthenticated) {
      void navigate(destination, { replace: true });
    }
  }, [isAuthenticated, destination, navigate]);

  function handleSubmit(values: { email: string; password: string }) {
    setFormError(null);

    login.mutate(values, {
      onSuccess: (response) => {
        signIn(response);
        void navigate(destination, { replace: true });
      },
      onError: (error) => {
        // Keep the email, drop the password.
        form.setFieldValue('password', '');

        if (error instanceof ApiError && error.isNetwork) {
          setFormError('Could not reach the API. Check that it is running, then try again.');
          return;
        }

        if (error instanceof ApiError && error.status === 401) {
          // The API returns the same 401 for an unknown email and a wrong
          // password on purpose, so it does not confirm which addresses exist.
          setFormError('Email or password is incorrect');
          return;
        }

        setFormError(
          error instanceof ApiError ? (error.detail ?? error.title) : 'Sign in failed. Try again.',
        );
      },
    });
  }

  return (
    <Center mih="100dvh" p="md">
      <Card padding="xl" w="100%" maw={400}>
        <Stack gap="lg">
          <Stack gap={4}>
            <Title order={2}>TradeLedger</Title>
            <Text c="dimmed" size="sm">
              Sign in with the seeded owner account.
            </Text>
          </Stack>

          {formError && (
            <Alert color="red" icon={<IconAlertTriangle size={18} />} role="alert">
              {formError}
            </Alert>
          )}

          <form onSubmit={form.onSubmit(handleSubmit)} noValidate>
            <Stack gap="md">
              <TextInput
                label="Email"
                type="email"
                autoComplete="username"
                autoFocus
                {...form.getInputProps('email')}
              />

              <PasswordInput
                label="Password"
                autoComplete="current-password"
                {...form.getInputProps('password')}
              />

              <Button type="submit" loading={login.isPending} fullWidth>
                Sign in
              </Button>
            </Stack>
          </form>
        </Stack>
      </Card>
    </Center>
  );
}
