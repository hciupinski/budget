"use client";

import { Fragment, useEffect, useMemo, useState } from "react";
import { ChevronDownIcon, ChevronRightIcon, SaveIcon, TrashIcon } from "@/components/budget/icons";
import type {
  AccountKind,
  BudgetAccount,
  InvestmentHolding,
  SavingsGoal
} from "@/lib/budget-types";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { currencyLabel, formatCurrency, type CurrencyCode, useCurrencySetting } from "@/lib/currency-settings";
import {
  createAccount as createAccountRequest,
  createHolding as createHoldingRequest,
  createSavingsGoal as createSavingsGoalRequest,
  createTransfer as createTransferRequest,
  refreshInvestmentPrices,
  removeHolding as removeHoldingRequest,
  saveSnapshots as saveSnapshotsRequest,
  updateAccount,
  updateSavingsGoal as updateSavingsGoalRequest,
  updateHoldingManualPrice
} from "@/lib/api-clients/accounts-api";
import { useAccountsState, type AccountsSubpage } from "@/components/budget/hooks/use-accounts-state";
import { redirectToLoginIfUnauthorized } from "@/lib/http/auth-redirect";
import { toUserFeedback } from "@/lib/http/user-feedback";
import { AccountsHeader } from "@/components/budget/accounts/accounts-header";
import {
  AccountsGeneralPanel,
  InvestmentsPanel,
  SavingsGoalsPanel,
  SnapshotsPanel,
  TransfersPanel
} from "@/components/budget/accounts/panels";
import { AccountsIconActionButton, AccountsStatCard } from "@/components/budget/accounts/shared";

const ACCOUNT_KINDS: ReadonlyArray<{ value: AccountKind; label: string }> = [
  { value: "BANK", label: "Bank" },
  { value: "SAVINGS", label: "Savings" },
  { value: "BROKERAGE", label: "Brokerage" },
  { value: "CASH_BUCKET", label: "Cash Bucket" }
];

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString("en-US", {
    year: "numeric",
    month: "short",
    day: "numeric"
  });
}

function toNumeric(value: string): number {
  const normalized = value.replace(",", ".");
  const parsed = Number(normalized);
  return Number.isFinite(parsed) ? parsed : 0;
}

function normalizeCurrency(value: string): CurrencyCode {
  if (value === "USD" || value === "EUR") {
    return value;
  }

  return "PLN";
}

function asCurrencyBy(value: number, currency: string): string {
  return formatCurrency(value, normalizeCurrency(currency));
}

function asSignedCurrencyBy(value: number, currency: string): string {
  const abs = asCurrencyBy(Math.abs(value), currency);
  if (value > 0) {
    return `+${abs}`;
  }

  if (value < 0) {
    return `-${abs}`;
  }

  return abs;
}

type HoldingSymbolGroup = {
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
};

type HoldingAccountGroup = {
  accountId: string;
  accountName: string;
  accountCurrency: string;
  symbols: HoldingSymbolGroup[];
};

