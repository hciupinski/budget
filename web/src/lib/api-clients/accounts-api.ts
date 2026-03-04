import { requestJson, requestVoid } from "@/lib/http/api-client";
import type {
  AccountKind,
  AssetsAccountsOverviewResponse,
  AssetsInvestmentsResponse,
  InvestmentHolding
} from "@/lib/budget-types";
import type { CurrencyCode } from "@/lib/currency-settings";

export function getAccountsOverview(year: number, month: number): Promise<AssetsAccountsOverviewResponse> {
  return requestJson<AssetsAccountsOverviewResponse>(`/api/budget/assets/accounts-overview?year=${year}&month=${month}`, {
    method: "GET"
  });
}

export function getInvestments(refreshMode: "auto" | "force" = "auto"): Promise<AssetsInvestmentsResponse> {
  return requestJson<AssetsInvestmentsResponse>(`/api/budget/assets/investments?refreshMode=${refreshMode}`, {
    method: "GET"
  });
}

export type CreateAccountPayload = {
  name: string;
  kind: AccountKind;
  currency: CurrencyCode;
  initialBalance: number;
};

export function createAccount(payload: CreateAccountPayload): Promise<void> {
  return requestVoid("/api/budget/assets/accounts", {
    method: "POST",
    body: JSON.stringify(payload)
  });
}

export type UpdateAccountPayload = {
  name?: string;
  kind?: AccountKind;
  currency?: CurrencyCode;
  currentBalance?: number;
  isArchived?: boolean;
};

export function updateAccount(accountId: string, payload: UpdateAccountPayload): Promise<void> {
  return requestVoid(`/api/budget/assets/accounts/${accountId}`, {
    method: "PATCH",
    body: JSON.stringify(payload)
  });
}

export type CreateTransferPayload = {
  fromAccountId: string;
  toAccountId: string;
  amount: number;
  note: string;
  transferDate: string;
};

export function createTransfer(payload: CreateTransferPayload): Promise<void> {
  return requestVoid("/api/budget/assets/transfers", {
    method: "POST",
    body: JSON.stringify(payload)
  });
}

export function saveSnapshots(
  year: number,
  month: number,
  payload: {
    snapshots: Array<{
      accountId: string;
      plannedBalance: number;
      actualBalance: number | null;
    }>;
  }
): Promise<void> {
  return requestVoid(`/api/budget/assets/snapshots/${year}/${month}`, {
    method: "PUT",
    body: JSON.stringify(payload)
  });
}

export type CreateHoldingPayload = {
  accountId: string;
  symbol: string;
  units: number;
  averageCost: number;
  manualPriceOverride: number | null;
};

export function createHolding(payload: CreateHoldingPayload): Promise<void> {
  return requestVoid("/api/budget/assets/holdings", {
    method: "POST",
    body: JSON.stringify(payload)
  });
}

export function refreshInvestmentPrices(): Promise<void> {
  return requestVoid("/api/budget/assets/prices/refresh", {
    method: "POST"
  });
}

export function updateHoldingManualPrice(holdingId: string, payload: { manualPriceOverride: number | null; clearManualPriceOverride: boolean }): Promise<void> {
  return requestVoid(`/api/budget/assets/holdings/${holdingId}`, {
    method: "PATCH",
    body: JSON.stringify(payload)
  });
}

export function removeHolding(holdingId: string): Promise<void> {
  return requestVoid(`/api/budget/assets/holdings/${holdingId}`, {
    method: "DELETE"
  });
}

export type CreateSavingsGoalPayload = {
  name: string;
  accountId: string | null;
  targetAmount: number;
  currentAmount: number;
  monthlyContributionTarget: number;
  targetYear: number | null;
  targetMonth: number | null;
};

export function createSavingsGoal(payload: CreateSavingsGoalPayload): Promise<void> {
  return requestVoid("/api/budget/assets/savings-goals", {
    method: "POST",
    body: JSON.stringify(payload)
  });
}

export type UpdateSavingsGoalPayload = {
  name?: string;
  accountId?: string | null;
  clearAccountLink?: boolean;
  targetAmount?: number;
  currentAmount?: number;
  monthlyContributionTarget?: number;
  targetYear?: number | null;
  targetMonth?: number | null;
  clearTargetDate?: boolean;
  isArchived?: boolean;
};

export function updateSavingsGoal(goalId: string, payload: UpdateSavingsGoalPayload): Promise<void> {
  return requestVoid(`/api/budget/assets/savings-goals/${goalId}`, {
    method: "PATCH",
    body: JSON.stringify(payload)
  });
}

export type HoldingsGroup = {
  accountId: string;
  accountName: string;
  accountCurrency: string;
  symbols: Array<{
    key: string;
    symbol: string;
    accountId: string;
    accountName: string;
    accountCurrency: string;
    holdings: InvestmentHolding[];
    units: number;
    costBasis: number;
    currentValue: number;
    profitLoss: number;
    averageCost: number;
    effectivePrice: number;
  }>;
};
