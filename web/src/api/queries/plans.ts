import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../client';
import { queryKeys } from '../queryKeys';
import type {
  CreatePlanRequest,
  Guid,
  PlanResponse,
  PlanStatus,
  PositionSizeRequest,
  PositionSizeResult,
} from '../types';

export function usePlans(status?: PlanStatus) {
  return useQuery({
    queryKey: queryKeys.planList(status),
    queryFn: () => api.get<PlanResponse[]>('/api/plans', { query: { status } }),
  });
}

export function useCreatePlan() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: CreatePlanRequest) => api.post<PlanResponse>('/api/plans', body),
    meta: { handlesValidation: true },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.plans }),
  });
}

export function useAbandonPlan() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: Guid) => api.post<PlanResponse>(`/api/plans/${id}/abandon`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.plans }),
  });
}

/**
 * The position-size calculator. Deliberately a server call rather than client
 * arithmetic: the sizing that goes into a plan must be the same numbers the
 * domain would compute, fees included.
 */
export function useCalculatePosition() {
  return useMutation({
    mutationFn: (body: PositionSizeRequest) =>
      api.post<PositionSizeResult>('/api/plans/calculate', body),
    // An invalid stop is expected while the form is being typed into; the
    // calculator panel shows it inline instead of raising a toast per keystroke.
    meta: { handlesValidation: true },
  });
}
