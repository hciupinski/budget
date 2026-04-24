export const MONTH_LABELS = [
  "Jan",
  "Feb",
  "Mar",
  "Apr",
  "May",
  "Jun",
  "Jul",
  "Aug",
  "Sep",
  "Oct",
  "Nov",
  "Dec"
] as const;

export const MONTHLY_STATUSES = ["ALL", "PLANNED", "DONE", "PARTIAL", "SKIPPED"] as const;

export type MonthlyStatus = (typeof MONTHLY_STATUSES)[number];

export type AnnualPlanResponse = {
  year: number;
  categories: Array<{
    categoryId: string;
    categoryName: string;
    section: "INCOME" | "COSTS" | "SAVINGS_INVESTMENTS";
    sortOrder: number;
    months: number[];
    total: number;
  }>;
  summary: {
    income: number;
    costs: number;
    savingsInvestments: number;
    remainder: number;
    grandTotal: number;
  };
};

export type MonthlyWorkspaceResponse = {
  year: number;
  month: number;
  statusFilter: MonthlyStatus;
  summary: {
    incomePlanned: number;
    incomeActual: number;
    costsPlanned: number;
    costsActual: number;
    savingsPlanned: number;
    savingsActual: number;
    remainderPlanned: number;
    remainderActual: number;
  };
  completion: {
    planned: number;
    done: number;
    partial: number;
    skipped: number;
    total: number;
  };
  actions: Array<{
    actionId: string;
    categoryId: string;
    categoryName: string;
    section: "INCOME" | "COSTS" | "SAVINGS_INVESTMENTS";
    plannedAmount: number;
    actualAmount: number | null;
    status: Exclude<MonthlyStatus, "ALL">;
    updatedAt: string;
  }>;
};

export type AuditEntryResponse = {
  id: string;
  changedAt: string;
  entityType: string;
  entityId: string;
  eventType: string;
  changedBy: string;
  payload: string;
};

export type AccountKind = "BANK" | "SAVINGS" | "BROKERAGE" | "CASH_BUCKET";

export type BudgetAccount = {
  id: string;
  name: string;
  kind: AccountKind;
  currency: "PLN" | "USD" | "EUR";
  currentBalance: number;
  isArchived: boolean;
  updatedAt: string;
};

export type AccountTransfer = {
  id: string;
  fromAccountId: string;
  fromAccountName: string;
  toAccountId: string;
  toAccountName: string;
  amount: number;
  note: string;
  transferDate: string;
};

export type AccountSnapshot = {
  accountId: string;
  accountName: string;
  accountKind: AccountKind;
  year: number;
  month: number;
  plannedBalance: number;
  actualBalance: number | null;
  updatedAt: string;
};

export type InvestmentHolding = {
  id: string;
  accountId: string;
  accountName: string;
  accountCurrency: "PLN" | "USD" | "EUR";
  symbol: string;
  units: number;
  averageCost: number;
  manualPriceOverride: number | null;
  lastFetchedPrice: number;
  effectivePrice: number;
  previousClosePrice: number | null;
  previousCloseAt: string | null;
  currentValue: number;
  costBasis: number;
  profitLoss: number;
  lastPriceUpdatedAt: string;
};

export type SavingsGoal = {
  id: string;
  name: string;
  accountId: string | null;
  accountName: string | null;
  targetAmount: number;
  currentAmount: number;
  monthlyContributionTarget: number;
  targetYear: number | null;
  targetMonth: number | null;
  progressPercent: number;
  updatedAt: string;
};

export type AssetsOverviewResponse = {
  year: number;
  month: number;
  summary: {
    baseCurrency: "PLN" | "USD" | "EUR";
    netWorth: number;
    snapshotPlanned: number;
    snapshotActual: number;
    exchangeRates: Record<string, number>;
  };
  accounts: BudgetAccount[];
  transfers: AccountTransfer[];
  snapshots: AccountSnapshot[];
  holdings: InvestmentHolding[];
  investments: {
    totalValue: number;
    totalCostBasis: number;
    totalProfitLoss: number;
    allocation: Array<{
      holdingId: string;
      symbol: string;
      currentValue: number;
      allocationPercent: number;
    }>;
  };
  savingsGoals: SavingsGoal[];
};

