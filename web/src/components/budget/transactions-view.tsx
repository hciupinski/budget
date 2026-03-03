"use client";

import { useMemo, useState } from "react";
import { ArrowDownLeftIcon, ArrowUpRightIcon, DateIcon, PlusIcon } from "@/components/budget/icons";
import { asCurrency, asSignedCurrency } from "@/components/budget/budget-ui-utils";

type TransactionType = "INCOME" | "EXPENSE";

type TransactionRecord = {
  id: string;
  date: string;
  description: string;
  category: string;
  type: TransactionType;
  status: "paid";
  amount: number;
};

const TRANSACTIONS: TransactionRecord[] = [
  {
    id: "1",
    date: "Dec 1, 2024",
    description: "Client A - Consulting",
    category: "Consulting",
    type: "INCOME",
    status: "paid",
    amount: 5000
  },
  {
    id: "2",
    date: "Dec 15, 2024",
    description: "Client B - Development",
    category: "Development",
    type: "INCOME",
    status: "paid",
    amount: 3500
  },
  {
    id: "3",
    date: "Dec 5, 2024",
    description: "Adobe Creative Suite",
    category: "Software & Tools",
    type: "EXPENSE",
    status: "paid",
    amount: -550
  },
  {
    id: "4",
    date: "Dec 8, 2024",
    description: "GitHub Pro",
    category: "Software & Tools",
    type: "EXPENSE",
    status: "paid",
    amount: -300
  },
  {
    id: "5",
    date: "Dec 10, 2024",
    description: "Office Desk",
    category: "Office Supplies",
    type: "EXPENSE",
    status: "paid",
    amount: -1200
  },
  {
    id: "6",
    date: "Dec 12, 2024",
    description: "Accountant Fee",
    category: "Professional Services",
    type: "EXPENSE",
    status: "paid",
    amount: -450
  }
];

