import { ApiError } from '@/api/problem';

interface FieldErrorTarget {
  setFieldError: (path: string, error: string) => void;
  values: object;
}

/**
 * Map a 400's field-keyed `errors` object onto a Mantine form's inputs.
 *
 * The casing on the wire is not consistent: the domain raises validation
 * through `Guard`, so the key is whatever `nameof(...)` produced at the throw
 * site — `"Rating"` from `nameof(edit.Rating)` but `"stopLossPrice"` from a
 * parameter name. Matching is therefore case-insensitive, and anything that
 * matches no input is returned so the caller can show it somewhere visible
 * rather than swallowing it.
 */
export function applyServerErrors(
  form: FieldErrorTarget,
  error: unknown,
  aliases: Record<string, string> = {},
): string[] {
  if (!(error instanceof ApiError) || !error.errors) {
    return [];
  }

  const byLowerCase = new Map<string, string>();

  for (const key of Object.keys(form.values)) {
    byLowerCase.set(key.toLowerCase(), key);
  }

  for (const [from, to] of Object.entries(aliases)) {
    byLowerCase.set(from.toLowerCase(), to);
  }

  const unmatched: string[] = [];

  for (const [field, messages] of Object.entries(error.errors)) {
    const message = messages.join(' ');
    const target = byLowerCase.get(field.toLowerCase());

    if (target) {
      form.setFieldError(target, message);
    } else {
      unmatched.push(`${field}: ${message}`);
    }
  }

  return unmatched;
}

/** `''` means "not filled in", which the API wants as null rather than an empty string. */
export function blankToNull(value: string | null | undefined): string | null {
  const trimmed = value?.trim();
  return trimmed ? trimmed : null;
}

/** Mantine's NumberInput yields `'' | number`; the API wants `number | null`. */
export function toNumberOrNull(value: number | string | null | undefined): number | null {
  if (value === '' || value === null || value === undefined) {
    return null;
  }

  const parsed = typeof value === 'number' ? value : Number(value);

  return Number.isFinite(parsed) ? parsed : null;
}

/** Only send what changed: the journal endpoint treats an absent field as "leave alone". */
export function changed<T>(next: T, initial: T): T | undefined {
  return Object.is(next, initial) ? undefined : next;
}