export type AssetsAccountsOverviewResponse = {
  year: number;
  month: number;
  summary: AssetsOverviewResponse["summary"];
  accounts: BudgetAccount[];
  transfers: AccountTransfer[];
  snapshots: AccountSnapshot[];
  savingsGoals: SavingsGoal[];
};

export type InvestmentPriceRefreshMeta = {
  refreshedAtUtc: string;
  cacheTtlMinutes: number;
  symbolsRequested: string[];
  symbolsRefreshed: string[];
  symbolsFromCache: string[];
  symbolsFallbackToStale: string[];
  hadProviderFailures: boolean;
};

export type AssetsInvestmentsResponse = {
  holdings: InvestmentHolding[];
  investments: AssetsOverviewResponse["investments"];
  priceRefreshMeta: InvestmentPriceRefreshMeta;
};

export type ProjectCompletionStatus = "ACTIVE" | "DONE";
export type ProjectCompletionSource = "AUTO" | "MANUAL";
export type ProjectAttachmentKind = "AGREEMENT" | "RECEIPT" | "DOCUMENT";

export type ProjectTotals = {
  planned: number;
  paid: number;
  manualAdjustment: number;
  actual: number;
  variance: number;
};

export type ProjectCompletionSummary = {
  activeMilestones: number;
  completedMilestones: number;
  activeSteps: number;
  completedSteps: number;
  openItems: number;
  doneItems: number;
};

export type ProjectAttachment = {
  id: string;
  itemId: string;
  paymentId: string | null;
  kind: ProjectAttachmentKind;
  mimeType: string;
  sizeBytes: number;
  originalName: string;
  createdAt: string;
  isRemoved: boolean;
  removedAt: string | null;
  downloadPath: string;
};

export type ProjectPayment = {
  id: string;
  amount: number;
  paymentDate: string;
  note: string;
  isArchived: boolean;
  updatedAt: string;
};

export type ProjectItem = {
  id: string;
  name: string;
  sortOrder: number;
  plannedAmount: number;
  paidAmount: number;
  manualAdjustment: number;
  actualAmount: number;
  variance: number;
  isDone: boolean;
  doneAt: string | null;
  payments: ProjectPayment[];
  attachments: ProjectAttachment[];
};

export type ProjectStep = {
  id: string;
  name: string;
  sortOrder: number;
  completionStatus: ProjectCompletionStatus;
  completionSource: ProjectCompletionSource;
  completedAt: string | null;
  totals: ProjectTotals;
  completion: ProjectCompletionSummary;
  items: ProjectItem[];
};

export type ProjectMilestone = {
  id: string;
  name: string;
  sortOrder: number;
  completionStatus: ProjectCompletionStatus;
  completionSource: ProjectCompletionSource;
  completedAt: string | null;
  totals: ProjectTotals;
  completion: ProjectCompletionSummary;
  steps: ProjectStep[];
};

export type ProjectSummary = {
  id: string;
  name: string;
  currency: "PLN" | "USD" | "EUR";
  isArchived: boolean;
  totals: ProjectTotals;
  completion: ProjectCompletionSummary;
  updatedAt: string;
};

export type ProjectsListResponse = {
  projects: ProjectSummary[];
};

export type ProjectDetailResponse = {
  id: string;
  name: string;
  description: string;
  currency: "PLN" | "USD" | "EUR";
  sortOrder: number;
  isArchived: boolean;
  archivedAt: string | null;
  createdAt: string;
  updatedAt: string;
  totals: ProjectTotals;
  completion: ProjectCompletionSummary;
  milestones: ProjectMilestone[];
};
