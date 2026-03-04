import type { ReactNode } from "react";
import { asCurrency, asSignedCurrency } from "@/components/budget/budget-ui-utils";

export function MonthlyWorkspaceTablePanel({ children }: { children: ReactNode }) {
  return <>{children}</>;
}

export function MonthlyWorkspaceSummaryPanel({ children }: { children: ReactNode }) {
  return <>{children}</>;
}

export function MonthlyMetricCard({
  label,
  planned,
  actual,
  valueTone,
  danger = false
}: {
  label: string;
  planned: number;
  actual: number;
  valueTone: string;
  danger?: boolean;
}) {
  return (
    <div className={`ui-surface rounded-[20px] border p-4 ${danger ? "border-[#f43f5e]" : "ui-border"}`}>
      <p className="ui-text-muted text-sm tracking-wide md:text-base">{label}</p>
      <p className={`mt-1 text-2xl font-medium md:text-3xl ${valueTone}`}>{asCurrency(planned)}</p>
      <p className="ui-text-muted mt-1 text-sm md:text-base">
        Actual: <span className={valueTone}>{asCurrency(actual)}</span>
      </p>
    </div>
  );
}

export function MonthlySummaryColumn({
  label,
  income,
  business,
  personal,
  savings,
  remainder
}: {
  label: string;
  income: number;
  business: number;
  personal: number;
  savings: number;
  remainder: number;
}) {
  const transfer = income - business;

  return (
    <div className="space-y-4">
      <p className="ui-text-muted text-sm tracking-wide md:text-base">{label}</p>

      <MonthlySummaryLine label="Total Business Income" value={income} valueTone="text-[#10a34a]" />
      <MonthlySummaryLine label="- Business Expenses" value={-business} valueTone="text-[#8f30ff]" />

      <div className="ui-border border-t" />

      <MonthlySummaryLine label="Transfer to Personal" value={transfer} valueTone="text-[#10a34a]" />
      <MonthlySummaryLine label="- Personal Expenses" value={-personal} valueTone="text-[#f35b00]" />
      <MonthlySummaryLine label="- Savings" value={-savings} valueTone="text-[#2563eb]" />

      <div className="ui-border border-t" />

      <MonthlySummaryLine label="Remainder" value={remainder} valueTone={remainder >= 0 ? "text-[#10a34a]" : "text-[#e11d48]"} />
    </div>
  );
}

export function MonthlySummaryLine({
  label,
  value,
  valueTone
}: {
  label: string;
  value: number;
  valueTone: string;
}) {
  return (
    <div className="flex items-center justify-between gap-4 text-sm md:text-base">
      <p className="ui-text-muted">{label}</p>
      <p className={valueTone}>{asSignedCurrency(value)}</p>
    </div>
  );
}
