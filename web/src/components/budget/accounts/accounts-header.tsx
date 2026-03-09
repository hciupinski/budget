import Link from "next/link";
import type { AssetsAccountsOverviewResponse } from "@/lib/budget-types";
import { formatCurrency, type CurrencyCode } from "@/lib/currency-settings";
import { monthLongLabel } from "@/components/budget/budget-ui-utils";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import type { AccountsSubpage } from "@/components/budget/hooks/use-accounts-state";
import { AccountsStatCard } from "@/components/budget/accounts/shared";

const ACCOUNTS_SUBPAGES: ReadonlyArray<{ value: AccountsSubpage; label: string; href: string }> = [
  { value: "general", label: "General", href: "/accounts/general" },
  { value: "savings", label: "Savings", href: "/accounts/savings" },
  { value: "investments", label: "Investments", href: "/accounts/investments" }
];

function normalizeCurrency(value: string): CurrencyCode {
  if (value === "USD" || value === "EUR") {
    return value;
  }

  return "PLN";
}

function asCurrencyBy(value: number, currency: string): string {
  return formatCurrency(value, normalizeCurrency(currency));
}

export function AccountsHeader({
  year,
  month,
  loading,
  saving,
  currency,
  subpage,
  accountsOverview,
  message,
  onYearChange,
  onMonthChange,
  onRefresh
}: {
  year: number;
  month: number;
  loading: boolean;
  saving: boolean;
  currency: CurrencyCode;
  subpage: AccountsSubpage;
  accountsOverview: AssetsAccountsOverviewResponse | null;
  message: { message: string; details?: string } | null;
  onYearChange: (value: number) => void;
  onMonthChange: (value: number) => void;
  onRefresh: () => void;
}) {
  return (
    <>
      <header className="flex flex-col gap-3 xl:flex-row xl:items-center xl:justify-between">
        <div>
          <h1 className="text-2xl md:text-3xl font-semibold tracking-[-0.02em] text-[#0f1321]">Accounts, Savings, and Investments</h1>
          <p className="text-base text-[#71768b]">
            {monthLongLabel(month)} {year} - Epic 4 workspace
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <Input
            type="number"
            min={2000}
            max={2100}
            value={year}
            onChange={(event) => onYearChange(Number(event.target.value))}
            className="h-11 w-28"
          />
          <select
            value={month}
            onChange={(event) => onMonthChange(Number(event.target.value))}
            className="h-11 rounded-md border border-input bg-background px-3 text-sm"
          >
            {Array.from({ length: 12 }, (_, index) => index + 1).map((value) => (
              <option key={value} value={value}>
                {monthLongLabel(value)}
              </option>
            ))}
          </select>
          <Button type="button" onClick={onRefresh} disabled={loading || saving}>
            Refresh
          </Button>
        </div>
      </header>

      <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <AccountsStatCard
          label={`Net Worth (${accountsOverview?.summary.baseCurrency ?? currency})`}
          value={asCurrencyBy(accountsOverview?.summary.netWorth ?? 0, accountsOverview?.summary.baseCurrency ?? currency)}
        />
        <AccountsStatCard
          label={`Snapshot Planned (${accountsOverview?.summary.baseCurrency ?? currency})`}
          value={asCurrencyBy(accountsOverview?.summary.snapshotPlanned ?? 0, accountsOverview?.summary.baseCurrency ?? currency)}
        />
        <AccountsStatCard
          label={`Snapshot Actual (${accountsOverview?.summary.baseCurrency ?? currency})`}
          value={asCurrencyBy(accountsOverview?.summary.snapshotActual ?? 0, accountsOverview?.summary.baseCurrency ?? currency)}
        />
        <AccountsStatCard
          label="FX Pairs Used"
          value={Object.keys(accountsOverview?.summary.exchangeRates ?? {}).sort().join(", ") || "-"}
          tone="ui-text-muted"
        />
      </section>

      <nav className="ui-border ui-surface flex flex-wrap gap-2 rounded-[20px] border p-2">
        {ACCOUNTS_SUBPAGES.map((item) => (
          <Link
            key={item.value}
            href={item.href}
            className={`rounded-xl px-4 py-2 text-sm transition-colors ${
              subpage === item.value
                ? "bg-[#040426] text-white"
                : "ui-text ui-hover-soft"
            }`}
          >
            {item.label}
          </Link>
        ))}
      </nav>

      {message ? (
        <p className="rounded-lg border border-[#d5d9e0] bg-[#f6f7f9] px-4 py-2 text-sm text-[#1c2230]">
          {message.message}
          {message.details ? ` ${message.details}` : ""}
        </p>
      ) : null}
    </>
  );
}
