import { Button, Center, Stack, Text, Title } from '@mantine/core';
import { Link } from 'react-router';

export function NotFoundPage() {
  return (
    <Center mih="60dvh">
      <Stack align="center" gap="sm">
        <Title order={2}>Not found</Title>
        <Text c="dimmed" size="sm">
          That page does not exist.
        </Text>
        <Button component={Link} to="/" variant="light">
          Back to the dashboard
        </Button>
      </Stack>
    </Center>
  );
}
