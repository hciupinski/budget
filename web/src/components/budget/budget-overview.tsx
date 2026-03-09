"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import { type AnnualPlanResponse, type AssetsInvestmentsResponse, type AssetsOverviewResponse, type MonthlyWorkspaceResponse } from "@/lib/budget-types";
import {
  ArrowDownLeftIcon,
  ArrowRightIcon,
  ArrowUpRightIcon,
  BusinessIcon,
  HomeIcon,
  InvestmentIcon,
  SavingsIcon
} from "@/components/budget/icons";
import {
  asCurrency,
  monthLongLabel
} from "@/components/budget/budget-ui-utils";
import { resolveManagedSection, useSectionSettings } from "@/lib/section-settings";
import { formatCurrency, useCurrencySetting } from "@/lib/currency-settings";
import {
  annualCustomOneTimeRowKey,
  PLANNER_CUSTOM_EVENT,
  refreshPlannerCustomizationFromApi,
  readAnnualCustomItems,
  readHiddenMonthlyApiRows,
  readMonthlyCustomItems,
  readNameOverrides,
  readOneTimeAnnualRowKeys,
  type ActionStatus,
  type AnnualCustomItem,
  type MonthlyCustomItem
} from "@/lib/planner-custom-items";
import { getAnnualPlan } from "@/lib/api-clients/annual-api";
import { getInvestments } from "@/lib/api-clients/accounts-api";
import { getAssetsOverview } from "@/lib/api-clients/overview-api";
import { getMonthlyWorkspace } from "@/lib/api-clients/monthly-api";
import { redirectToLoginIfUnauthorized } from "@/lib/http/auth-redirect";
import { toUserFeedback, type UserFeedback } from "@/lib/http/user-feedback";
import { resolveCustomSection } from "@/components/budget/planner-row-utils";
import { calculateBrokerageMetrics } from "@/components/budget/brokerage-metrics";

function isSavingsOrInvestments(kind: string): boolean {
  return kind === "SAVINGS" || kind === "INVESTMENTS";
}

function resolveActualAmount(status: string, actualAmount: number | null, plannedAmount: number): number {
  if (status === "DONE") {
    return actualAmount ?? plannedAmount;
  }

  if (status === "PARTIAL") {
    return actualAmount ?? 0;
  }

  if (status === "SKIPPED") {
    return 0;
  }

  return 0;
}

