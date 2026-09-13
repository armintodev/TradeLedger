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
