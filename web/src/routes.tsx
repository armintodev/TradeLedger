/* eslint-disable react-refresh/only-export-components -- this is the route
   manifest: it exports the router alongside the small layout components the
   routes are built from, which is the point of the file. */
import { lazy, Suspense, type ReactNode } from 'react';
import { Center, Loader } from '@mantine/core';
import { createBrowserRouter, Outlet } from 'react-router';
import { AuthProvider } from '@/auth/AuthProvider';
import { RequireAuth } from '@/auth/RequireAuth';
import { AuthenticatedLayout } from '@/components/AuthenticatedLayout';
import { LoginPage } from '@/features/auth/LoginPage';
import { NotFoundPage } from '@/features/NotFoundPage';
import { DashboardPage } from '@/features/dashboard/DashboardPage';
import { TradesPage } from '@/features/trades/TradesPage';
import { TradeDetailPage } from '@/features/trades/TradeDetailPage';
import { NewTradePage } from '@/features/trades/NewTradePage';
import { ReviewPage } from '@/features/review/ReviewPage';

// Lazy route chunks. Charts, the calculator and the settings forms are all
// reachable but none of them belongs in the bundle that renders the review
// queue — see `web/SPEC.md` — Constraints.
const AnalyticsPage = lazy(() =>
  import('@/features/analytics/AnalyticsPage').then((m) => ({ default: m.AnalyticsPage })),
);

const PortfolioPage = lazy(() =>
  import('@/features/portfolio/PortfolioPage').then((m) => ({ default: m.PortfolioPage })),
);

const PlansPage = lazy(() =>
  import('@/features/plans/PlansPage').then((m) => ({ default: m.PlansPage })),
);

const NewPlanPage = lazy(() =>
  import('@/features/plans/NewPlanPage').then((m) => ({ default: m.NewPlanPage })),
);

const SettingsPage = lazy(() =>
  import('@/features/settings/SettingsPage').then((m) => ({ default: m.SettingsPage })),
);

const BacktestsPage = lazy(() =>
  import('@/features/backtests/BacktestsPage').then((m) => ({ default: m.BacktestsPage })),
);

const QueueRunPage = lazy(() =>
  import('@/features/backtests/runs/QueueRunPage').then((m) => ({ default: m.QueueRunPage })),
);

const RunDetailPage = lazy(() =>
  import('@/features/backtests/runs/RunDetailPage').then((m) => ({ default: m.RunDetailPage })),
);

const StrategyEditorPage = lazy(() =>
  import('@/features/backtests/strategies/StrategyEditorPage').then((m) => ({
    default: m.StrategyEditorPage,
  })),
);

function Chunk({ children }: { children: ReactNode }) {
  return (
    <Suspense
      fallback={
        <Center mih="40dvh">
          <Loader />
        </Center>
      }
    >
      {children}
    </Suspense>
  );
}

/** AuthProvider needs router context for its 401 redirect, so it sits here. */
function RootLayout() {
  return (
    <AuthProvider>
      <Outlet />
    </AuthProvider>
  );
}

export const router = createBrowserRouter([
  {
    element: <RootLayout />,
    children: [
      { path: '/login', element: <LoginPage /> },
      {
        element: <RequireAuth />,
        children: [
          {
            element: <AuthenticatedLayout />,
            children: [
              { index: true, element: <DashboardPage /> },
              { path: 'trades', element: <TradesPage /> },
              { path: 'trades/new', element: <NewTradePage /> },
              { path: 'trades/:id', element: <TradeDetailPage /> },
              { path: 'review', element: <ReviewPage /> },
              {
                path: 'analytics',
                element: (
                  <Chunk>
                    <AnalyticsPage />
                  </Chunk>
                ),
              },
              {
                path: 'portfolio',
                element: (
                  <Chunk>
                    <PortfolioPage />
                  </Chunk>
                ),
              },
              {
                path: 'plans',
                element: (
                  <Chunk>
                    <PlansPage />
                  </Chunk>
                ),
              },
              {
                path: 'plans/new',
                element: (
                  <Chunk>
                    <NewPlanPage />
                  </Chunk>
                ),
              },
              {
                path: 'settings',
                element: (
                  <Chunk>
                    <SettingsPage />
                  </Chunk>
                ),
              },
              {
                path: 'backtests',
                element: (
                  <Chunk>
                    <BacktestsPage />
                  </Chunk>
                ),
              },
              {
                path: 'backtests/runs/new',
                element: (
                  <Chunk>
                    <QueueRunPage />
                  </Chunk>
                ),
              },
              {
                path: 'backtests/runs/:id',
                element: (
                  <Chunk>
                    <RunDetailPage />
                  </Chunk>
                ),
              },
              {
                path: 'backtests/strategies/new',
                element: (
                  <Chunk>
                    <StrategyEditorPage />
                  </Chunk>
                ),
              },
              {
                path: 'backtests/strategies/:id',
                element: (
                  <Chunk>
                    <StrategyEditorPage />
                  </Chunk>
                ),
              },
              { path: '*', element: <NotFoundPage /> },
            ],
          },
        ],
      },
    ],
  },
]);
