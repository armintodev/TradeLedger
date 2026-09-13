import { useQuery } from '@tanstack/react-query';
import { useMemo } from 'react';
import { api } from '../client';
import { queryKeys } from '../queryKeys';
import type { TaxonomyKind, TaxonomyTermResponse } from '../types';

export type TaxonomyGroups = Record<TaxonomyKind, TaxonomyTermResponse[]>;

const EMPTY_GROUPS: TaxonomyGroups = {
  Strategy: [],
  MentalState: [],
  Mistake: [],
  Tracking: [],
  ChecklistItem: [],
  Timeframe: [],
  ExitType: [],
  EntryType: [],
};

/**
 * One call feeds every picker on the journal form. The API returns active terms
 * only unless `includeInactive` is set, so a retired strategy stays on the
 * trades that used it without being offered again.
 */
export function useTaxonomy() {
  const query = useQuery({
    queryKey: queryKeys.taxonomy,
    queryFn: () => api.get<TaxonomyTermResponse[]>('/api/taxonomy'),
    staleTime: 10 * 60 * 1000,
  });

  const groups = useMemo(() => {
    if (!query.data) {
      return EMPTY_GROUPS;
    }

    const grouped: TaxonomyGroups = {
      Strategy: [],
      MentalState: [],
      Mistake: [],
      Tracking: [],
      ChecklistItem: [],
      Timeframe: [],
      ExitType: [],
      EntryType: [],
    };

    for (const term of query.data) {
      grouped[term.kind]?.push(term);
    }

    for (const kind of Object.keys(grouped) as TaxonomyKind[]) {
      grouped[kind].sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name));
    }

    return grouped;
  }, [query.data]);

  return { ...query, groups };
}

/** Mantine's Select/MultiSelect data shape. */
export function toOptions(terms: TaxonomyTermResponse[]) {
  return terms.map((term) => ({ value: term.id, label: term.name }));
}
