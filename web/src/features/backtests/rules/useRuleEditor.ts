import { useCallback, useEffect, useMemo, useReducer } from 'react';
import { useDebouncedValue } from '@mantine/hooks';
import { parseRuleDocument } from '@/lib/rules/parse';
import { serialiseRule, type SerialisedRule } from '@/lib/rules/serialise';
import { starterDraft } from '@/lib/rules/starter';
import { validateDraft, type Diagnostic } from '@/lib/rules/validate';
import type { RuleDraft } from '@/lib/rules/types';

export type EditorMode = 'visual' | 'json';

interface State {
  draft: RuleDraft;
  /** What the JSON tab shows. A projection of the draft, except while typed into. */
  jsonText: string;
  /**
   * True once the JSON tab has been edited. The typed text then becomes what is
   * saved, so a pasted document keeps its exact shape — and therefore its rule
   * hash, which is the identity by which two runs are judged to share a rule.
   */
  jsonAuthoritative: boolean;
  /** Set only by a `JSON.parse` failure; never by a semantic problem. */
  jsonError: string | null;
  mode: EditorMode;
}

type Action =
  | { type: 'apply'; fn: (draft: RuleDraft) => RuleDraft }
  | { type: 'json-changed'; text: string }
  | { type: 'json-parsed'; draft: RuleDraft }
  | { type: 'json-invalid'; message: string }
  | { type: 'set-mode'; mode: EditorMode }
  | { type: 'revert-json' }
  | { type: 'reset'; draft: RuleDraft };

function reducer(state: State, action: Action): State {
  switch (action.type) {
    case 'apply': {
      // A visual edit is authoritative and rewrites the JSON projection.
      const draft = action.fn(state.draft);

      return {
        ...state,
        draft,
        jsonText: serialiseRule(draft).text,
        jsonAuthoritative: false,
        jsonError: null,
      };
    }

    case 'json-changed':
      return { ...state, jsonText: action.text, jsonAuthoritative: true };

    case 'json-parsed':
      // The draft follows the text, but the text is left exactly as typed —
      // reformatting under the cursor is how this kind of editor becomes
      // unusable.
      return { ...state, draft: action.draft, jsonError: null };

    case 'json-invalid':
      // The draft is deliberately left alone: half-typed JSON should not
      // destroy the tree the user already had.
      return { ...state, jsonError: action.message };

    case 'set-mode':
      // Switching tabs rewrites nothing. Round-tripping text through the
      // builder would normalise it and change its hash.
      return { ...state, mode: action.mode };

    case 'revert-json':
      return {
        ...state,
        jsonText: serialiseRule(state.draft).text,
        jsonAuthoritative: false,
        jsonError: null,
      };

    case 'reset':
      return {
        draft: action.draft,
        jsonText: serialiseRule(action.draft).text,
        jsonAuthoritative: false,
        jsonError: null,
        mode: state.mode,
      };
  }
}

function initial(draft: RuleDraft): State {
  return {
    draft,
    jsonText: serialiseRule(draft).text,
    jsonAuthoritative: false,
    jsonError: null,
    mode: 'visual',
  };
}

export interface RuleEditor {
  draft: RuleDraft;
  mode: EditorMode;
  jsonText: string;
  jsonError: string | null;
  jsonAuthoritative: boolean;
  /** The draft's own serialisation, with the path maps for error resolution. */
  serialised: SerialisedRule;
  /** What will actually be sent — the typed text when the JSON tab owns it. */
  documentText: string;
  document: unknown;
  diagnostics: Diagnostic[];
  /** First declared indicator ref, used to seed new nodes meaningfully. */
  firstRef: string | undefined;
  apply: (fn: (draft: RuleDraft) => RuleDraft) => void;
  setMode: (mode: EditorMode) => void;
  setJsonText: (text: string) => void;
  revertJson: () => void;
  reset: (draft: RuleDraft) => void;
}

export function useRuleEditor(seed?: RuleDraft): RuleEditor {
  const [state, dispatch] = useReducer(reducer, seed ?? starterDraft(), initial);

  // Parsing on every keystroke would fight the user; 300 ms is short enough
  // that the tree keeps up and long enough that half-typed JSON is not parsed.
  const [debouncedJson] = useDebouncedValue(state.jsonText, 300);

  useEffect(() => {
    if (!state.jsonAuthoritative) {
      return;
    }

    try {
      dispatch({ type: 'json-parsed', draft: parseRuleDocument(JSON.parse(debouncedJson)) });
    } catch (cause) {
      dispatch({
        type: 'json-invalid',
        message: cause instanceof Error ? cause.message : 'That is not valid JSON.',
      });
    }
  }, [debouncedJson, state.jsonAuthoritative]);

  const serialised = useMemo(() => serialiseRule(state.draft), [state.draft]);

  const diagnostics = useMemo(() => validateDraft(state.draft), [state.draft]);

  // What gets validated and saved. When the JSON tab owns the document, the
  // user's exact text is sent so its hash survives.
  const documentText =
    state.jsonAuthoritative && state.jsonError === null ? state.jsonText : serialised.text;

  const document = useMemo(() => {
    try {
      return JSON.parse(documentText) as unknown;
    } catch {
      return serialised.document;
    }
  }, [documentText, serialised.document]);

  const apply = useCallback((fn: (draft: RuleDraft) => RuleDraft) => {
    dispatch({ type: 'apply', fn });
  }, []);

  return {
    draft: state.draft,
    mode: state.mode,
    jsonText: state.jsonText,
    jsonError: state.jsonError,
    jsonAuthoritative: state.jsonAuthoritative,
    serialised,
    documentText,
    document,
    diagnostics,
    firstRef: state.draft.indicators.find((indicator) => indicator.ref.trim())?.ref,
    apply,
    setMode: useCallback((mode: EditorMode) => dispatch({ type: 'set-mode', mode }), []),
    setJsonText: useCallback((text: string) => dispatch({ type: 'json-changed', text }), []),
    revertJson: useCallback(() => dispatch({ type: 'revert-json' }), []),
    reset: useCallback((draft: RuleDraft) => dispatch({ type: 'reset', draft }), []),
  };
}
