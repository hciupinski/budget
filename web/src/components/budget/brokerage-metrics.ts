import type { BudgetAccount, InvestmentHolding } from "@/lib/budget-types";

type BrokerageDirection = "up" | "down" | "flat";

export type BrokerageMetric = {
  displayValue: number;
  direction: BrokerageDirection | null;
  changePercent: number | null;
};

function toRoundedMoney(value: number): number {
  return Number(value.toFixed(2));
}

export function calculateBrokerageMetrics(
  accounts: BudgetAccount[],
  holdings: InvestmentHolding[]
): Map<string, BrokerageMetric> {
  const metrics = new Map<string, BrokerageMetric>();
  const holdingsByAccountId = new Map<string, InvestmentHolding[]>();

  for (const holding of holdings) {
    const grouped = holdingsByAccountId.get(holding.accountId) ?? [];
    grouped.push(holding);
    holdingsByAccountId.set(holding.accountId, grouped);
  }

  for (const account of accounts) {
    if (account.kind !== "BROKERAGE") {
      continue;
    }

    const accountHoldings = holdingsByAccountId.get(account.id) ?? [];
    if (accountHoldings.length === 0) {
      metrics.set(account.id, {
        displayValue: account.currentBalance,
        direction: null,
        changePercent: null
      });
      continue;
    }

    let currentValue = 0;
    let previousValue = 0;
    let missingBaseline = false;

    for (const holding of accountHoldings) {
      currentValue += holding.currentValue;

      if (holding.manualPriceOverride != null) {
        previousValue += holding.currentValue;
        continue;
      }

      if (typeof holding.previousClosePrice === "number") {
        previousValue += toRoundedMoney(holding.units * holding.previousClosePrice);
        continue;
      }

      missingBaseline = true;
    }

    const roundedCurrent = toRoundedMoney(currentValue);
    const roundedPrevious = toRoundedMoney(previousValue);

    if (missingBaseline) {
      metrics.set(account.id, {
        displayValue: roundedCurrent,
        direction: null,
        changePercent: null
      });
      continue;
    }

    if (roundedPrevious <= 0) {
      metrics.set(account.id, {
        displayValue: roundedCurrent,
        direction: "flat",
        changePercent: 0
      });
      continue;
    }

    const deltaPercent = Number((((roundedCurrent - roundedPrevious) / roundedPrevious) * 100).toFixed(2));
    const direction: BrokerageDirection = deltaPercent > 0 ? "up" : deltaPercent < 0 ? "down" : "flat";

    metrics.set(account.id, {
      displayValue: roundedCurrent,
      direction,
      changePercent: deltaPercent
    });
  }

  return metrics;
}
