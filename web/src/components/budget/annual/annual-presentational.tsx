import type { ReactNode } from "react";
import { asCurrency, asSignedCurrency } from "@/components/budget/budget-ui-utils";

export function AnnualPlannerSummaryPanel({ children }: { children: ReactNode }) {
  return <>{children}</>;
}

export function AnnualPlannerTablePanel({ children }: { children: ReactNode }) {
  return <>{children}</>;
}

export function AnnualMetricCard({
  label,
  value,
  valueTone,
  danger = false
}: {
  label: string;
  value: number;
  valueTone: string;
  danger?: boolean;
}) {
  return (
    <div className={`ui-surface rounded-[20px] border p-4 ${danger ? "border-[#f43f5e]" : "ui-border"}`}>
      <p className="ui-text-muted text-sm tracking-wide md:text-base">{label}</p>
      <p className={`mt-1 text-2xl font-medium md:text-3xl ${valueTone}`}>{asCurrency(value)}</p>
    </div>
  );
}

export function AnnualSummaryLine({
  label,
  value,
  valueTone
}: {
  label: string;
  value: number;
  valueTone: string;
}) {
  return (
    <div className="flex items-center justify-between gap-4">
      <p className="ui-text-muted">{label}</p>
      <p className={valueTone}>{asSignedCurrency(value)}</p>
    </div>
  );
}
