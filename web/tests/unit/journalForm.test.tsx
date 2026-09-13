import { http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { JournalForm } from '@/features/trades/JournalForm';
import type { TaxonomyTermResponse, TradeDetailResponse } from '@/api/types';
import { renderWithProviders } from './render';
import { server } from './msw/server';

const BASE = 'http://localhost:5000';

const STRATEGY: TaxonomyTermResponse = {
  id: '11111111-1111-4111-8111-111111111111',
  kind: 'Strategy',
  name: 'Breakout',
  sortOrder: 1,
  isActive: true,
  colorHex: null,
  description: null,
};

const trade: TradeDetailResponse = {
  id: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
  accountId: 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',
  symbol: 'BTCUSDT',
  side: 'Long',
  origin: 'Synced',
  reviewState: 'Unreviewed',
  outcome: 'Win',
  isPlanned: false,
  exchangePositionId: '900123',
  openedAt: '2026-03-15T08:00:00+00:00',
  closedAt: '2026-03-15T10:14:33+00:00',
  marketSession: 'Tokyo, London',
  marketSessionLabel: 'Tokyo / London overlap',
  entryPrice: 64000,
  exitPrice: 65000,
  quantity: 0.5,
  positionMargin: 6400,
  leverage: 5,
  orderValue: 32000,
  marginMode: 'Isolated',
  positionMode: 'OneWay',
  orderType: 'Market',
  percentClosed: 100,
  liquidatedQuantity: null,
  liquidationPrice: null,
  stopLossPrice: null,
  takeProfitPrice: null,
  positionToAccountPercent: null,
  plannedStopLossPercent: null,
  accountRiskedPercent: null,
  plannedReturnR: null,
  grossProfitLoss: 500,
  fees: 19.2,
  funding: -1.4,
  netProfitLoss: 479.4,
  achievedReturnR: null,
  tradeGainPercent: null,
  accountChangePercent: null,
  balanceAfter: null,
  duration: '02:14:33',
  strategyName: 'Breakout',
  timeframeName: null,
  entryTypeName: null,
  exitTypeName: null,
  entryMentalStateName: null,
  exitMentalStateName: null,
  marketContext: null,
  rating: null,
  memo: null,
  tag: null,
  postTradeTag: null,
  tradePlanId: null,
  executions: [],
  mistakes: [],
  trackings: [],
  attachments: [],
};

function taxonomyHandler() {
  return http.get(`${BASE}/api/taxonomy`, () => HttpResponse.json([STRATEGY]));
}

describe('JournalForm', () => {
  it('lands a 400 field error on the input it names, whatever its casing', async () => {
    const user = userEvent.setup();

    server.use(
      taxonomyHandler(),
      http.patch(`${BASE}/api/trades/${trade.id}/journal`, () =>
        HttpResponse.json(
          {
            title: 'One or more fields are invalid',
            status: 400,
            code: 'validation_failed',
            traceId: '0HN2',
            // PascalCase, because the domain throws through `Guard` with
            // `nameof(edit.Rating)`. The form's field is `rating`.
            errors: { Rating: ['Rating must be between 1 and 5.'] },
          },
          { status: 400 },
        ),
      ),
    );

    renderWithProviders(<JournalForm trade={trade} mode="review" onSaved={vi.fn()} />);

    await user.click(await screen.findByRole('button', { name: /save & next/i }));

    expect(await screen.findByText('Rating must be between 1 and 5.')).toBeInTheDocument();
  });

  it('surfaces an error naming a field the form does not have instead of swallowing it', async () => {
    const user = userEvent.setup();

    server.use(
      taxonomyHandler(),
      http.patch(`${BASE}/api/trades/${trade.id}/journal`, () =>
        HttpResponse.json(
          {
            title: 'One or more fields are invalid',
            status: 400,
            code: 'validation_failed',
            errors: { SomethingElse: ['Not a field this form renders.'] },
          },
          { status: 400 },
        ),
      ),
    );

    renderWithProviders(<JournalForm trade={trade} mode="review" onSaved={vi.fn()} />);

    await user.click(await screen.findByRole('button', { name: /save & next/i }));

    expect(
      await screen.findByText(/SomethingElse: Not a field this form renders\./),
    ).toBeInTheDocument();
  });

  it('sends markReviewed in review mode and calls back with the saved trade', async () => {
    const user = userEvent.setup();
    const onSaved = vi.fn();

    const captured: { body?: Record<string, unknown> } = {};

    server.use(
      taxonomyHandler(),
      http.patch(`${BASE}/api/trades/${trade.id}/journal`, async ({ request }) => {
        captured.body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json({ ...trade, reviewState: 'Reviewed' });
      }),
    );

    renderWithProviders(<JournalForm trade={trade} mode="review" onSaved={onSaved} />);

    await user.click(await screen.findByRole('button', { name: /save & next/i }));

    await waitFor(() => expect(onSaved).toHaveBeenCalledOnce());

    expect(captured.body?.markReviewed).toBe(true);
  });

  it('does not mark reviewed when editing an existing trade', async () => {
    const user = userEvent.setup();

    const captured: { body?: Record<string, unknown> } = {};

    server.use(
      taxonomyHandler(),
      http.patch(`${BASE}/api/trades/${trade.id}/journal`, async ({ request }) => {
        captured.body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(trade);
      }),
    );

    renderWithProviders(<JournalForm trade={trade} mode="edit" onSaved={vi.fn()} />);

    await user.click(await screen.findByRole('button', { name: /^save$/i }));

    await waitFor(() => expect(captured.body).toBeDefined());

    expect(captured.body?.markReviewed).toBeUndefined();
  });

  it('pre-selects a picker by matching the response name back to a taxonomy id', async () => {
    server.use(taxonomyHandler());

    renderWithProviders(<JournalForm trade={trade} mode="edit" onSaved={vi.fn()} />);

    // TradeDetailResponse carries names, not ids, so the id is resolved from the
    // taxonomy list before the Select can show anything.
    expect(await screen.findByDisplayValue('Breakout')).toBeInTheDocument();
  });

  it('rewrites mistakes and trackings in full, so clearing them clears them', async () => {
    const user = userEvent.setup();

    const captured: { body?: Record<string, unknown> } = {};

    server.use(
      taxonomyHandler(),
      http.patch(`${BASE}/api/trades/${trade.id}/journal`, async ({ request }) => {
        captured.body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(trade);
      }),
    );

    renderWithProviders(<JournalForm trade={trade} mode="edit" onSaved={vi.fn()} />);

    await user.click(await screen.findByRole('button', { name: /^save$/i }));

    await waitFor(() => expect(captured.body).toBeDefined());

    expect(captured.body?.mistakeIds).toEqual([]);
    expect(captured.body?.trackingIds).toEqual([]);
  });
});