export function TransactionsView() {
  const [filter, setFilter] = useState<"ALL" | TransactionType>("ALL");

  const totals = useMemo(() => {
    const income = TRANSACTIONS.filter((item) => item.type === "INCOME").reduce((sum, item) => sum + item.amount, 0);
    const expenses = TRANSACTIONS
      .filter((item) => item.type === "EXPENSE")
      .reduce((sum, item) => sum + Math.abs(item.amount), 0);

    return {
      income,
      expenses,
      cashFlow: income - expenses
    };
  }, []);

  const filteredTransactions = useMemo(() => {
    if (filter === "ALL") {
      return TRANSACTIONS;
    }

    return TRANSACTIONS.filter((item) => item.type === filter);
  }, [filter]);

  return (
    <div className="space-y-6">
      <header className="flex flex-col gap-4 xl:flex-row xl:items-start xl:justify-between">
        <div>
          <h1 className="text-2xl md:text-3xl font-semibold tracking-[-0.02em] text-[#0f1321]">Transactions</h1>
          <p className="text-base text-[#71768b]">View and manage all your transactions</p>
        </div>

        <button
          type="button"
          className="inline-flex h-12 items-center gap-2 rounded-2xl bg-[#040426] px-5 text-sm md:text-base text-white hover:opacity-95"
        >
          <PlusIcon size={20} />
          Add Transaction
        </button>
      </header>

      <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        <StatCard label="Total Income" value={asCurrency(totals.income)} valueTone="text-[#10a34a]" />
        <StatCard label="Total Expenses" value={asCurrency(totals.expenses)} valueTone="text-[#e11d48]" />
        <StatCard label="Net Cash Flow" value={asCurrency(totals.cashFlow)} valueTone="text-[#111827]" />
      </section>

      <section className="rounded-[22px] border border-[#cfd3da] bg-[#f6f7f9] p-6">
        <div className="flex flex-wrap gap-2">
          <FilterButton active={filter === "ALL"} label="All" onClick={() => setFilter("ALL")} />
          <FilterButton active={filter === "INCOME"} label="Income" onClick={() => setFilter("INCOME")} />
          <FilterButton active={filter === "EXPENSE"} label="Expenses" onClick={() => setFilter("EXPENSE")} />
        </div>
      </section>

      <section className="rounded-[22px] border border-[#cfd3da] bg-[#f6f7f9] p-5 md:p-6">
        <h2 className="text-xl md:text-2xl font-medium text-[#171a24]">Transaction History</h2>
        <p className="mt-2 text-sm md:text-base text-[#73788d]">All your income and expenses in one place</p>

        <div className="mt-6 overflow-x-auto">
          <table className="w-max min-w-full border-collapse">
            <thead>
              <tr>
                <th className="border-b border-[#cfd3da] px-3 py-3 text-left text-sm md:text-base font-semibold text-[#171b25]">Date</th>
                <th className="border-b border-[#cfd3da] px-3 py-3 text-left text-sm md:text-base font-semibold text-[#171b25]">Description</th>
                <th className="border-b border-[#cfd3da] px-3 py-3 text-left text-sm md:text-base font-semibold text-[#171b25]">Category</th>
                <th className="border-b border-[#cfd3da] px-3 py-3 text-left text-sm md:text-base font-semibold text-[#171b25]">Type</th>
                <th className="border-b border-[#cfd3da] px-3 py-3 text-left text-sm md:text-base font-semibold text-[#171b25]">Status</th>
                <th className="border-b border-[#cfd3da] px-3 py-3 text-right text-sm md:text-base font-semibold text-[#171b25]">Amount</th>
              </tr>
            </thead>
            <tbody>
              {filteredTransactions.map((transaction) => (
                <tr key={transaction.id}>
                  <td className="border-b border-[#cfd3da] px-3 py-3 text-sm md:text-base text-[#1d2230]">
                    <span className="inline-flex items-center gap-2">
                      <DateIcon size={18} className="text-[#6f7489]" />
                      {transaction.date}
                    </span>
                  </td>
                  <td className="border-b border-[#cfd3da] px-3 py-3 text-sm md:text-base text-[#1d2230]">{transaction.description}</td>
                  <td className="border-b border-[#cfd3da] px-3 py-3 text-sm md:text-base text-[#1d2230]">{transaction.category}</td>
                  <td className="border-b border-[#cfd3da] px-3 py-3 text-sm md:text-base text-[#1d2230]">
                    <span className="inline-flex items-center gap-2">
                      {transaction.type === "INCOME" ? (
                        <ArrowDownLeftIcon size={18} className="text-[#10a34a]" />
                      ) : (
                        <ArrowUpRightIcon size={18} className="text-[#e11d48]" />
                      )}
                      {transaction.type === "INCOME" ? "Income" : "Business Expense"}
                    </span>
                  </td>
                  <td className="border-b border-[#cfd3da] px-3 py-3">
                    <span className="inline-flex h-7 items-center rounded-full bg-[#040426] px-3 text-xs md:text-sm text-white">
                      {transaction.status}
                    </span>
                  </td>
                  <td
                    className={`border-b border-[#cfd3da] px-3 py-3 text-right text-sm md:text-base ${
                      transaction.amount >= 0 ? "text-[#10a34a]" : "text-[#e11d48]"
                    }`}
                  >
                    {asSignedCurrency(transaction.amount)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
}

function StatCard({ label, value, valueTone }: { label: string; value: string; valueTone: string }) {
  return (
    <div className="rounded-[20px] border border-[#cfd3da] bg-[#f6f7f9] p-4">
      <p className="text-sm md:text-base text-[#71768b]">{label}</p>
      <p className={`mt-1 text-xl md:text-2xl font-medium ${valueTone}`}>{value}</p>
    </div>
  );
}

function FilterButton({
  active,
  label,
  onClick
}: {
  active: boolean;
  label: string;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`h-12 rounded-2xl px-6 text-sm md:text-base ${
        active
          ? "bg-[#040426] text-white"
          : "border border-[#d1d5dd] bg-[#f3f4f6] text-[#1d2230] hover:bg-[#e9ebf0]"
      }`}
    >
      {label}
    </button>
  );
}
