/**
 * Wire types for the TradeLedger API.
 *
 * Transcribed from the `XxxRequests.cs` / `XxxResponses.cs` records in
 * `src/TradeLedger.Api/Features/**` and the analytics contracts in
 * `src/TradeLedger.Core/Analytics/`. Property names are camelCase because
 * minimal APIs serialise with `JsonSerializerDefaults.Web`; enums are strings
 * because `Program.cs` registers `JsonStringEnumConverter`.
 *
 * `npm run api:types` regenerates `schema.d.ts` from the live OpenAPI document
 * (Development only). Run it whenever the API contract changes and reconcile
 * anything below that no longer matches.
 */

// ---------------------------------------------------------------- enums

export type AccountKind =
  'ExchangeFutures' | 'ExchangeSpot' | 'ExchangeWallet' | 'ExternalWallet' | 'ManualVenue';

export type SyncMode = 'Manual' | 'Api';
export type Venue = 'Manual' | 'Bitunix';
export type TradeSide = 'Long' | 'Short';
export type MarginMode = 'Isolated' | 'Cross';
export type PositionMode = 'OneWay' | 'Hedge';
export type TradeOrigin = 'Manual' | 'Synced';
export type ReviewState = 'Unreviewed' | 'Reviewed';
export type TradeOutcome = 'Open' | 'Win' | 'Loss' | 'Breakeven';
export type ExecutionRole = 'Open' | 'Increase' | 'Reduce' | 'Close' | 'Liquidation';
export type OrderType = 'Unknown' | 'Market' | 'Limit';
export type PlanStatus = 'Draft' | 'Active' | 'Linked' | 'Abandoned' | 'Expired';
export type TransferDirection = 'Deposit' | 'Withdrawal' | 'Internal';
export type HoldingKind = 'Spot' | 'Wallet' | 'LiquidityPool' | 'Farm';
export type SyncRunStatus = 'Running' | 'Succeeded' | 'Failed';
export type ProxyScheme = 'Http' | 'Https' | 'Socks5' | 'Socks4' | 'Socks4a';

export type TaxonomyKind =
  | 'Strategy'
  | 'MentalState'
  | 'Mistake'
  | 'Tracking'
  | 'ChecklistItem'
  | 'Timeframe'
  | 'ExitType'
  | 'EntryType';

export type BreakdownDimension =
  | 'Strategy'
  | 'Symbol'
  | 'Side'
  | 'Timeframe'
  | 'MarketSession'
  | 'EntryMentalState'
  | 'ExitMentalState'
  | 'DayOfWeek'
  | 'HourOfDay'
  | 'Planned';

/**
 * `MarketSession` is a `[Flags]` enum, so a combined value arrives as
 * `"Tokyo, London"` rather than a single member. Single flags are what the
 * `marketSession` query parameter accepts.
 */
export type MarketSessionFlag = 'Tokyo' | 'London' | 'NewYork';
export type MarketSessionValue = string;

/** An ISO 8601 instant with offset, as `DateTimeOffset` serialises. */
export type Instant = string;

/** A `TimeSpan`: `"HH:mm:ss"`, or `"d.HH:mm:ss"` past 24 hours. */
export type Duration = string;

export type Guid = string;

// ---------------------------------------------------------------- shared

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
}

// ---------------------------------------------------------------- auth

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  expiresAt: Instant;
  email: string;
  displayName: string | null;
}

export interface MeResponse {
  id: Guid;
  email: string;
  displayName: string | null;
  startingBalance: number;
  journalStartedAt: Instant | null;
  defaultRiskPerTrade: number | null;
  timeZoneId: string;
}

// ---------------------------------------------------------------- accounts

export interface AccountResponse {
  id: Guid;
  name: string;
  kind: AccountKind;
  venue: Venue;
  syncMode: SyncMode;
  quoteAsset: string;
  isActive: boolean;
  trackedFrom: Instant | null;
  apiKeyHint: string | null;
  credentialEnabled: boolean;
  lastVerifiedAt: Instant | null;
  verifiedViaEgress: string | null;
}

