import { useMe } from '@/api/queries/auth';
import { TimeZoneContext } from '@/lib/timeZone';
import { resolveTimeZone } from '@/lib/time';
import { AppShell } from './AppShell';

/**
 * Resolves the trader's zone once for the whole authenticated tree. Until
 * `me` lands — and whenever the server holds a Windows zone id the browser
 * cannot resolve — instants fall back to the browser's own zone.
 */
export function AuthenticatedLayout() {
  const me = useMe();
  const timeZone = resolveTimeZone(me.data?.timeZoneId);

  return (
    <TimeZoneContext value={timeZone}>
      <AppShell />
    </TimeZoneContext>
  );
}
