import { describe, expect, it } from "vitest";
import type { BudgetAccount, InvestmentHolding } from "@/lib/budget-types";
import { calculateBrokerageMetrics } from "@/components/budget/brokerage-metrics";

const brokerageAccount: BudgetAccount = {
  id: "acc-1",
  name: "Brokerage A",
  kind: "BROKERAGE",
  currency: "USD",
  currentBalance: 1234,
  isArchived: false,
  updatedAt: "2026-03-04T00:00:00Z"
};

const bankAccount: BudgetAccount = {
  id: "acc-2",
  name: "Bank",
  kind: "BANK",
  currency: "PLN",
  currentBalance: 10,
  isArchived: false,
  updatedAt: "2026-03-04T00:00:00Z"
};

function holding(partial: Partial<InvestmentHolding> = {}): InvestmentHolding {
  return {
    id: "h-1",
    accountId: "acc-1",
    accountName: "Brokerage A",
    accountCurrency: "USD",
    symbol: "AAA",
    units: 10,
    averageCost: 100,
    manualPriceOverride: null,
    lastFetchedPrice: 110,
    effectivePrice: 110,
    previousClosePrice: 100,
    previousCloseAt: "2026-03-03T00:00:00Z",
    currentValue: 1100,
    costBasis: 1000,
    profitLoss: 100,
    lastPriceUpdatedAt: "2026-03-04T00:00:00Z",
    ...partial
  };
}

describe("calculateBrokerageMetrics", () => {
  it("calculates positive change for brokerage account with complete baseline", () => {
    const result = calculateBrokerageMetrics([brokerageAccount], [holding()]);
    const metric = result.get("acc-1");

    expect(metric).toBeDefined();
    expect(metric?.displayValue).toBe(1100);
    expect(metric?.direction).toBe("up");
    expect(metric?.changePercent).toBe(10);
  });

  it("returns neutral baseline when no holdings exist for brokerage", () => {
    const result = calculateBrokerageMetrics([brokerageAccount], []);
    const metric = result.get("acc-1");

    expect(metric).toBeDefined();
    expect(metric?.displayValue).toBe(1234);
    expect(metric?.direction).toBeNull();
    expect(metric?.changePercent).toBeNull();
  });

  it("marks baseline as missing when a non-manual holding has no previous close", () => {
    const result = calculateBrokerageMetrics([brokerageAccount], [holding({ previousClosePrice: null })]);
    const metric = result.get("acc-1");

    expect(metric).toBeDefined();
    expect(metric?.direction).toBeNull();
    expect(metric?.changePercent).toBeNull();
  });

  it("uses current value as previous baseline for manual override holdings", () => {
    const result = calculateBrokerageMetrics(
      [brokerageAccount],
      [holding({ manualPriceOverride: 111, previousClosePrice: null, currentValue: 1110, effectivePrice: 111 })]
    );
    const metric = result.get("acc-1");

    expect(metric).toBeDefined();
    expect(metric?.direction).toBe("flat");
    expect(metric?.changePercent).toBe(0);
  });

  it("ignores non-brokerage accounts", () => {
    const result = calculateBrokerageMetrics([bankAccount], [holding({ accountId: "acc-2" })]);
    expect(result.size).toBe(0);
  });
});