export interface CreateAccountRequest {
  name: string;
  kind: AccountKind;
  venue: Venue;
  syncMode: SyncMode;
  quoteAsset?: string | null;
  trackedFrom?: Instant | null;
}

export interface CreatedAccountResponse {
  id: Guid;
}

export interface SetCredentialRequest {
  apiKey: string;
  apiSecret: string;
  label?: string | null;
}

export interface CredentialResponse {
  apiKeyHint: string;
  lastVerifiedAt: Instant | null;
  verifiedViaEgress: string | null;
}

// ---------------------------------------------------------------- taxonomy

export interface TaxonomyTermResponse {
  id: Guid;
  kind: TaxonomyKind;
  name: string;
  sortOrder: number;
  isActive: boolean;
  colorHex: string | null;
  description: string | null;
}

// ---------------------------------------------------------------- trades

export interface MarketContextResponse {
  total2: string | null;
  btcDominance: string | null;
  usdtDominance: string | null;
  marketTrend: string | null;
  sma: string | null;
  marketSession: string | null;
  btcPair: string | null;
  rsi: string | null;
  volume: string | null;
  candleShape: string | null;
}

export type MarketContextRequest = MarketContextResponse;

export interface TradeListItem {
  id: Guid;
  accountId: Guid;
  symbol: string;
  side: TradeSide;
  origin: TradeOrigin;
  reviewState: ReviewState;
  outcome: TradeOutcome;
  isPlanned: boolean;
  openedAt: Instant;
  closedAt: Instant | null;
  marketSession: MarketSessionValue;
  entryPrice: number;
  exitPrice: number | null;
  quantity: number;
  leverage: number;
  fees: number;
  funding: number;
  netProfitLoss: number;
  achievedReturnR: number | null;
  plannedReturnR: number | null;
  strategyName: string | null;
  rating: number | null;
  duration: Duration | null;
}

export interface ExecutionResponse {
  id: Guid;
  role: ExecutionRole;
  side: TradeSide;
  price: number;
  quantity: number;
  fee: number;
  feeAsset: string;
  realizedProfitLoss: number | null;
  executedAt: Instant;
  exchangeTradeId: string | null;
  exchangeOrderId: string | null;
  orderType: OrderType;
}

export interface TradeTermResponse {
  termId: Guid;
  name: string;
  note: string | null;
  estimatedCost: number | null;
}

export interface AttachmentResponse {
  id: Guid;
  slot: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  caption: string | null;
  uploadedAt: Instant;
}

export interface TradeDetailResponse {
  id: Guid;
  accountId: Guid;
  symbol: string;
  side: TradeSide;
  origin: TradeOrigin;
  reviewState: ReviewState;
  outcome: TradeOutcome;
  isPlanned: boolean;
  exchangePositionId: string | null;
  openedAt: Instant;
  closedAt: Instant | null;
  marketSession: MarketSessionValue;
  marketSessionLabel: string;
  entryPrice: number;
  exitPrice: number | null;
  quantity: number;
  positionMargin: number | null;
  leverage: number;
  orderValue: number | null;
  marginMode: MarginMode | null;
  positionMode: PositionMode | null;
  orderType: OrderType;
  percentClosed: number | null;
  liquidatedQuantity: number | null;
  liquidationPrice: number | null;
  stopLossPrice: number | null;
  takeProfitPrice: number | null;
  positionToAccountPercent: number | null;
  plannedStopLossPercent: number | null;
  accountRiskedPercent: number | null;
  plannedReturnR: number | null;
  grossProfitLoss: number;
  fees: number;
  funding: number;
  netProfitLoss: number;
  achievedReturnR: number | null;
  tradeGainPercent: number | null;
  accountChangePercent: number | null;
  balanceAfter: number | null;
  duration: Duration | null;
  strategyName: string | null;
  timeframeName: string | null;
  entryTypeName: string | null;
  exitTypeName: string | null;
  entryMentalStateName: string | null;
  exitMentalStateName: string | null;
  marketContext: MarketContextResponse | null;
  rating: number | null;
  memo: string | null;
  tag: string | null;
  postTradeTag: string | null;
  tradePlanId: Guid | null;
  executions: ExecutionResponse[];
  mistakes: TradeTermResponse[];
  trackings: TradeTermResponse[];
  attachments: AttachmentResponse[];
}