export function BudgetOverview() {
  const now = new Date();
  useCurrencySetting();
  const year = now.getFullYear();
  const month = now.getMonth() + 1;
  const sectionSettings = useSectionSettings();
  const [annualPlan, setAnnualPlan] = useState<AnnualPlanResponse | null>(null);
  const [assetsOverview, setAssetsOverview] = useState<AssetsOverviewResponse | null>(null);
  const [investmentsOverview, setInvestmentsOverview] = useState<AssetsInvestmentsResponse | null>(null);
  const [monthlyWorkspaces, setMonthlyWorkspaces] = useState<MonthlyWorkspaceResponse[]>([]);
  const [annualCustomItems, setAnnualCustomItems] = useState<AnnualCustomItem[]>([]);
  const [monthlyCustomItems, setMonthlyCustomItems] = useState<MonthlyCustomItem[]>([]);
  const [nameOverrides, setNameOverrides] = useState<Record<string, string>>({});
  const [hiddenMonthlyApiRows, setHiddenMonthlyApiRows] = useState<string[]>([]);
  const [oneTimeAnnualRows, setOneTimeAnnualRows] = useState<string[]>([]);
  const [message, setMessage] = useState<UserFeedback | null>(null);
  const [investmentsMessage, setInvestmentsMessage] = useState<UserFeedback | null>(null);
  const [savingsProgressMessage, setSavingsProgressMessage] = useState<UserFeedback | null>(null);
  const [loadingSavingsProgress, setLoadingSavingsProgress] = useState<boolean>(false);

  const loadAnnualPlan = useCallback(async (selectedYear: number) => {
    try {
      const payload = await getAnnualPlan(selectedYear);
      setAnnualPlan(payload);
      setMessage(null);
    } catch (error) {
      if (redirectToLoginIfUnauthorized(error)) {
        return;
      }

      setAnnualPlan(null);
      setMessage(toUserFeedback(error, "Unable to load annual overview."));
    }
  }, []);

  const loadAssetsOverview = useCallback(async (selectedYear: number, selectedMonth: number) => {
    try {
      const payload = await getAssetsOverview(selectedYear, selectedMonth);
      setAssetsOverview(payload);
      setMessage(null);
    } catch (error) {
      if (redirectToLoginIfUnauthorized(error)) {
        return;
      }

      setAssetsOverview(null);
      setMessage(toUserFeedback(error, "Unable to load assets overview."));
    }
  }, []);

  const loadInvestmentsOverview = useCallback(async () => {
    try {
      const payload = await getInvestments("auto");
      setInvestmentsOverview(payload);
      setInvestmentsMessage(null);
    } catch (error) {
      if (redirectToLoginIfUnauthorized(error)) {
        return;
      }

      setInvestmentsOverview(null);
      setInvestmentsMessage(toUserFeedback(error, "Unable to load brokerage day-over-day changes."));
    }
  }, []);

  const loadMonthlySavingsProgress = useCallback(async (selectedYear: number, selectedMonth: number) => {
    setLoadingSavingsProgress(true);
    try {
      const monthsToLoad = Array.from({ length: selectedMonth }, (_, index) => index + 1);
      const payload = await Promise.all(monthsToLoad.map((value) => getMonthlyWorkspace(selectedYear, value)));
      setMonthlyWorkspaces(payload);
      setSavingsProgressMessage(null);
    } catch (error) {
      if (redirectToLoginIfUnauthorized(error)) {
        return;
      }

      setMonthlyWorkspaces([]);
      setSavingsProgressMessage(toUserFeedback(error, "Unable to load savings progress."));
    } finally {
      setLoadingSavingsProgress(false);
    }
  }, []);

  useEffect(() => {
    void loadAnnualPlan(year);
    void loadAssetsOverview(year, month);
    void loadInvestmentsOverview();
    void loadMonthlySavingsProgress(year, month);
  }, [loadAnnualPlan, loadAssetsOverview, loadInvestmentsOverview, loadMonthlySavingsProgress, month, year]);

  useEffect(() => {
    function syncCustomItems() {
      setAnnualCustomItems(readAnnualCustomItems());
      setMonthlyCustomItems(readMonthlyCustomItems());
      setNameOverrides(readNameOverrides());
      setHiddenMonthlyApiRows(readHiddenMonthlyApiRows());
      setOneTimeAnnualRows(readOneTimeAnnualRowKeys());
    }

    syncCustomItems();
    void refreshPlannerCustomizationFromApi();
    window.addEventListener(PLANNER_CUSTOM_EVENT, syncCustomItems);
    window.addEventListener("storage", syncCustomItems);

    return () => {
      window.removeEventListener(PLANNER_CUSTOM_EVENT, syncCustomItems);
      window.removeEventListener("storage", syncCustomItems);
    };
  }, []);

  const monthMetrics = useMemo(() => {
    if (!annualPlan) {
      return {
        businessIncome: 8500,
        businessExpenses: 1235,
        transferToPersonal: 7265,
        personalExpenses: 3350,
        savings: 800,
        investments: 1500
      };
    }

    const rows = annualPlan.categories;
    const monthIndex = month - 1;

    const businessIncome = rows
      .filter((row) => resolveManagedSection(row.section, row.categoryName, sectionSettings).kind === "INCOME")
      .reduce((sum, row) => sum + (row.months[monthIndex] ?? 0), 0);

    const businessExpenses = rows
      .filter((row) =>
        resolveManagedSection(row.section, row.categoryName, sectionSettings).kind === "BUSINESS_EXPENSES"
      )
      .reduce((sum, row) => sum + (row.months[monthIndex] ?? 0), 0);

    const personalExpenses = rows
      .filter((row) =>
        resolveManagedSection(row.section, row.categoryName, sectionSettings).kind === "PERSONAL_EXPENSES"
      )
      .reduce((sum, row) => sum + (row.months[monthIndex] ?? 0), 0);

    const savings = rows
      .filter((row) => resolveManagedSection(row.section, row.categoryName, sectionSettings).kind === "SAVINGS")
      .reduce((sum, row) => sum + (row.months[monthIndex] ?? 0), 0);

    const investments = rows
      .filter((row) => resolveManagedSection(row.section, row.categoryName, sectionSettings).kind === "INVESTMENTS")
      .reduce((sum, row) => sum + (row.months[monthIndex] ?? 0), 0);

    const customRows = annualCustomItems.filter((item) => item.year === year);

    let customIncome = 0;
    let customBusinessExpenses = 0;
    let customPersonalExpenses = 0;
    let customSavings = 0;
    let customInvestments = 0;

    for (const customRow of customRows) {
      const resolved = resolveManagedSection(
        customRow.sectionKind === "INCOME"
          ? "INCOME"
          : customRow.sectionKind === "BUSINESS_EXPENSES" || customRow.sectionKind === "PERSONAL_EXPENSES"
            ? "COSTS"
            : "SAVINGS_INVESTMENTS",
        customRow.name,
        sectionSettings
      );
      const monthValue = customRow.months[monthIndex] ?? 0;

      if (resolved.kind === "INCOME") {
        customIncome += monthValue;
      }

      if (resolved.kind === "BUSINESS_EXPENSES") {
        customBusinessExpenses += monthValue;
      }

      if (resolved.kind === "PERSONAL_EXPENSES") {
        customPersonalExpenses += monthValue;
      }

      if (resolved.kind === "SAVINGS") {
        customSavings += monthValue;
      }

      if (resolved.kind === "INVESTMENTS") {
        customInvestments += monthValue;
      }
    }

    return {
      businessIncome: businessIncome + customIncome,
      businessExpenses: businessExpenses + customBusinessExpenses,
      transferToPersonal: (businessIncome + customIncome) - (businessExpenses + customBusinessExpenses),
      personalExpenses: personalExpenses + customPersonalExpenses,
      savings: savings + customSavings,
      investments: investments + customInvestments
    };
  }, [annualCustomItems, annualPlan, month, sectionSettings, year]);

  const brokerageMetricsByAccountId = useMemo(
    () => calculateBrokerageMetrics(assetsOverview?.accounts ?? [], investmentsOverview?.holdings ?? []),
    [assetsOverview?.accounts, investmentsOverview?.holdings]
  );

  const savingsProgress = useMemo(() => {
    const plannedByMonth = Array.from({ length: 12 }, () => 0);

    for (const row of annualPlan?.categories ?? []) {
      const kind = resolveManagedSection(row.section, row.categoryName, sectionSettings).kind;
      if (!isSavingsOrInvestments(kind)) {
        continue;
      }

      for (let index = 0; index < 12; index += 1) {
        plannedByMonth[index] += row.months[index] ?? 0;
      }
    }

    for (const customRow of annualCustomItems.filter((item) => item.year === year)) {
      const customSection = resolveManagedSection(
        customRow.sectionKind === "INCOME"
          ? "INCOME"
          : customRow.sectionKind === "BUSINESS_EXPENSES" || customRow.sectionKind === "PERSONAL_EXPENSES"
            ? "COSTS"
            : "SAVINGS_INVESTMENTS",
        customRow.name,
        sectionSettings
      );

      if (!isSavingsOrInvestments(customSection.kind)) {
        continue;
      }

      for (let index = 0; index < 12; index += 1) {
        plannedByMonth[index] += customRow.months[index] ?? 0;
      }
    }

    const actualByMonth = Array.from({ length: 12 }, () => 0);
    const annualCustomForYear = annualCustomItems.filter((item) => item.year === year);
    const annualCustomIds = new Set(annualCustomForYear.map((item) => item.id));

    for (const workspace of monthlyWorkspaces) {
      const monthNumber = workspace.month;
      const monthIndex = monthNumber - 1;
      if (monthIndex < 0 || monthIndex > 11) {
        continue;
      }

      let monthActual = 0;

      for (const action of workspace.actions) {
        if (hiddenMonthlyApiRows.includes(action.actionId)) {
          continue;
        }

        const displayName = nameOverrides[action.categoryId] ?? action.categoryName;
        const kind = resolveManagedSection(action.section, displayName, sectionSettings).kind;
        if (!isSavingsOrInvestments(kind)) {
          continue;
        }

        monthActual += resolveActualAmount(action.status, action.actualAmount, action.plannedAmount);
      }

      const monthlyCustomForPeriod = monthlyCustomItems.filter(
        (item) => item.year === year && item.month === monthNumber
      );

      const linkedAnnualCustomMap = new Map<string, MonthlyCustomItem>();
      for (const customItem of monthlyCustomForPeriod) {
        const resolvedCustomSection = resolveCustomSection(
          {
            sectionId: customItem.sectionId,
            sectionKind: customItem.sectionKind
          },
          sectionSettings
        );
        if (!isSavingsOrInvestments(resolvedCustomSection.kind)) {
          continue;
        }

        if (customItem.annualCustomItemId && annualCustomIds.has(customItem.annualCustomItemId)) {
          linkedAnnualCustomMap.set(customItem.annualCustomItemId, customItem);
          continue;
        }

        monthActual += resolveActualAmount(customItem.status, customItem.actualAmount, customItem.plannedAmount);
      }

      for (const annualCustom of annualCustomForYear) {
        const resolvedAnnualSection = resolveCustomSection(
          {
            sectionId: annualCustom.sectionId,
            sectionKind: annualCustom.sectionKind
          },
          sectionSettings
        );
        if (!isSavingsOrInvestments(resolvedAnnualSection.kind)) {
          continue;
        }

        const plannedFromAnnual = annualCustom.months[monthIndex] ?? 0;
        const oneTimeAnnualKey = annualCustomOneTimeRowKey(year, annualCustom.id);
        if (oneTimeAnnualRows.includes(oneTimeAnnualKey) && plannedFromAnnual === 0) {
          continue;
        }

        const existingMonthly = linkedAnnualCustomMap.get(annualCustom.id);
        const status: ActionStatus = existingMonthly?.status ?? "PLANNED";
        const plannedAmount = existingMonthly?.plannedAmount ?? plannedFromAnnual;
        const actualAmount = existingMonthly?.actualAmount ?? null;

        monthActual += resolveActualAmount(status, actualAmount, plannedAmount);
      }

      actualByMonth[monthIndex] += monthActual;
    }

    const plannedCumulative = Array.from({ length: 12 }, () => 0);
    const actualCumulative: Array<number | null> = Array.from({ length: 12 }, () => null);
    let plannedRunningTotal = 0;
    let actualRunningTotal = 0;

    for (let index = 0; index < 12; index += 1) {
      plannedRunningTotal += plannedByMonth[index];
      plannedCumulative[index] = plannedRunningTotal;

      if (index + 1 <= month) {
        actualRunningTotal += actualByMonth[index];
        actualCumulative[index] = actualRunningTotal;
      }
    }

    return {
      plannedCumulative,
      actualCumulative,
      plannedYearToDate: plannedCumulative[month - 1] ?? 0,
      actualYearToDate: actualCumulative[month - 1] ?? 0
    };
  }, [
    annualCustomItems,
    annualPlan,
    hiddenMonthlyApiRows,
    month,
    monthlyCustomItems,
    monthlyWorkspaces,
    nameOverrides,
    oneTimeAnnualRows,
    sectionSettings,
    year
  ]);

  return (
    <div className="space-y-8">
      <header>
        <h1 className="ui-text-strong text-2xl md:text-3xl font-semibold tracking-[-0.02em]">Budget Dashboard</h1>
        <p className="ui-text-muted text-base">
          {monthLongLabel(month)} {year} - Current Month Overview
        </p>
      </header>
      {message ? (
        <p className="ui-text-muted text-sm">
          {message.message}
          {message.details ? ` ${message.details}` : ""}
        </p>
      ) : null}

      <section className="ui-border ui-surface rounded-[22px] border p-5 md:p-6">
        <h2 className="ui-text-strong text-xl md:text-2xl font-medium">Monthly Money Flow</h2>
        <p className="ui-text-muted mt-2 text-sm md:text-base">Track how your business income flows through to personal finances</p>

        <div className="mt-8 grid gap-3 xl:grid-cols-[1fr_auto_1fr_auto_1fr] xl:items-center">
          <FlowCard
            tone="blue"
            label="Business Income"
            value={monthMetrics.businessIncome}
            icon={<BusinessIcon size={22} />}
          />
          <ArrowRightIcon size={30} className="ui-text-muted mx-auto hidden xl:block" />
          <FlowCard
            tone="purple"
            label="Business Expenses"
            value={monthMetrics.businessExpenses}
            icon={<span className="text-sm font-semibold">$</span>}
          />
          <ArrowRightIcon size={30} className="ui-text-muted mx-auto hidden xl:block" />
          <FlowCard
            tone="green"
            label="To Personal"
            value={monthMetrics.transferToPersonal}
            icon={<HomeIcon size={22} />}
          />
        </div>

        <div className="ui-border my-8 border-t" />

        <h3 className="ui-text-strong text-lg md:text-xl font-medium">Personal Distribution</h3>
        <div className="mt-4 grid gap-3 md:grid-cols-3">
          <DistributionCard
            tone="orange"
            label="Expenses"
            value={monthMetrics.personalExpenses}
            icon={<HomeIcon size={18} />}
          />
          <DistributionCard tone="teal" label="Savings" value={monthMetrics.savings} icon={<SavingsIcon size={18} />} />
          <DistributionCard
            tone="indigo"
            label="Investments"
            value={monthMetrics.investments}
            icon={<InvestmentIcon size={18} />}
          />
        </div>
      </section>

      <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        {(assetsOverview?.accounts ?? []).filter((account) => !account.isArchived).map((account) => {
          const brokerageMetric = account.kind === "BROKERAGE" ? brokerageMetricsByAccountId.get(account.id) : null;
          const displayedValue = brokerageMetric?.displayValue ?? account.currentBalance;

          return (
            <div key={account.id} className="ui-border ui-surface rounded-[20px] border p-4">
              <p className="ui-text-muted text-sm md:text-base">{account.name}</p>
              <p className="ui-text-strong mt-1 text-xl md:text-2xl font-medium">
                {formatCurrency(displayedValue, account.currency)}
              </p>

              {account.kind === "BROKERAGE" ? (
                <div className="mt-2 flex items-center justify-between">
                  <span className="ui-text-muted text-xs">vs previous trading day</span>
                  {!brokerageMetric || brokerageMetric.changePercent === null || brokerageMetric.direction === null ? (
                    <span className="ui-text-muted text-sm">—</span>
                  ) : (
                    <span
                      className={`inline-flex items-center gap-1 text-sm font-medium ${
                        brokerageMetric.direction === "up"
                          ? "text-[#10a34a]"
                          : brokerageMetric.direction === "down"
                            ? "text-[#e11d48]"
                            : "ui-text"
                      }`}
                    >
                      {brokerageMetric.direction === "up" ? <ArrowUpRightIcon size={14} /> : null}
                      {brokerageMetric.direction === "down" ? <ArrowDownLeftIcon size={14} /> : null}
                      {brokerageMetric.direction === "up"
                        ? "+"
                        : brokerageMetric.direction === "down"
                          ? "-"
                          : ""}
                      {Math.abs(brokerageMetric.changePercent).toFixed(2)}%
                    </span>
                  )}
                </div>
              ) : null}
            </div>
          );
        })}
      </section>
      {investmentsMessage ? (
        <p className="ui-text-muted text-sm">
          {investmentsMessage.message}
          {investmentsMessage.details ? ` ${investmentsMessage.details}` : ""}
        </p>
      ) : null}

      <section className="ui-border ui-surface rounded-[22px] border p-5 md:p-6">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div>
            <h2 className="ui-text-strong text-xl md:text-2xl font-medium">Savings + Investments Progress ({year})</h2>
            <p className="ui-text-muted mt-2 text-sm md:text-base">
              Gray line: planned cumulative savings + investments. Blue line: actual cumulative progress until {monthLongLabel(month)}.
            </p>
          </div>
          <div className="grid gap-1 text-sm">
            <p className="ui-text-muted">
              Planned YTD: <span className="ui-text-strong">{asCurrency(savingsProgress.plannedYearToDate)}</span>
            </p>
            <p className="ui-text-muted">
              Actual YTD: <span className="ui-text-strong">{asCurrency(savingsProgress.actualYearToDate)}</span>
            </p>
          </div>
        </div>

        <div className="mt-5">
          <SavingsProgressChart
            planned={savingsProgress.plannedCumulative}
            actual={savingsProgress.actualCumulative}
          />
        </div>

        {loadingSavingsProgress ? <p className="ui-text-muted mt-3 text-sm">Loading savings and investments progress...</p> : null}
        {savingsProgressMessage ? (
          <p className="ui-text-muted mt-3 text-sm">
            {savingsProgressMessage.message}
            {savingsProgressMessage.details ? ` ${savingsProgressMessage.details}` : ""}
          </p>
        ) : null}
      </section>
    </div>
  );
}

