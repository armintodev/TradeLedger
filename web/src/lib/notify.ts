import { notifications } from '@mantine/notifications';
import { ApiError } from '@/api/problem';

/**
 * Toasts, keyed off the API's error codes.
 *
 * Validation failures never come through here — a 400 carrying `errors` belongs
 * on the form's own inputs, and a toast on top of that is noise. See
 * `web/SPEC.md` — Error code mapping.
 */

export function notifySuccess(message: string, title?: string): void {
  notifications.show({ color: 'teal', title, message, autoClose: 3500 });
}

export function notifyInfo(message: string, title?: string): void {
  notifications.show({ color: 'blue', title, message, autoClose: 4000 });
}

export interface NotifyErrorOptions {
  /** Shown instead of the problem's own title. */
  title?: string;
  /** The form is rendering the field errors itself, so stay quiet. */
  handlesValidation?: boolean;
}

/**
 * Returns true when something was shown, so callers can tell a silenced error
 * (a cancelled request, a validation failure a form owns) from a surfaced one.
 */
export function notifyError(error: unknown, options: NotifyErrorOptions = {}): boolean {
  if (!(error instanceof ApiError)) {
    notifications.show({
      color: 'red',
      title: options.title ?? 'Something went wrong',
      message: error instanceof Error ? error.message : String(error),
      autoClose: 8000,
    });

    return true;
  }

  switch (error.code) {
    // The request was abandoned — usually a filter changed mid-flight. Never
    // worth a toast.
    case 'request_cancelled':
      return false;

    // The redirect to /login is already happening; a toast would flash and die
    // with the page.
    case 'not_authenticated':
      return false;

    case 'validation_failed':
      if (options.handlesValidation) {
        return false;
      }

      break;

    case 'proxy_required':
      notifications.show({
        color: 'orange',
        title: 'No egress proxy is configured',
        message:
          'Bitunix only accepts requests from the allowlisted IP, so the call was refused before a socket was opened. Configure the proxy under Settings → Proxy.',
        autoClose: false,
      });

      return true;

    case 'missing_reference':
      notifications.show({
        color: 'orange',
        title: 'One of the selected items no longer exists',
        message: 'The list has been refreshed — pick the value again and resubmit.',
        autoClose: 8000,
      });

      return true;

    case 'concurrent_update':
      notifications.show({
        color: 'orange',
        title: 'This changed while you were editing',
        message: 'The record has been reloaded. Review it and apply your change again.',
        autoClose: 10000,
      });

      return true;

    default:
      break;
  }

  notifications.show({
    color: error.isNetwork ? 'orange' : 'red',
    title: options.title ?? error.title,
    // A 422 message is written for the trader, so it is shown verbatim.
    message: buildMessage(error),
    autoClose: error.isNetwork ? 6000 : 10000,
  });

  return true;
}

function buildMessage(error: ApiError): string {
  const parts: string[] = [];

  if (error.detail) {
    parts.push(error.detail);
  }

  if (error.errors) {
    for (const [field, messages] of Object.entries(error.errors)) {
      parts.push(`${field}: ${messages.join(' ')}`);
    }
  }

  if (parts.length === 0) {
    parts.push(error.title);
  }

  if (error.traceId) {
    parts.push(`Trace ${error.traceId}`);
  }

  return parts.join('\n');
}