export interface JournalTradeRequest {
  strategyId?: Guid | null;
  timeframeId?: Guid | null;
  entryTypeId?: Guid | null;
  exitTypeId?: Guid | null;
  entryMentalStateId?: Guid | null;
  exitMentalStateId?: Guid | null;
  marketContext?: MarketContextRequest | null;
  stopLossPrice?: number | null;
  takeProfitPrice?: number | null;
  mistakeIds?: Guid[] | null;
  trackingIds?: Guid[] | null;
  rating?: number | null;
  memo?: string | null;
  tag?: string | null;
  postTradeTag?: string | null;
  markReviewed?: boolean | null;
}

export interface CreateManualTradeRequest {
  accountId: Guid;
  symbol: string;
  side: TradeSide;
  openedAt: Instant;
  closedAt?: Instant | null;
  entryPrice: number;
  exitPrice?: number | null;
  quantity: number;
  leverage?: number | null;
  positionMargin?: number | null;
  fees?: number | null;
  funding?: number | null;
  stopLossPrice?: number | null;
  takeProfitPrice?: number | null;
  strategyId?: Guid | null;
  memo?: string | null;
}

// ---------------------------------------------------------------- analytics

export interface PerformanceSummary {
  totalTrades: number;
  winningTrades: number;
  losingTrades: number;
  breakevenTrades: number;
  openPositions: number;
  winRate: number;
  grossProfitLoss: number;
  totalFees: number;
  totalFunding: number;
  netProfitLoss: number;
  averageWin: number;
  averageLoss: number;
  profitFactor: number | null;
  expectancy: number;
  averageAchievedR: number | null;
  averagePlannedR: number | null;
  plannedTradeCount: number;
  unplannedTradeCount: number;
  plannedNetProfitLoss: number;
  unplannedNetProfitLoss: number;
  longestWinStreak: number;
  longestLossStreak: number;
  averageDuration: Duration | null;
}

export interface EquityPoint {
  at: Instant;
  equity: number;
}

export interface EquityCurve {
  points: EquityPoint[];
  startEquity: number;
  currentEquity: number;
  peakEquity: number;
  maxDrawdown: number;
  maxDrawdownPercent: number;
  maxDrawdownAt: Instant | null;
  currentDrawdown: number;
  currentDrawdownPercent: number;
}

export interface BreakdownRow {
  key: string;
  tradeCount: number;
  winCount: number;
  winRate: number;
  netProfitLoss: number;
  averageR: number | null;
}

export interface MistakeCost {
  mistake: string;
  occurrences: number;
  totalCost: number;
}

// ---------------------------------------------------------------- portfolio

export interface HoldingResponse {
  id: Guid;
  accountId: Guid;
  kind: HoldingKind;
  asset: string;
  quantity: number;
  averageEntryPrice: number | null;
  entryValueUsd: number | null;
  currentPrice: number | null;
  currentValueUsd: number | null;
  pricedAt: Instant | null;
  poolName: string | null;
  farmApr: number | null;
  isFarmed: boolean;
  openedAt: Instant;
  closedAt: Instant | null;
  note: string | null;
}

export interface CreateHoldingRequest {
  accountId: Guid;
  kind: HoldingKind;
  asset: string;
  quantity: number;
  averageEntryPrice?: number | null;
  entryValueUsd?: number | null;
  poolName?: string | null;
  farmApr?: number | null;
  isFarmed?: boolean | null;
  openedAt: Instant;
  note?: string | null;
}

export interface RepriceHoldingRequest {
  price: number;
  pricedAt?: Instant | null;
}