function SavingsProgressChart({
  planned,
  actual
}: {
  planned: number[];
  actual: Array<number | null>;
}) {
  const width = 960;
  const height = 260;
  const paddingLeft = 10;
  const paddingRight = 10;
  const paddingTop = 10;
  const paddingBottom = 28;
  const plotWidth = width - paddingLeft - paddingRight;
  const plotHeight = height - paddingTop - paddingBottom;
  const maxValue = Math.max(1, ...planned, ...actual.filter((value): value is number => value !== null));

  const valueToY = (value: number) => paddingTop + (1 - value / maxValue) * plotHeight;
  const indexToX = (index: number) => paddingLeft + (index / 11) * plotWidth;

  const toPath = (values: Array<number | null>) => {
    let path = "";
    let hasStarted = false;

    values.forEach((value, index) => {
      if (value === null) {
        hasStarted = false;
        return;
      }

      const x = indexToX(index);
      const y = valueToY(value);
      if (!hasStarted) {
        path += `M ${x} ${y}`;
        hasStarted = true;
      } else {
        path += ` L ${x} ${y}`;
      }
    });

    return path;
  };

  const plannedPath = toPath(planned);
  const actualPath = toPath(actual);

  return (
    <div>
      <div className="mb-2 flex flex-wrap items-center gap-4 text-xs">
        <span className="ui-text-muted inline-flex items-center gap-2">
          <span className="inline-block h-[2px] w-8 bg-[#8b93a8]" />
          Planned
        </span>
        <span className="ui-text-muted inline-flex items-center gap-2">
          <span className="inline-block h-[2px] w-8 bg-[#2d7af5]" />
          Actual
        </span>
      </div>
      <svg viewBox={`0 0 ${width} ${height}`} className="h-[240px] w-full">
        {[0.25, 0.5, 0.75].map((ratio) => (
          <line
            key={ratio}
            x1={paddingLeft}
            x2={width - paddingRight}
            y1={paddingTop + plotHeight * ratio}
            y2={paddingTop + plotHeight * ratio}
            stroke="#d9dde6"
            strokeWidth="1"
          />
        ))}
        <line
          x1={paddingLeft}
          x2={width - paddingRight}
          y1={paddingTop + plotHeight}
          y2={paddingTop + plotHeight}
          stroke="#c5cad5"
          strokeWidth="1.2"
        />
        <path d={plannedPath} fill="none" stroke="#8b93a8" strokeWidth="2.5" strokeLinecap="round" />
        <path d={actualPath} fill="none" stroke="#2d7af5" strokeWidth="2.5" strokeLinecap="round" />
      </svg>
      <div className="ui-text-muted mt-1 grid grid-cols-12 text-center text-[11px] md:text-xs">
        {["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"].map((label) => (
          <span key={label}>{label}</span>
        ))}
      </div>
    </div>
  );
}

