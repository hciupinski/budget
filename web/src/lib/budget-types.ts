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