export function AccountsView({ subpage = "general" }: { subpage?: AccountsSubpage }) {
  const now = new Date();
  const currency = useCurrencySetting();
  const {
    year,
    setYear,
    month,
    setMonth,
    accountsOverview,
    investmentsData,
    loadingAccounts,
    loadingInvestments,
    message,
    setMessage,
    loadAccountsOverview,
    loadInvestments,
    refreshData
  } = useAccountsState(now.getFullYear(), now.getMonth() + 1, subpage);
  const [saving, setSaving] = useState<boolean>(false);

  const [newAccountName, setNewAccountName] = useState<string>("");
  const [newAccountKind, setNewAccountKind] = useState<AccountKind>("BANK");
  const [newAccountCurrency, setNewAccountCurrency] = useState<CurrencyCode>("PLN");
  const [newAccountBalance, setNewAccountBalance] = useState<string>("0");
  const [editingAccountId, setEditingAccountId] = useState<string | null>(null);
  const [accountEditDraft, setAccountEditDraft] = useState<{
    name: string;
    kind: AccountKind;
    currency: CurrencyCode;
    initialAmount: string;
  } | null>(null);

  const [transferFromId, setTransferFromId] = useState<string>("");
  const [transferToId, setTransferToId] = useState<string>("");
  const [transferAmount, setTransferAmount] = useState<string>("0");
  const [transferNote, setTransferNote] = useState<string>("");

  const [snapshotDrafts, setSnapshotDrafts] = useState<Record<string, { planned: string; actual: string }>>({});

  const [holdingAccountId, setHoldingAccountId] = useState<string>("");
  const [holdingSymbol, setHoldingSymbol] = useState<string>("");
  const [holdingUnits, setHoldingUnits] = useState<string>("0");
  const [holdingAverageCost, setHoldingAverageCost] = useState<string>("0");
  const [holdingManualPrice, setHoldingManualPrice] = useState<string>("");
  const [manualPriceDrafts, setManualPriceDrafts] = useState<Record<string, string>>({});
  const [expandedHoldingAccounts, setExpandedHoldingAccounts] = useState<Record<string, boolean>>({});
  const [expandedHoldingSymbols, setExpandedHoldingSymbols] = useState<Record<string, boolean>>({});

  const [goalName, setGoalName] = useState<string>("");
  const [goalAccountId, setGoalAccountId] = useState<string>("");
  const [goalTargetAmount, setGoalTargetAmount] = useState<string>("0");
  const [goalCurrentAmount, setGoalCurrentAmount] = useState<string>("0");
  const [goalMonthlyContribution, setGoalMonthlyContribution] = useState<string>("0");
  const [editingGoalId, setEditingGoalId] = useState<string | null>(null);
  const [goalEditDraft, setGoalEditDraft] = useState<{
    name: string;
    accountId: string;
    targetAmount: string;
    currentAmount: string;
    monthlyContributionTarget: string;
  } | null>(null);

  const loading = loadingAccounts || loadingInvestments;
  const activeAccounts = useMemo(
    () => (accountsOverview?.accounts ?? []).filter((item) => !item.isArchived),
    [accountsOverview?.accounts]
  );
  const brokerageAccounts = useMemo(
    () => activeAccounts.filter((item) => item.kind === "BROKERAGE"),
    [activeAccounts]
  );
  const accountCurrencyById = useMemo(() => {
    const map = new Map<string, CurrencyCode>();
    for (const account of accountsOverview?.accounts ?? []) {
      map.set(account.id, account.currency);
    }
    return map;
  }, [accountsOverview?.accounts]);

  useEffect(() => {
    void loadAccountsOverview(year, month);
  }, [loadAccountsOverview, month, year]);

  useEffect(() => {
    if (subpage === "investments") {
      void loadInvestments();
    }
  }, [loadInvestments, subpage]);

  useEffect(() => {
    setNewAccountCurrency(currency);
  }, [currency]);

  useEffect(() => {
    if (!accountsOverview) {
      return;
    }

    setSnapshotDrafts(() => {
      const next: Record<string, { planned: string; actual: string }> = {};
      for (const account of accountsOverview.accounts) {
        const snapshot = accountsOverview.snapshots.find((item) => item.accountId === account.id);
        next[account.id] = {
          planned: String(snapshot?.plannedBalance ?? account.currentBalance),
          actual: String(snapshot?.actualBalance ?? "")
        };
      }
      return next;
    });
  }, [accountsOverview]);

  useEffect(() => {
    if (!investmentsData) {
      return;
    }

    setManualPriceDrafts(() => {
      const next: Record<string, string> = {};
      for (const holding of investmentsData.holdings) {
        next[holding.id] = holding.manualPriceOverride === null ? "" : String(holding.manualPriceOverride);
      }
      return next;
    });
  }, [investmentsData]);

  useEffect(() => {
    if (activeAccounts.length >= 2 && !transferFromId && !transferToId) {
      setTransferFromId(activeAccounts[0].id);
      setTransferToId(activeAccounts[1].id);
    }
  }, [activeAccounts, transferFromId, transferToId]);

  useEffect(() => {
    if (brokerageAccounts.length > 0 && !holdingAccountId) {
      setHoldingAccountId(brokerageAccounts[0].id);
    }
  }, [brokerageAccounts, holdingAccountId]);

  function beginEditAccount(account: BudgetAccount) {
    setEditingAccountId(account.id);
    setAccountEditDraft({
      name: account.name,
      kind: account.kind,
      currency: account.currency,
      initialAmount: String(account.currentBalance)
    });
  }

  function cancelEditAccount() {
    setEditingAccountId(null);
    setAccountEditDraft(null);
  }

  async function saveEditedAccount(accountId: string) {
    if (!accountEditDraft || !accountEditDraft.name.trim()) {
      setMessage({ message: "Account name is required." });
      return;
    }

    setSaving(true);
    try {
      await updateAccount(accountId, {
        name: accountEditDraft.name.trim(),
        kind: accountEditDraft.kind,
        currency: accountEditDraft.currency,
        currentBalance: toNumeric(accountEditDraft.initialAmount)
      });

      cancelEditAccount();
      setMessage({ message: "Account updated." });
      await loadAccountsOverview(year, month);
    } catch (error) {
      if (redirectToLoginIfUnauthorized(error)) {
        return;
      }

      setMessage(toUserFeedback(error, "Unable to update account."));
    } finally {
      setSaving(false);
    }
  }

  async function removeAccount(accountId: string) {
    setSaving(true);
    try {
      await updateAccount(accountId, {
        isArchived: true
      });

      cancelEditAccount();
      setMessage({ message: "Account removed." });
      await loadAccountsOverview(year, month);
    } catch (error) {
      if (redirectToLoginIfUnauthorized(error)) {
        return;
      }

      setMessage(toUserFeedback(error, "Unable to remove account."));
    } finally {
      setSaving(false);
    }
  }

  async function createAccount() {
    if (!newAccountName.trim()) {
      setMessage({ message: "Account name is required." });
      return;
    }

    setSaving(true);
    try {
      await createAccountRequest({
        name: newAccountName.trim(),
        kind: newAccountKind,
        currency: newAccountCurrency,
        initialBalance: toNumeric(newAccountBalance)
      });

      setNewAccountName("");
      setNewAccountBalance("0");
      setMessage({ message: "Account created." });
      await loadAccountsOverview(year, month);
    } catch (error) {
      if (redirectToLoginIfUnauthorized(error)) {
        return;
      }

      setMessage(toUserFeedback(error, "Unable to create account."));
    } finally {
      setSaving(false);
    }
  }

  async function createTransfer() {
    if (!transferFromId || !transferToId) {
      setMessage({ message: "Select source and destination accounts." });
      return;
    }

    setSaving(true);
    try {
      await createTransferRequest({
        fromAccountId: transferFromId,
        toAccountId: transferToId,
        amount: toNumeric(transferAmount),
        note: transferNote.trim(),
        transferDate: new Date().toISOString()
      });

      setTransferAmount("0");
      setTransferNote("");
      setMessage({ message: "Transfer logged." });
      await loadAccountsOverview(year, month);
    } catch (error) {
      if (redirectToLoginIfUnauthorized(error)) {
        return;
      }

      setMessage(toUserFeedback(error, "Unable to log transfer."));
    } finally {
      setSaving(false);
    }
  }

  async function saveSnapshots() {
    if (!accountsOverview) {
      return;
    }

    const snapshots = activeAccounts.map((account) => {
      const draft = snapshotDrafts[account.id] ?? { planned: "0", actual: "" };
      const actual = draft.actual.trim() === "" ? null : toNumeric(draft.actual);

      return {
        accountId: account.id,
        plannedBalance: toNumeric(draft.planned),
        actualBalance: actual
      };
    });

    setSaving(true);
    try {
      await saveSnapshotsRequest(year, month, {
        snapshots
      });

      setMessage({ message: "Monthly snapshots saved." });
      await loadAccountsOverview(year, month);
    } catch (error) {
      if (redirectToLoginIfUnauthorized(error)) {
        return;
      }

      setMessage(toUserFeedback(error, "Unable to save snapshots."));
    } finally {
      setSaving(false);
    }
  }

  async function createHolding() {
    if (!holdingAccountId || !holdingSymbol.trim()) {
      setMessage({ message: "Select brokerage account and provide symbol." });
      return;
    }

    setSaving(true);
    try {
      await createHoldingRequest({
        accountId: holdingAccountId,
        symbol: holdingSymbol.trim(),
        units: toNumeric(holdingUnits),
        averageCost: toNumeric(holdingAverageCost),
        manualPriceOverride: holdingManualPrice.trim() === "" ? null : toNumeric(holdingManualPrice)
      });

      setHoldingSymbol("");
      setHoldingUnits("0");
      setHoldingAverageCost("0");
      setHoldingManualPrice("");
      setMessage({ message: "Holding added." });
      await loadInvestments();
    } catch (error) {
      if (redirectToLoginIfUnauthorized(error)) {
        return;
      }

      setMessage(toUserFeedback(error, "Unable to add holding."));
    } finally {
      setSaving(false);
    }
  }

  async function refreshPrices() {
    setSaving(true);
    try {
      await refreshInvestmentPrices();
      setMessage({ message: "Market prices refreshed." });
      await loadInvestments();
    } catch (error) {
      if (redirectToLoginIfUnauthorized(error)) {
        return;
      }

      setMessage(toUserFeedback(error, "Unable to refresh market prices."));
    } finally {
      setSaving(false);
    }
  }

  async function saveManualPrice(holding: InvestmentHolding) {
    const draft = manualPriceDrafts[holding.id] ?? "";
    setSaving(true);
    try {
      await updateHoldingManualPrice(holding.id, {
        manualPriceOverride: draft.trim() === "" ? null : toNumeric(draft),
        clearManualPriceOverride: draft.trim() === ""
      });

      setMessage({ message: `Manual price updated for ${holding.symbol}.` });
      await loadInvestments();
    } catch (error) {
      if (redirectToLoginIfUnauthorized(error)) {
        return;
      }

      setMessage(toUserFeedback(error, "Unable to update manual price."));
    } finally {
      setSaving(false);
    }
  }

  async function removeHolding(holdingId: string) {
    setSaving(true);
    try {
      await removeHoldingRequest(holdingId);
      setMessage({ message: "Holding removed." });
      await loadInvestments();
    } catch (error) {
      if (redirectToLoginIfUnauthorized(error)) {
        return;
      }

      setMessage(toUserFeedback(error, "Unable to remove holding."));
    } finally {
      setSaving(false);
    }
  }

  async function createSavingsGoal() {
    if (!goalName.trim()) {
      setMessage({ message: "Savings goal name is required." });
      return;
    }

    setSaving(true);
    try {
      await createSavingsGoalRequest({
        name: goalName.trim(),
        accountId: goalAccountId || null,
        targetAmount: toNumeric(goalTargetAmount),
        currentAmount: toNumeric(goalCurrentAmount),
        monthlyContributionTarget: toNumeric(goalMonthlyContribution),
        targetYear: null,
        targetMonth: null
      });

      setGoalName("");
      setGoalTargetAmount("0");
      setGoalCurrentAmount("0");
      setGoalMonthlyContribution("0");
      setGoalAccountId("");
      setMessage({ message: "Savings goal created." });
      await loadAccountsOverview(year, month);
    } catch (error) {
      if (redirectToLoginIfUnauthorized(error)) {
        return;
      }

      setMessage(toUserFeedback(error, "Unable to create savings goal."));
    } finally {
      setSaving(false);
    }
  }

  function beginEditGoal(goal: SavingsGoal) {
    setEditingGoalId(goal.id);
    setGoalEditDraft({
      name: goal.name,
      accountId: goal.accountId ?? "",
      targetAmount: String(goal.targetAmount),
      currentAmount: String(goal.currentAmount),
      monthlyContributionTarget: String(goal.monthlyContributionTarget)
    });
  }

  function cancelEditGoal() {
    setEditingGoalId(null);
    setGoalEditDraft(null);
  }

  async function saveEditedGoal(goalId: string) {
    if (!goalEditDraft || !goalEditDraft.name.trim()) {
      setMessage({ message: "Savings goal name is required." });
      return;
    }

    setSaving(true);
    try {
      await updateSavingsGoalRequest(goalId, {
        name: goalEditDraft.name.trim(),
        accountId: goalEditDraft.accountId || null,
        clearAccountLink: goalEditDraft.accountId === "",
        targetAmount: toNumeric(goalEditDraft.targetAmount),
        currentAmount: toNumeric(goalEditDraft.currentAmount),
        monthlyContributionTarget: toNumeric(goalEditDraft.monthlyContributionTarget)
      });

      cancelEditGoal();
      setMessage({ message: "Savings goal updated." });
      await loadAccountsOverview(year, month);
    } catch (error) {
      if (redirectToLoginIfUnauthorized(error)) {
        return;
      }

      setMessage(toUserFeedback(error, "Unable to update savings goal."));
    } finally {
      setSaving(false);
    }
  }

  async function markGoalDone(goal: SavingsGoal) {
    setSaving(true);
    try {
      await updateSavingsGoalRequest(goal.id, {
        currentAmount: goal.targetAmount,
        monthlyContributionTarget: 0,
        clearAccountLink: goal.accountId !== null
      });

      if (editingGoalId === goal.id) {
        cancelEditGoal();
      }

      setMessage({ message: `Savings goal "${goal.name}" marked as done.` });
      await loadAccountsOverview(year, month);
    } catch (error) {
      if (redirectToLoginIfUnauthorized(error)) {
        return;
      }

      setMessage(toUserFeedback(error, "Unable to mark savings goal as done."));
    } finally {
      setSaving(false);
    }
  }

  async function removeGoal(goal: SavingsGoal) {
    setSaving(true);
    try {
      await updateSavingsGoalRequest(goal.id, {
        isArchived: true
      });

      if (editingGoalId === goal.id) {
        cancelEditGoal();
      }

      setMessage({ message: `Savings goal "${goal.name}" removed.` });
      await loadAccountsOverview(year, month);
    } catch (error) {
      if (redirectToLoginIfUnauthorized(error)) {
        return;
      }

      setMessage(toUserFeedback(error, "Unable to remove savings goal."));
    } finally {
      setSaving(false);
    }
  }

  const investmentTotalsByCurrency = useMemo(() => {
    const byCurrency = new Map<string, { value: number; costBasis: number; profitLoss: number }>();

    for (const holding of investmentsData?.holdings ?? []) {
      const current = byCurrency.get(holding.accountCurrency) ?? { value: 0, costBasis: 0, profitLoss: 0 };
      current.value += holding.currentValue;
      current.costBasis += holding.costBasis;
      current.profitLoss += holding.profitLoss;
      byCurrency.set(holding.accountCurrency, current);
    }

    return Array.from(byCurrency.entries()).sort((a, b) => a[0].localeCompare(b[0]));
  }, [investmentsData?.holdings]);

  const groupedHoldings = useMemo<HoldingAccountGroup[]>(() => {
    const accountsMap = new Map<string, HoldingAccountGroup>();

    for (const holding of investmentsData?.holdings ?? []) {
      const accountGroup = accountsMap.get(holding.accountId) ?? {
        accountId: holding.accountId,
        accountName: holding.accountName,
        accountCurrency: holding.accountCurrency,
        symbols: []
      };

      let symbolGroup = accountGroup.symbols.find((item) => item.symbol === holding.symbol);
      if (!symbolGroup) {
        symbolGroup = {
          key: `${holding.accountId}::${holding.symbol}`,
          symbol: holding.symbol,
          accountId: holding.accountId,
          accountName: holding.accountName,
          accountCurrency: holding.accountCurrency,
          holdings: [],
          units: 0,
          costBasis: 0,
          currentValue: 0,
          profitLoss: 0,
          averageCost: 0,
          effectivePrice: 0
        };
        accountGroup.symbols.push(symbolGroup);
      }

      symbolGroup.holdings.push(holding);
      symbolGroup.units += holding.units;
      symbolGroup.costBasis += holding.costBasis;
      symbolGroup.currentValue += holding.currentValue;
      symbolGroup.profitLoss += holding.profitLoss;

      accountsMap.set(holding.accountId, accountGroup);
    }

    const grouped = Array.from(accountsMap.values())
      .map((accountGroup) => {
        const symbols = accountGroup.symbols
          .map((symbolGroup) => {
            const avgCost = symbolGroup.units > 0 ? symbolGroup.costBasis / symbolGroup.units : 0;
            const price = symbolGroup.units > 0 ? symbolGroup.currentValue / symbolGroup.units : 0;

            return {
              ...symbolGroup,
              units: Number(symbolGroup.units.toFixed(6)),
              costBasis: Number(symbolGroup.costBasis.toFixed(2)),
              currentValue: Number(symbolGroup.currentValue.toFixed(2)),
              profitLoss: Number(symbolGroup.profitLoss.toFixed(2)),
              averageCost: Number(avgCost.toFixed(4)),
              effectivePrice: Number(price.toFixed(4)),
              holdings: [...symbolGroup.holdings].sort((a, b) => a.id.localeCompare(b.id))
            };
          })
          .sort((a, b) => a.symbol.localeCompare(b.symbol));

        return {
          ...accountGroup,
          symbols
        };
      })
      .sort((a, b) => a.accountName.localeCompare(b.accountName));

    return grouped;
  }, [investmentsData?.holdings]);

  useEffect(() => {
    setExpandedHoldingAccounts((current) => {
      const next: Record<string, boolean> = {};
      for (const accountGroup of groupedHoldings) {
        next[accountGroup.accountId] = current[accountGroup.accountId] ?? true;
      }
      return next;
    });
  }, [groupedHoldings]);

  useEffect(() => {
    setExpandedHoldingSymbols((current) => {
      const next: Record<string, boolean> = {};
      for (const accountGroup of groupedHoldings) {
        for (const symbolGroup of accountGroup.symbols) {
          next[symbolGroup.key] = current[symbolGroup.key] ?? false;
        }
      }
      return next;
    });
  }, [groupedHoldings]);

  return (
    <div className="space-y-6">
      <AccountsHeader
        year={year}
        month={month}
        loading={loading}
        saving={saving}
        currency={currency}
        subpage={subpage}
        accountsOverview={accountsOverview}
        message={message}
        onYearChange={setYear}
        onMonthChange={setMonth}
        onRefresh={() => void refreshData()}
      />

      {subpage === "general" ? (
        <AccountsGeneralPanel>
      <section className="ui-border ui-surface rounded-[22px] border p-5 md:p-6">
        <h2 className="ui-text-strong text-xl md:text-2xl font-medium">Accounts</h2>
        <p className="ui-text-muted mt-2 text-sm md:text-base">Create and monitor bank, savings, brokerage, and cash buckets.</p>

        <div className="mt-5 grid gap-2 md:grid-cols-[minmax(180px,1fr)_170px_120px_150px_auto]">
          <Input
            value={newAccountName}
            onChange={(event) => setNewAccountName(event.target.value)}
            placeholder="Account name"
          />
          <select
            value={newAccountKind}
            onChange={(event) => setNewAccountKind(event.target.value as AccountKind)}
            className="h-10 rounded-md border border-input bg-background px-3 text-sm"
          >
            {ACCOUNT_KINDS.map((kind) => (
              <option key={kind.value} value={kind.value}>
                {kind.label}
              </option>
            ))}
          </select>
          <select
            value={newAccountCurrency}
            onChange={(event) => setNewAccountCurrency(normalizeCurrency(event.target.value))}
            className="ui-control h-10 rounded-md border px-3 text-sm"
          >
            <option value="PLN">PLN</option>
            <option value="USD">USD</option>
            <option value="EUR">EUR</option>
          </select>
          <Input
            type="number"
            value={newAccountBalance}
            onChange={(event) => setNewAccountBalance(event.target.value)}
            placeholder="Initial balance"
          />
          <Button type="button" onClick={() => void createAccount()} disabled={saving}>
            Add Account
          </Button>
        </div>

        <div className="mt-6 grid gap-3 md:grid-cols-2">
          {activeAccounts.map((account) => {
            const isEditing = editingAccountId === account.id && accountEditDraft !== null;

            return (
              <article
                key={account.id}
                role="button"
                tabIndex={0}
                onClick={() => {
                  if (!isEditing) {
                    beginEditAccount(account);
                  }
                }}
                onKeyDown={(event) => {
                  if (event.key === "Enter" || event.key === " ") {
                    event.preventDefault();
                    if (!isEditing) {
                      beginEditAccount(account);
                    }
                  }
                }}
                className="ui-border ui-surface-soft rounded-[16px] border p-4 transition-colors hover:opacity-95"
              >
                {!isEditing ? (
                  <>
                    <div className="flex items-center justify-between gap-2">
                      <h3 className="ui-text-strong text-base font-medium">{account.name}</h3>
                      <span className="ui-border ui-text-muted rounded-full border px-2 py-0.5 text-xs">{account.kind}</span>
                    </div>
                    <p className="ui-text-strong mt-2 text-xl font-semibold">{asCurrencyBy(account.currentBalance, account.currency)}</p>
                    <p className="ui-text-muted text-sm">{currencyLabel(account.currency)}</p>
                    <p className="ui-text-muted mt-2 text-xs">Click to edit</p>
                  </>
                ) : (
                  <div className="space-y-2">
                    <Input
                      value={accountEditDraft.name}
                      onChange={(event) =>
                        setAccountEditDraft((current) =>
                          current
                            ? {
                                ...current,
                                name: event.target.value
                              }
                            : current
                        )
                      }
                      placeholder="Account name"
                      onClick={(event) => event.stopPropagation()}
                    />
                    <div className="grid gap-2 md:grid-cols-[1fr_1fr_1fr]">
                      <select
                        value={accountEditDraft.kind}
                        onChange={(event) =>
                          setAccountEditDraft((current) =>
                            current
                              ? {
                                  ...current,
                                  kind: event.target.value as AccountKind
                                }
                              : current
                          )
                        }
                        onClick={(event) => event.stopPropagation()}
                        className="ui-control h-10 rounded-md border px-3 text-sm"
                        >
                          {ACCOUNT_KINDS.map((kind) => (
                            <option key={kind.value} value={kind.value}>
                              {kind.label}
                            </option>
                          ))}
                      </select>
                      <select
                        value={accountEditDraft.currency}
                        onChange={(event) =>
                          setAccountEditDraft((current) =>
                            current
                              ? {
                                  ...current,
                                  currency: normalizeCurrency(event.target.value)
                                }
                              : current
                          )
                        }
                        onClick={(event) => event.stopPropagation()}
                        className="ui-control h-10 rounded-md border px-3 text-sm"
                      >
                        <option value="PLN">PLN</option>
                        <option value="USD">USD</option>
                        <option value="EUR">EUR</option>
                      </select>
                      <Input
                        type="number"
                        value={accountEditDraft.initialAmount}
                        onChange={(event) =>
                          setAccountEditDraft((current) =>
                            current
                              ? {
                                  ...current,
                                  initialAmount: event.target.value
                                }
                              : current
                          )
                        }
                        onClick={(event) => event.stopPropagation()}
                        placeholder="Initial amount"
                      />
                    </div>
                    <div className="flex justify-end gap-2">
                      <Button
                        type="button"
                        size="sm"
                        variant="outline"
                        className="border-[#dc2626] text-[#dc2626] hover:bg-[#dc2626]/10"
                        onClick={(event) => {
                          event.stopPropagation();
                          void removeAccount(account.id);
                        }}
                        disabled={saving}
                      >
                        Remove
                      </Button>
                      <Button
                        type="button"
                        size="sm"
                        variant="outline"
                        onClick={(event) => {
                          event.stopPropagation();
                          cancelEditAccount();
                        }}
                        disabled={saving}
                      >
                        Cancel
                      </Button>
                      <Button
                        type="button"
                        size="sm"
                        onClick={(event) => {
                          event.stopPropagation();
                          void saveEditedAccount(account.id);
                        }}
                        disabled={saving}
                      >
                        Save
                      </Button>
                    </div>
                  </div>
                )}
              </article>
            );
          })}
        </div>
      </section>

      <TransfersPanel>
      <section className="rounded-[22px] border border-[#cfd3da] bg-[#f6f7f9] p-5 md:p-6">
        <h2 className="text-xl md:text-2xl font-medium text-[#171a24]">Transfer Log</h2>
        <p className="mt-2 text-sm md:text-base text-[#73788d]">Log internal transfers between accounts and keep balances synchronized.</p>

        <div className="mt-5 grid gap-2 md:grid-cols-[1fr_1fr_140px_1fr_auto]">
          <select
            value={transferFromId}
            onChange={(event) => setTransferFromId(event.target.value)}
            className="h-10 rounded-md border border-input bg-background px-3 text-sm"
          >
            <option value="">From account</option>
            {activeAccounts.map((account) => (
              <option key={account.id} value={account.id}>
                {account.name}
              </option>
            ))}
          </select>
          <select
            value={transferToId}
            onChange={(event) => setTransferToId(event.target.value)}
            className="h-10 rounded-md border border-input bg-background px-3 text-sm"
          >
            <option value="">To account</option>
            {activeAccounts.map((account) => (
              <option key={account.id} value={account.id}>
                {account.name}
              </option>
            ))}
          </select>
          <Input
            type="number"
            value={transferAmount}
            onChange={(event) => setTransferAmount(event.target.value)}
            placeholder="Amount"
          />
          <Input value={transferNote} onChange={(event) => setTransferNote(event.target.value)} placeholder="Note (optional)" />
          <Button type="button" onClick={() => void createTransfer()} disabled={saving}>
            Add Transfer
          </Button>
        </div>

        <div className="mt-6 overflow-x-auto">
          <table className="w-max min-w-full border-collapse">
            <thead>
              <tr>
                <th className="border-b border-[#d2d7df] px-3 py-2 text-left text-sm font-semibold text-[#1f2430]">Date</th>
                <th className="border-b border-[#d2d7df] px-3 py-2 text-left text-sm font-semibold text-[#1f2430]">From</th>
                <th className="border-b border-[#d2d7df] px-3 py-2 text-left text-sm font-semibold text-[#1f2430]">To</th>
                <th className="border-b border-[#d2d7df] px-3 py-2 text-right text-sm font-semibold text-[#1f2430]">Amount</th>
                <th className="border-b border-[#d2d7df] px-3 py-2 text-left text-sm font-semibold text-[#1f2430]">Note</th>
              </tr>
            </thead>
            <tbody>
              {(accountsOverview?.transfers ?? []).map((transfer) => (
                <tr key={transfer.id}>
                  {(() => {
                    const transferCurrency = accountCurrencyById.get(transfer.fromAccountId) ?? currency;
                    return (
                      <>
                  <td className="border-b border-[#e0e4ea] px-3 py-2 text-sm text-[#1f2430]">{formatDate(transfer.transferDate)}</td>
                  <td className="border-b border-[#e0e4ea] px-3 py-2 text-sm text-[#1f2430]">{transfer.fromAccountName}</td>
                  <td className="border-b border-[#e0e4ea] px-3 py-2 text-sm text-[#1f2430]">{transfer.toAccountName}</td>
                  <td className="border-b border-[#e0e4ea] px-3 py-2 text-right text-sm text-[#1f2430]">
                    {asCurrencyBy(transfer.amount, transferCurrency)}
                  </td>
                  <td className="border-b border-[#e0e4ea] px-3 py-2 text-sm text-[#1f2430]">{transfer.note || "-"}</td>
                      </>
                    );
                  })()}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
      </TransfersPanel>

      <SnapshotsPanel>
      <section className="rounded-[22px] border border-[#cfd3da] bg-[#f6f7f9] p-5 md:p-6">
        <div className="flex items-center justify-between gap-3">
          <div>
            <h2 className="text-xl md:text-2xl font-medium text-[#171a24]">Monthly Account Snapshots</h2>
            <p className="mt-2 text-sm md:text-base text-[#73788d]">Maintain planned vs actual account balances.</p>
          </div>
          <Button type="button" onClick={() => void saveSnapshots()} disabled={saving || !accountsOverview}>
            Save Snapshots
          </Button>
        </div>

        <div className="mt-6 overflow-x-auto">
          <table className="w-max min-w-full border-collapse">
            <thead>
              <tr>
                <th className="border-b border-[#d2d7df] px-3 py-2 text-left text-sm font-semibold text-[#1f2430]">Account</th>
                <th className="border-b border-[#d2d7df] px-3 py-2 text-left text-sm font-semibold text-[#1f2430]">Kind</th>
                <th className="border-b border-[#d2d7df] px-3 py-2 text-left text-sm font-semibold text-[#1f2430]">Currency</th>
                <th className="border-b border-[#d2d7df] px-3 py-2 text-right text-sm font-semibold text-[#1f2430]">Planned</th>
                <th className="border-b border-[#d2d7df] px-3 py-2 text-right text-sm font-semibold text-[#1f2430]">Actual</th>
              </tr>
            </thead>
            <tbody>
              {activeAccounts.map((account) => (
                <tr key={account.id}>
                  <td className="border-b border-[#e0e4ea] px-3 py-2 text-sm text-[#1f2430]">{account.name}</td>
                  <td className="border-b border-[#e0e4ea] px-3 py-2 text-sm text-[#1f2430]">{account.kind}</td>
                  <td className="border-b border-[#e0e4ea] px-3 py-2 text-sm text-[#1f2430]">{account.currency}</td>
                  <td className="border-b border-[#e0e4ea] px-3 py-2 text-right">
                    <Input
                      type="number"
                      value={snapshotDrafts[account.id]?.planned ?? "0"}
                      onChange={(event) =>
                        setSnapshotDrafts((current) => ({
                          ...current,
                          [account.id]: {
                            planned: event.target.value,
                            actual: current[account.id]?.actual ?? ""
                          }
                        }))
                      }
                      className="ml-auto w-36 text-right"
                    />
                  </td>
                  <td className="border-b border-[#e0e4ea] px-3 py-2 text-right">
                    <Input
                      type="number"
                      value={snapshotDrafts[account.id]?.actual ?? ""}
                      onChange={(event) =>
                        setSnapshotDrafts((current) => ({
                          ...current,
                          [account.id]: {
                            planned: current[account.id]?.planned ?? "0",
                            actual: event.target.value
                          }
                        }))
                      }
                      className="ml-auto w-36 text-right"
                    />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
      </SnapshotsPanel>
        </AccountsGeneralPanel>
      ) : null}

      {subpage === "investments" ? (
      <InvestmentsPanel>
      <section className="rounded-[22px] border border-[#cfd3da] bg-[#f6f7f9] p-5 md:p-6">
        <div className="flex items-center justify-between gap-3">
          <div>
            <h2 className="text-xl md:text-2xl font-medium text-[#171a24]">Investments Dashboard</h2>
            <p className="mt-2 text-sm md:text-base text-[#73788d]">Allocation, valuation, and profit/loss with manual price override.</p>
          </div>
          <Button type="button" onClick={() => void refreshPrices()} disabled={saving}>
            Refresh Market Prices
          </Button>
        </div>

        <div className="mt-4 grid gap-4 md:grid-cols-3">
          {investmentTotalsByCurrency.length === 0 ? (
            <AccountsStatCard label="Portfolio Value" value={asCurrencyBy(0, currency)} />
          ) : (
            investmentTotalsByCurrency.map(([totalCurrency, totals]) => (
              <div key={totalCurrency} className="space-y-2">
                <AccountsStatCard label={`Portfolio Value (${totalCurrency})`} value={asCurrencyBy(totals.value, totalCurrency)} />
                <AccountsStatCard label={`Cost Basis (${totalCurrency})`} value={asCurrencyBy(totals.costBasis, totalCurrency)} />
                <AccountsStatCard
                  label={`Profit / Loss (${totalCurrency})`}
                  value={asSignedCurrencyBy(totals.profitLoss, totalCurrency)}
                  tone={totals.profitLoss >= 0 ? "text-[#10a34a]" : "text-[#e11d48]"}
                />
              </div>
            ))
          )}
        </div>

        <div className="mt-5 grid gap-2 md:grid-cols-[1fr_130px_130px_130px_auto]">
          <select
            value={holdingAccountId}
            onChange={(event) => setHoldingAccountId(event.target.value)}
            className="h-10 rounded-md border border-input bg-background px-3 text-sm"
          >
            <option value="">Brokerage account</option>
            {brokerageAccounts.map((account) => (
              <option key={account.id} value={account.id}>
                {account.name}
              </option>
            ))}
          </select>
          <Input value={holdingSymbol} onChange={(event) => setHoldingSymbol(event.target.value)} placeholder="Symbol" />
          <Input type="number" value={holdingUnits} onChange={(event) => setHoldingUnits(event.target.value)} placeholder="Units" />
          <Input
            type="number"
            value={holdingAverageCost}
            onChange={(event) => setHoldingAverageCost(event.target.value)}
            placeholder="Avg cost"
          />
          <Button type="button" onClick={() => void createHolding()} disabled={saving}>
            Add Holding
          </Button>
        </div>

        <div className="mt-2 grid gap-2 md:grid-cols-[1fr_180px]">
          <div />
          <Input
            type="number"
            value={holdingManualPrice}
            onChange={(event) => setHoldingManualPrice(event.target.value)}
            placeholder="Optional manual price"
          />
        </div>

        <div className="mt-6 overflow-x-auto">
          <table className="w-max min-w-full border-collapse">
            <thead>
              <tr>
                <th className="border-b border-[#d2d7df] px-3 py-2 text-left text-sm font-semibold text-[#1f2430]">Symbol</th>
                <th className="border-b border-[#d2d7df] px-3 py-2 text-left text-sm font-semibold text-[#1f2430]">Account</th>
                <th className="border-b border-[#d2d7df] px-3 py-2 text-left text-sm font-semibold text-[#1f2430]">Currency</th>
                <th className="border-b border-[#d2d7df] px-3 py-2 text-right text-sm font-semibold text-[#1f2430]">Units</th>
                <th className="border-b border-[#d2d7df] px-3 py-2 text-right text-sm font-semibold text-[#1f2430]">Avg Cost</th>
                <th className="border-b border-[#d2d7df] px-3 py-2 text-right text-sm font-semibold text-[#1f2430]">Price</th>
                <th className="border-b border-[#d2d7df] px-3 py-2 text-right text-sm font-semibold text-[#1f2430]">Value</th>
                <th className="border-b border-[#d2d7df] px-3 py-2 text-right text-sm font-semibold text-[#1f2430]">P/L</th>
                <th className="border-b border-[#d2d7df] px-3 py-2 text-left text-sm font-semibold text-[#1f2430]">Manual Override</th>
                <th className="border-b border-[#d2d7df] px-3 py-2 text-left text-sm font-semibold text-[#1f2430]">Actions</th>
              </tr>
            </thead>
            <tbody>
              {groupedHoldings.length === 0 ? (
                <tr>
                  <td colSpan={10} className="border-b border-[#e0e4ea] px-3 py-4 text-center text-sm text-[#6f7489]">
                    No holdings yet.
                  </td>
                </tr>
              ) : (
                groupedHoldings.map((accountGroup) => {
                  const accountExpanded = expandedHoldingAccounts[accountGroup.accountId] ?? true;

                  return (
                    <Fragment key={accountGroup.accountId}>
                      <tr className="ui-surface-soft">
                        <td colSpan={10} className="border-b border-[#d2d7df] px-2 py-1">
                          <button
                            type="button"
                            onClick={() =>
                              setExpandedHoldingAccounts((current) => ({
                                ...current,
                                [accountGroup.accountId]: !accountExpanded
                              }))
                            }
                            className="ui-text-strong ui-hover-soft inline-flex w-full items-center gap-2 rounded-md px-2 py-1 text-left text-sm font-medium"
                          >
                            {accountExpanded ? <ChevronDownIcon size={16} /> : <ChevronRightIcon size={16} />}
                            <span>{accountGroup.accountName}</span>
                            <span className="ui-text-muted text-xs">
                              ({accountGroup.accountCurrency}) · {accountGroup.symbols.length} symbols
                            </span>
                          </button>
                        </td>
                      </tr>

                      {accountExpanded &&
                        accountGroup.symbols.map((symbolGroup) => {
                          const hasMultipleLots = symbolGroup.holdings.length > 1;
                          const symbolExpanded = expandedHoldingSymbols[symbolGroup.key] ?? false;
                          const singleHolding = hasMultipleLots ? null : symbolGroup.holdings[0];

                          return (
                            <Fragment key={symbolGroup.key}>
                              <tr className="ui-surface">
                                <td className="border-b border-[#e0e4ea] px-3 py-2 text-sm text-[#1f2430]">
                                  <div className="inline-flex items-center gap-2">
                                    {hasMultipleLots ? (
                                      <button
                                        type="button"
                                        onClick={() =>
                                          setExpandedHoldingSymbols((current) => ({
                                            ...current,
                                            [symbolGroup.key]: !symbolExpanded
                                          }))
                                        }
                                        className="ui-text inline-flex items-center gap-1 rounded px-1 py-0.5 hover:bg-[#e9edf4]"
                                      >
                                        {symbolExpanded ? <ChevronDownIcon size={14} /> : <ChevronRightIcon size={14} />}
                                        <span>{symbolGroup.symbol}</span>
                                      </button>
                                    ) : (
                                      <span>{symbolGroup.symbol}</span>
                                    )}
                                    {hasMultipleLots ? <span className="ui-text-muted text-xs">{symbolGroup.holdings.length} lots</span> : null}
                                  </div>
                                </td>
                                <td className="border-b border-[#e0e4ea] px-3 py-2 text-sm text-[#1f2430]">{symbolGroup.accountName}</td>
                                <td className="border-b border-[#e0e4ea] px-3 py-2 text-sm text-[#1f2430]">{symbolGroup.accountCurrency}</td>
                                <td className="border-b border-[#e0e4ea] px-3 py-2 text-right text-sm text-[#1f2430]">{symbolGroup.units}</td>
                                <td className="border-b border-[#e0e4ea] px-3 py-2 text-right text-sm text-[#1f2430]">{asCurrencyBy(symbolGroup.averageCost, symbolGroup.accountCurrency)}</td>
                                <td className="border-b border-[#e0e4ea] px-3 py-2 text-right text-sm text-[#1f2430]">{asCurrencyBy(symbolGroup.effectivePrice, symbolGroup.accountCurrency)}</td>
                                <td className="border-b border-[#e0e4ea] px-3 py-2 text-right text-sm text-[#1f2430]">{asCurrencyBy(symbolGroup.currentValue, symbolGroup.accountCurrency)}</td>
                                <td
                                  className={`border-b border-[#e0e4ea] px-3 py-2 text-right text-sm ${
                                    symbolGroup.profitLoss >= 0 ? "text-[#10a34a]" : "text-[#e11d48]"
                                  }`}
                                >
                                  {asSignedCurrencyBy(symbolGroup.profitLoss, symbolGroup.accountCurrency)}
                                </td>
                                <td className="border-b border-[#e0e4ea] px-3 py-2">
                                  {singleHolding ? (
                                    <Input
                                      type="number"
                                      value={manualPriceDrafts[singleHolding.id] ?? ""}
                                      onChange={(event) =>
                                        setManualPriceDrafts((current) => ({
                                          ...current,
                                          [singleHolding.id]: event.target.value
                                        }))
                                      }
                                      className="h-9 w-28 text-right"
                                    />
                                  ) : (
                                    <span className="ui-text-muted text-xs">Expand to edit lots</span>
                                  )}
                                </td>
                                <td className="border-b border-[#e0e4ea] px-3 py-2">
                                  {singleHolding ? (
                                    <div className="flex items-center gap-2">
                                      <AccountsIconActionButton
                                        label="Save manual price"
                                        tone="default"
                                        onClick={() => void saveManualPrice(singleHolding)}
                                        disabled={saving}
                                      >
                                        <SaveIcon size={14} />
                                      </AccountsIconActionButton>
                                      <AccountsIconActionButton
                                        label="Remove holding"
                                        tone="danger"
                                        onClick={() => void removeHolding(singleHolding.id)}
                                        disabled={saving}
                                      >
                                        <TrashIcon size={14} />
                                      </AccountsIconActionButton>
                                    </div>
                                  ) : (
                                    <span className="ui-text-muted text-xs">Expand for row actions</span>
                                  )}
                                </td>
                              </tr>

                              {hasMultipleLots && symbolExpanded
                                ? symbolGroup.holdings.map((holding, index) => (
                                    <tr key={holding.id}>
                                      <td className="border-b border-[#e0e4ea] px-3 py-2 pl-8 text-sm text-[#1f2430]">
                                        <span className="ui-text-muted">Lot #{index + 1}</span>
                                      </td>
                                      <td className="border-b border-[#e0e4ea] px-3 py-2 text-sm text-[#1f2430]">{holding.accountName}</td>
                                      <td className="border-b border-[#e0e4ea] px-3 py-2 text-sm text-[#1f2430]">{holding.accountCurrency}</td>
                                      <td className="border-b border-[#e0e4ea] px-3 py-2 text-right text-sm text-[#1f2430]">{holding.units}</td>
                                      <td className="border-b border-[#e0e4ea] px-3 py-2 text-right text-sm text-[#1f2430]">{asCurrencyBy(holding.averageCost, holding.accountCurrency)}</td>
                                      <td className="border-b border-[#e0e4ea] px-3 py-2 text-right text-sm text-[#1f2430]">{asCurrencyBy(holding.effectivePrice, holding.accountCurrency)}</td>
                                      <td className="border-b border-[#e0e4ea] px-3 py-2 text-right text-sm text-[#1f2430]">{asCurrencyBy(holding.currentValue, holding.accountCurrency)}</td>
                                      <td className={`border-b border-[#e0e4ea] px-3 py-2 text-right text-sm ${holding.profitLoss >= 0 ? "text-[#10a34a]" : "text-[#e11d48]"}`}>
                                        {asSignedCurrencyBy(holding.profitLoss, holding.accountCurrency)}
                                      </td>
                                      <td className="border-b border-[#e0e4ea] px-3 py-2">
                                        <Input
                                          type="number"
                                          value={manualPriceDrafts[holding.id] ?? ""}
                                          onChange={(event) =>
                                            setManualPriceDrafts((current) => ({
                                              ...current,
                                              [holding.id]: event.target.value
                                            }))
                                          }
                                          className="h-9 w-28 text-right"
                                        />
                                      </td>
                                      <td className="border-b border-[#e0e4ea] px-3 py-2">
                                        <div className="flex items-center gap-2">
                                          <AccountsIconActionButton
                                            label="Save manual price"
                                            tone="default"
                                            onClick={() => void saveManualPrice(holding)}
                                            disabled={saving}
                                          >
                                            <SaveIcon size={14} />
                                          </AccountsIconActionButton>
                                          <AccountsIconActionButton
                                            label="Remove holding"
                                            tone="danger"
                                            onClick={() => void removeHolding(holding.id)}
                                            disabled={saving}
                                          >
                                            <TrashIcon size={14} />
                                          </AccountsIconActionButton>
                                        </div>
                                      </td>
                                    </tr>
                                  ))
                                : null}
                            </Fragment>
                          );
                        })}
                    </Fragment>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      </section>
      </InvestmentsPanel>
      ) : null}

      {subpage === "savings" ? (
      <SavingsGoalsPanel>
      <section className="rounded-[22px] border border-[#cfd3da] bg-[#f6f7f9] p-5 md:p-6">
        <h2 className="text-xl md:text-2xl font-medium text-[#171a24]">Savings Goals</h2>
        <p className="mt-2 text-sm md:text-base text-[#73788d]">Track emergency fund and long-term targets.</p>

        <fieldset className="mt-5 rounded-2xl border border-[#d9dde6] bg-white/60 p-4">
          <legend className="px-1 text-sm font-medium text-[#1f2430]">Create new goal</legend>
          <div className="mt-2 grid gap-3 md:grid-cols-[minmax(160px,1fr)_minmax(200px,1fr)_140px_140px_220px_auto]">
            <div className="space-y-1">
              <Label htmlFor="new-goal-name">Goal name</Label>
              <Input id="new-goal-name" value={goalName} onChange={(event) => setGoalName(event.target.value)} placeholder="Emergency Fund" />
            </div>
            <div className="space-y-1">
              <Label htmlFor="new-goal-account">Linked account (optional)</Label>
              <select
                id="new-goal-account"
                value={goalAccountId}
                onChange={(event) => setGoalAccountId(event.target.value)}
                className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm"
              >
                <option value="">No linked account</option>
                {activeAccounts.map((account) => (
                  <option key={account.id} value={account.id}>
                    {account.name}
                  </option>
                ))}
              </select>
            </div>
            <div className="space-y-1">
              <Label htmlFor="new-goal-target">Target amount</Label>
              <Input
                id="new-goal-target"
                type="number"
                value={goalTargetAmount}
                onChange={(event) => setGoalTargetAmount(event.target.value)}
                placeholder="10000"
              />
            </div>
            <div className="space-y-1">
              <Label htmlFor="new-goal-current">Current amount</Label>
              <Input
                id="new-goal-current"
                type="number"
                value={goalCurrentAmount}
                onChange={(event) => setGoalCurrentAmount(event.target.value)}
                placeholder="2500"
              />
            </div>
            <div className="space-y-1">
              <Label htmlFor="new-goal-monthly" className="whitespace-nowrap">Monthly contribution target</Label>
              <Input
                id="new-goal-monthly"
                type="number"
                value={goalMonthlyContribution}
                onChange={(event) => setGoalMonthlyContribution(event.target.value)}
                placeholder="500"
              />
            </div>
            <div className="flex items-end">
              <Button type="button" onClick={() => void createSavingsGoal()} disabled={saving} className="w-full md:w-auto">
                Add Goal
              </Button>
            </div>
          </div>
        </fieldset>

        <div className="mt-6 space-y-3">
          {(accountsOverview?.savingsGoals ?? []).map((goal) => (
            <article key={goal.id} className="ui-border ui-surface-soft rounded-[16px] border p-4">
              {(() => {
                const goalCurrency = goal.accountId ? (accountCurrencyById.get(goal.accountId) ?? (accountsOverview?.summary.baseCurrency ?? currency)) : (accountsOverview?.summary.baseCurrency ?? currency);
                const isEditingGoal = editingGoalId === goal.id && goalEditDraft !== null;
                return (
                  <>
                    <div className="flex flex-wrap items-start justify-between gap-2">
                      <div>
                        <h3 className="ui-text-strong text-base font-medium">{goal.name}</h3>
                        <p className="ui-text mt-1 text-sm">
                          {asCurrencyBy(goal.currentAmount, goalCurrency)} / {asCurrencyBy(goal.targetAmount, goalCurrency)}
                        </p>
                      </div>
                      <div className="flex flex-wrap items-center gap-2">
                        <Button
                          type="button"
                          size="sm"
                          variant="outline"
                          onClick={() => {
                            if (isEditingGoal) {
                              cancelEditGoal();
                            } else {
                              beginEditGoal(goal);
                            }
                          }}
                          disabled={saving}
                        >
                          {isEditingGoal ? "Cancel Edit" : "Edit"}
                        </Button>
                        <Button
                          type="button"
                          size="sm"
                          variant="outline"
                          onClick={() => void markGoalDone(goal)}
                          disabled={saving || goal.progressPercent >= 100}
                        >
                          Mark Done
                        </Button>
                        <Button
                          type="button"
                          size="sm"
                          variant="outline"
                          className="border-[#dc2626] text-[#dc2626] hover:bg-[#dc2626]/10"
                          onClick={() => void removeGoal(goal)}
                          disabled={saving}
                        >
                          Remove
                        </Button>
                      </div>
                    </div>

                    {isEditingGoal ? (
                      <fieldset className="mt-3 rounded-xl border border-[#d9dde6] bg-white/70 p-3">
                        <legend className="px-1 text-xs font-medium text-[#4b5568]">Edit goal</legend>
                        <div className="mt-2 grid gap-3 md:grid-cols-[minmax(160px,1fr)_minmax(200px,1fr)_140px_140px_220px_auto_auto]">
                          <div className="space-y-1">
                            <Label htmlFor={`goal-name-${goal.id}`}>Goal name</Label>
                            <Input
                              id={`goal-name-${goal.id}`}
                              value={goalEditDraft.name}
                              onChange={(event) =>
                                setGoalEditDraft((current) =>
                                  current
                                    ? {
                                        ...current,
                                        name: event.target.value
                                      }
                                    : current
                                )
                              }
                            />
                          </div>
                          <div className="space-y-1">
                            <Label htmlFor={`goal-account-${goal.id}`}>Linked account (optional)</Label>
                            <select
                              id={`goal-account-${goal.id}`}
                              value={goalEditDraft.accountId}
                              onChange={(event) =>
                                setGoalEditDraft((current) =>
                                  current
                                    ? {
                                        ...current,
                                        accountId: event.target.value
                                      }
                                    : current
                                )
                              }
                              className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm"
                            >
                              <option value="">No linked account</option>
                              {activeAccounts.map((account) => (
                                <option key={account.id} value={account.id}>
                                  {account.name}
                                </option>
                              ))}
                            </select>
                          </div>
                          <div className="space-y-1">
                            <Label htmlFor={`goal-target-${goal.id}`}>Target amount</Label>
                            <Input
                              id={`goal-target-${goal.id}`}
                              type="number"
                              value={goalEditDraft.targetAmount}
                              onChange={(event) =>
                                setGoalEditDraft((current) =>
                                  current
                                    ? {
                                        ...current,
                                        targetAmount: event.target.value
                                      }
                                    : current
                                )
                              }
                            />
                          </div>
                          <div className="space-y-1">
                            <Label htmlFor={`goal-current-${goal.id}`}>Current amount</Label>
                            <Input
                              id={`goal-current-${goal.id}`}
                              type="number"
                              value={goalEditDraft.currentAmount}
                              onChange={(event) =>
                                setGoalEditDraft((current) =>
                                  current
                                    ? {
                                        ...current,
                                        currentAmount: event.target.value
                                      }
                                    : current
                                )
                              }
                            />
                          </div>
                          <div className="space-y-1">
                            <Label htmlFor={`goal-monthly-${goal.id}`} className="whitespace-nowrap">Monthly contribution target</Label>
                            <Input
                              id={`goal-monthly-${goal.id}`}
                              type="number"
                              value={goalEditDraft.monthlyContributionTarget}
                              onChange={(event) =>
                                setGoalEditDraft((current) =>
                                  current
                                    ? {
                                        ...current,
                                        monthlyContributionTarget: event.target.value
                                      }
                                    : current
                                )
                              }
                            />
                          </div>
                          <div className="flex items-end">
                            <Button type="button" size="sm" onClick={() => void saveEditedGoal(goal.id)} disabled={saving}>
                              Save
                            </Button>
                          </div>
                          <div className="flex items-end">
                            <Button type="button" size="sm" variant="outline" onClick={cancelEditGoal} disabled={saving}>
                              Cancel
                            </Button>
                          </div>
                        </div>
                      </fieldset>
                    ) : (
                      <>
                        <div className="mt-2 h-2 overflow-hidden rounded-full bg-[#e6eaf1]">
                          <div
                            className="h-full rounded-full bg-[#0ea5a2]"
                            style={{ width: `${Math.max(0, Math.min(goal.progressPercent, 100))}%` }}
                          />
                        </div>
                        <div className="ui-text-muted mt-2 flex flex-wrap items-center gap-3 text-sm">
                          <span>{goal.progressPercent.toFixed(2)}%</span>
                          <span>Monthly target: {asCurrencyBy(goal.monthlyContributionTarget, goalCurrency)}</span>
                          <span>{goal.accountName ? `Linked: ${goal.accountName}` : "Unlinked goal"}</span>
                        </div>
                      </>
                    )}
                  </>
                );
              })()}
            </article>
          ))}
        </div>
      </section>
      </SavingsGoalsPanel>
      ) : null}

      {loading ? <p className="text-sm text-[#6b7280]">Loading...</p> : null}
    </div>
  );
}
