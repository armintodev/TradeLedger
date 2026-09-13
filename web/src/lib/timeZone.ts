import { createContext, useContext } from 'react';

/**
 * The trader's own zone, resolved once from `me.timeZoneId` and shared, so
 * every instant on screen agrees. Falls back to the browser's zone, which is
 * also what happens when the server holds a Windows zone id the browser cannot
 * resolve.
 */
export const TimeZoneContext = createContext<string>(
  Intl.DateTimeFormat().resolvedOptions().timeZone,
);

export function useTimeZone(): string {
  return useContext(TimeZoneContext);
}
