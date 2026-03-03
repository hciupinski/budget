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