export interface CloseHoldingRequest {
  closedAt?: Instant | null;
}

export interface TransferResponse {
  id: Guid;
  fromAccountId: Guid | null;
  toAccountId: Guid | null;
  direction: TransferDirection;
  asset: string;
  amount: number;
  fee: number;
  valueUsd: number | null;
  writeOff: boolean;
  network: string | null;
  txHash: string | null;
  counterparty: string | null;
  note: string | null;
  occurredAt: Instant;
}

export interface CreateTransferRequest {
  fromAccountId?: Guid | null;
  toAccountId?: Guid | null;
  direction: TransferDirection;
  asset: string;
  amount: number;
  fee?: number | null;
  valueUsd?: number | null;
  writeOff?: boolean | null;
  network?: string | null;
  txHash?: string | null;
  counterparty?: string | null;
  note?: string | null;
  occurredAt: Instant;
}

export interface BalanceSnapshotResponse {
  id: Guid;
  accountId: Guid;
  asset: string;
  walletBalance: number;
  available: number;
  frozen: number;
  margin: number;
  unrealizedPnl: number;
  equity: number;
  bonus: number;
  capturedAt: Instant;
  isManual: boolean;
}

export interface CreateSnapshotRequest {
  accountId: Guid;
  asset?: string | null;
  walletBalance: number;
  available?: number | null;
  unrealizedPnl?: number | null;
  capturedAt?: Instant | null;
}

// ---------------------------------------------------------------- plans

export interface PlanResponse {
  id: Guid;
  accountId: Guid | null;
  symbol: string;
  side: TradeSide;
  status: PlanStatus;
  strategyName: string | null;
  timeframeName: string | null;
  entryMentalStateName: string | null;
  plannedEntryPrice: number;
  plannedStopLossPrice: number;
  plannedTakeProfitPrice: number | null;
  riskFraction: number;
  plannedRiskReward: number;
  leverage: number;
  averageFeeRate: number | null;
  plannedQuantity: number | null;
  plannedOrderValue: number | null;
  plannedMargin: number | null;
  estimatedProfit: number | null;
  estimatedLoss: number | null;
  balanceAtPlanning: number | null;
  marketContext: MarketContextResponse | null;
  notes: string | null;
  expiresAt: Instant | null;
  createdAt: Instant;
  linkedTradeId: Guid | null;
}

export interface CreatePlanRequest {
  accountId?: Guid | null;
  symbol: string;
  side: TradeSide;
  entryPrice: number;
  stopLossPrice: number;
  takeProfitPrice?: number | null;
  riskFraction: number;
  riskReward: number;
  leverage?: number | null;
  averageFeeRate?: number | null;
  balance: number;
  strategyId?: Guid | null;
  timeframeId?: Guid | null;
  entryMentalStateId?: Guid | null;
  marketContext?: MarketContextRequest | null;
  notes?: string | null;
  expiresAt?: Instant | null;
}

export interface PositionSizeRequest {
  balance: number;
  entryPrice: number;
  stopLossPrice: number;
  riskFraction: number;
  riskReward?: number;
  leverage?: number;
  averageFeeRate?: number | null;
}

export interface PositionSizeResult {
  side: string;
  stopToEntryRatio: number;
  riskAmount: number;
  quantity: number;
  orderValue: number;
  margin: number;
  marginQuantity: number;
  takeProfitPrice: number;
  estimatedProfit: number;
  estimatedLoss: number;
  feesEntryPlusTakeProfit: number;
  feesEntryPlusStop: number;
}

// ---------------------------------------------------------------- sync

export interface SyncStatusResponse {
  accountId: Guid;
  endpoint: string;
  lastSyncedAt: Instant | null;
  lastRecordAt: Instant | null;
  backfillComplete: boolean;
}

export interface SyncRunResponse {
  id: Guid;
  accountId: Guid;
  endpoint: string;
  status: SyncRunStatus;
  isBackfill: boolean;
  startedAt: Instant;
  finishedAt: Instant | null;
  recordsSeen: number;
  recordsWritten: number;
  requestsMade: number;
  error: string | null;
}