function FlowCard({
  tone,
  label,
  value,
  icon
}: {
  tone: "blue" | "purple" | "green";
  label: string;
  value: number;
  icon: ReactNode;
}) {
  const classes: Record<typeof tone, string> = {
    blue: "border-[#aad0f6] bg-[#e8f1fb]",
    purple: "border-[#d8bdf5] bg-[#f1ebf8]",
    green: "border-[#98e2bd] bg-[#e5f4eb]"
  };

  return (
    <div className={`rounded-[18px] border p-4 ${classes[tone]}`}>
      <div className="flex items-center gap-3">
        <div className="grid h-12 w-12 place-items-center rounded-2xl bg-[#2d7af5] text-white">{icon}</div>
        <div>
          <p className="ui-text-muted text-sm">{label}</p>
          <p className="ui-text-strong text-xl md:text-2xl font-medium">{asCurrency(value)}</p>
        </div>
      </div>
    </div>
  );
}

function DistributionCard({
  tone,
  label,
  value,
  icon
}: {
  tone: "orange" | "teal" | "indigo";
  label: string;
  value: number;
  icon: ReactNode;
}) {
  const classes: Record<typeof tone, string> = {
    orange: "border-[#f3c18c] bg-[#f5efe6]",
    teal: "border-[#82e8dc] bg-[#e2f0f0]",
    indigo: "border-[#b6c2f7] bg-[#e9edf8]"
  };

  const iconBg: Record<typeof tone, string> = {
    orange: "bg-[#f97316]",
    teal: "bg-[#0ea5a2]",
    indigo: "bg-[#4f46e5]"
  };

  return (
    <div className={`rounded-[18px] border p-4 ${classes[tone]}`}>
      <div className="ui-text-muted flex items-center gap-2">
        <span className={`grid h-6 w-6 place-items-center rounded-md text-white ${iconBg[tone]}`}>{icon}</span>
        <span className="text-sm md:text-base">{label}</span>
      </div>
      <p className="ui-text-strong mt-2 text-lg md:text-xl font-medium">{asCurrency(value)}</p>
    </div>
  );
}