export interface SyncOutcome {
  ran: boolean;
  recordsSeen: number;
  recordsWritten: number;
}

// ---------------------------------------------------------------- proxy

export interface ProxyResponse {
  configured: boolean;
  enabled: boolean;
  scheme: ProxyScheme | null;
  host: string | null;
  port: number | null;
  username: string | null;
  hasPassword: boolean;
  required: boolean;
  configurationFallback: string | null;
}

export interface SetProxyRequest {
  scheme: ProxyScheme;
  host: string;
  port: number;
  username?: string | null;
  password?: string | null;
  enabled?: boolean | null;
  clearPassword?: boolean | null;
}

export interface ProxyUpdatedResponse {
  egress: string;
  enabled: boolean;
}

// ------------------------------------------------- market data (enums)

export type CandleSource = 'BinanceFutures' | 'BinanceSpot' | 'CsvImport';

/**
 * Note the deliberate holes in the underlying numbering — 3m and 5m do not
 * exist. Never derive this from an index.
 *
 * `OneMinute` is drill-down only: it exists so the engine can resolve which of
 * a stop or a target was hit first inside a larger bar. It is legal for
 * backfill, import, gaps, coverage and delete, and rejected by the backtest
 * queue endpoint.
 */
export type CandleInterval =
  | 'OneMinute'
  | 'FifteenMinutes'
  | 'ThirtyMinutes'
  | 'OneHour'
  | 'TwoHours'
  | 'FourHours'
  | 'SixHours'
  | 'TwelveHours'
  | 'OneDay'
  | 'OneWeek';

export type MarketDataJobStatus = 'Queued' | 'Running' | 'Succeeded' | 'Failed' | 'Cancelled';

// ------------------------------------------------- backtests (enums)

export type BacktestAccountMode = 'Sequential' | 'Independent';

/** `WhatIf` is scaffolding — there is no engine and no endpoint that queues one. */
export type BacktestKind = 'RuleEngine' | 'WhatIf';

export type BacktestStatus = 'Queued' | 'Running' | 'Succeeded' | 'Failed' | 'Cancelled';

/**
 * Set from the request's `allowGaps` at queue time and never corrected, so it
 * means "gaps were permitted", not "gaps were present". See
 * `docs/backtest-impl.md` §1.
 */
export type DataQuality = 'Clean' | 'Gapped';

export type BacktestExitReason = 'StopLoss' | 'TakeProfit' | 'Liquidation' | 'EndOfData';

/** How a trade's exit was decided when one bar held both the stop and the target. */
export type IntrabarResolution =
  'Unambiguous' | 'ResolvedByMinute' | 'AssumedWithinMinute' | 'AssumedNoMinuteData';

// ------------------------------------------------- market data

export interface MarketDataCoverageResponse {
  source: CandleSource;
  symbol: string;
  interval: CandleInterval;
  rowCount: number;
  firstOpenTime: Instant | null;
  lastOpenTime: Instant | null;
  isTradeable: boolean;
}

/**
 * A run of consecutive missing bars, not a single timestamp.
 *
 * `from` and `to` are the open times of the first and last **missing** bar, so a
 * one-bar gap has `from === to`. Backfilling a gap verbatim is therefore
 * rejected with `empty_backfill_window` — use `backfillWindowFor` in
 * `lib/marketData.ts`, which adds one interval to `to`.
 */
export interface CandleGapResponse {
  from: Instant;
  to: Instant;
  missingCount: number;
}

export interface BackfillRequest {
  source: CandleSource;
  symbol: string;
  interval: CandleInterval;
  from: Instant;
  to: Instant;
  includeFundingRates?: boolean | null;
}

export interface BackfillJobResponse {
  id: Guid;
  source: CandleSource;
  symbol: string;
  interval: CandleInterval;
  from: Instant;
  to: Instant;
  status: MarketDataJobStatus;
  candlesWritten: number;
  fundingRatesWritten: number;
  /** Time-based, written once per 30-day chunk — not a smooth progress bar. */
  progressPercent: number;
  queuedAt: Instant;
  startedAt: Instant | null;
  finishedAt: Instant | null;
  error: string | null;
}

export interface CandleImportResponse {
  id: Guid;
  fileName: string;
  source: CandleSource;
  symbol: string;
  interval: CandleInterval;
  rowsParsed: number;
  rowsInserted: number;
  rowsSkippedAsDuplicate: number;
  firstOpenTime: Instant | null;
  lastOpenTime: Instant | null;
  warnings: string[];
}

export interface DeleteCandlesResponse {
  deleted: number;
}

// ------------------------------------------------- backtest accounts

export interface BacktestAccountResponse {
  id: Guid;
  name: string;
  description: string | null;
  startingBalance: number;
  /** Starting balance plus every succeeded run's net, on a sequential account. */
  currentBalance: number;
  netProfitLoss: number;
  currency: string;
  mode: BacktestAccountMode;
  backtestStrategyId: Guid | null;
  succeededRuns: number;
  isActive: boolean;
  createdAt: Instant;
}

export interface CreateBacktestAccountRequest {
  name: string;
  startingBalance: number;
  description?: string | null;
  currency?: string | null;
  mode?: BacktestAccountMode | null;
  backtestStrategyId?: Guid | null;
}

/** Every field is an optional patch. `backtestStrategyId` is not updatable. */
export interface UpdateBacktestAccountRequest {
  name?: string | null;
  description?: string | null;
  mode?: BacktestAccountMode | null;
  isActive?: boolean | null;
}

// ------------------------------------------------- backtest strategies

export interface BacktestStrategyResponse {
  id: Guid;
  name: string;
  description: string | null;
  ruleHash: string;
  version: number;
  strategyTermId: Guid | null;
  isActive: boolean;
  createdAt: Instant;
  updatedAt: Instant;
  /** Populated only by `GET /{id}` and `PUT /{id}` — null on the list and on create. */
  rule: unknown | null;
}

export interface SaveBacktestStrategyRequest {
  name: string;
  /** The rule document inline, not a string. */
  rule: unknown;
  description?: string | null;
  strategyTermId?: Guid | null;
}

/** Always answers 200, even for a rejected rule. `isValid` is the discriminant. */
export interface RuleValidationResponse {
  isValid: boolean;
  warmupBars: number | null;
  /** Echoed as `"fast (Ema 20)"`. */
  indicators: string[] | null;
  hasLongEntry: boolean | null;
  hasShortEntry: boolean | null;
  /** A JSON path into the rule tree, e.g. `$.indicators[0].params.period`. */
  path: string | null;
  reason: string | null;
}

export interface IndicatorDescriptionResponse {
  type: string;
  outputs: string[];
  takesSource: boolean;
  warmupMultiplier: number;
  minPeriod: number;
  maxPeriod: number;
  /** Per type, not globally Close: Highest reads highs and Lowest lows. */
  defaultSource: 'Close' | 'Open' | 'High' | 'Low' | 'Volume';
}

// ------------------------------------------------- backtest runs

export interface QueueBacktestRequest {
  backtestAccountId: Guid;
  from: Instant;
  to: Instant;
  /** Optional on the wire, but a run without one fails seconds after it starts. */
  backtestStrategyId?: Guid | null;
  symbol?: string | null;
  source?: CandleSource | null;
  interval?: CandleInterval | null;
  riskPercentPerPosition?: number | null;
  riskRewardRatio?: number | null;
  leverage?: number | null;
  takerFeeRate?: number | null;
  /** Accepted, stored, and never used — every simulated fill is a taker. */
  makerFeeRate?: number | null;
  slippageRate?: number | null;
  maintenanceMarginRate?: number | null;
  includeFunding?: boolean | null;
  allowGaps?: boolean | null;
}

/**
 * `skippedNoCandleData` and `liquidationRiskCount` are declared by the API but
 * never assigned, so they are always 0. They are deliberately absent here —
 * see `docs/backtest-impl.md` §5.
 */
export interface BacktestResultSummary {
  performance: PerformanceSummary | null;
  maxIntrabarDrawdown: number;
  maxIntrabarDrawdownPercent: number;
  ambiguousSignals: number;
  skippedInvalidStop: number;
  skippedInsufficientMargin: number;
  openAtEndOfData: number;
  resolvedUnambiguous: number;
  resolvedByMinute: number;
  assumedWithinMinute: number;
  assumedNoMinuteData: number;
}

export interface BacktestRunResponse {
  id: Guid;
  backtestAccountId: Guid;
  kind: BacktestKind;
  status: BacktestStatus;
  backtestStrategyId: Guid | null;
  /** The run's frozen rule identity. The rule itself is not exposed. */
  ruleHash: string | null;
  symbol: string | null;
  source: CandleSource | null;
  interval: CandleInterval | null;
  from: Instant;
  to: Instant;
  openingBalance: number;
  closingBalance: number | null;
  riskPercentPerPosition: number;
  riskRewardRatio: number;
  leverage: number;
  takerFeeRate: number;
  slippageRate: number;
  maintenanceMarginRate: number;
  includeFunding: boolean;
  allowGaps: boolean;
  dataQuality: DataQuality;
  engineVersion: number;
  totalBars: number;
  barsProcessed: number;
  progressPercent: number;
  cancellationRequested: boolean;
  queuedAt: Instant;
  startedAt: Instant | null;
  finishedAt: Instant | null;
  error: string | null;
  result: BacktestResultSummary | null;
  warnings: string[];
}

export interface BacktestTradeResponse {
  id: Guid;
  sequence: number;
  symbol: string;
  side: TradeSide;
  openedAt: Instant;
  closedAt: Instant | null;
  barsInTrade: number;
  entryPrice: number;
  exitPrice: number | null;
  quantity: number;
  leverage: number;
  positionMargin: number;
  orderValue: number;
  stopLossPrice: number;
  takeProfitPrice: number;
  liquidationPrice: number;
  grossProfitLoss: number;
  fees: number;
  funding: number;
  netProfitLoss: number;
  achievedReturnR: number | null;
  plannedReturnR: number | null;
  tradeGainPercent: number | null;
  balanceAfter: number;
  duration: Duration | null;
  outcome: TradeOutcome;
  exitReason: BacktestExitReason | null;
  intrabarResolution: IntrabarResolution;
  wasLiquidated: boolean;
  maeR: number | null;
  mfeR: number | null;
  /**
   * ADX at the bar whose close produced the entry signal — the market's trend strength when
   * the decision was made. Null on runs queued before the engine measured it, and on a
   * position opened before the reading was warm.
   */
  cycleAdx: number | null;
  /** The interval `cycleAdx` was read on. Never read one without the other. */
  cycleInterval: CandleInterval | null;
  sourceTradeId: Guid | null;
}

export interface BacktestExecutionResponse {
  id: Guid;
  backtestTradeId: Guid;
  role: ExecutionRole;
  price: number;
  quantity: number;
  fee: number;
  notional: number;
  executedAt: Instant;
  /** Index of the bar the fill landed on, counted from the run's first bar. */
  barIndex: number;
}

/**
 * The list row plus what a ledger row has no width for: the bars the position
 * occupied, the engine's own note, and the fills. Only the by-id fetch returns
 * it — the run's trades list carries `BacktestTradeResponse`.
 */
export interface BacktestTradeDetailResponse extends BacktestTradeResponse {
  backtestRunId: Guid;
  entryBarIndex: number;
  exitBarIndex: number;
  /** True when the bar held both the stop and the target and the worse one was assumed. */
  exitWasAssumed: boolean;
  notes: string | null;
  executions: BacktestExecutionResponse[];
}
