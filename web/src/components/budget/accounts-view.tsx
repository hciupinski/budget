import type { ReactNode } from "react";
import { asCurrency } from "@/components/budget/budget-ui-utils";
import { BusinessIcon, EditIcon, HomeIcon, InvestmentIcon, SavingsIcon } from "@/components/budget/icons";

type AccountKind = "Business" | "Personal" | "Savings" | "Investment";

type AccountItem = {
  id: string;
  name: string;
  kind: AccountKind;
  balance: number;
  icon: ReactNode;
};

const ACCOUNTS: AccountItem[] = [
  {
    id: "business",
    name: "Business Account",
    kind: "Business",
    balance: 15420.5,
    icon: <BusinessIcon size={22} />
  },
  {
    id: "personal",
    name: "Personal Checking",
    kind: "Personal",
    balance: 3240,
    icon: <HomeIcon size={22} />
  },
  {
    id: "savings",
    name: "Emergency Fund",
    kind: "Savings",
    balance: 12000,
    icon: <SavingsIcon size={22} />
  },
  {
    id: "investments",
    name: "Investment Portfolio",
    kind: "Investment",
    balance: 28500,
    icon: <InvestmentIcon size={22} />
  }
];

const TILE_TONES: Record<AccountKind, string> = {
  Business: "border-[#9ec5f0] bg-[#e6edf8]",
  Personal: "border-[#98e2bd] bg-[#e5f4eb]",
  Savings: "border-[#82e8dc] bg-[#e2f0f0]",
  Investment: "border-[#d8bdf5] bg-[#f1ebf8]"
};

const ICON_TONES: Record<AccountKind, string> = {
  Business: "bg-[#2d7af5]",
  Personal: "bg-[#22c55e]",
  Savings: "bg-[#0ea5a2]",
  Investment: "bg-[#8b5cf6]"
};

const KIND_ICONS: Record<AccountKind, ReactNode> = {
  Business: <BusinessIcon size={20} />,
  Personal: <HomeIcon size={20} />,
  Savings: <SavingsIcon size={20} />,
  Investment: <InvestmentIcon size={20} />
};

export function AccountsView() {
  const netWorth = ACCOUNTS.reduce((sum, account) => sum + account.balance, 0);

  return (
    <div className="space-y-6">
      <header>
        <h1 className="text-3xl md:text-4xl font-semibold tracking-[-0.02em] text-[#0f1321]">Account Management</h1>
        <p className="text-lg md:text-xl text-[#71768b]">View and update your account balances</p>
      </header>

      <section className="rounded-[22px] border border-[#cfd3da] bg-[#f6f7f9] p-6 md:p-8">
        <p className="text-xl md:text-2xl text-[#70768b]">Total Net Worth</p>
        <p className="text-4xl md:text-5xl font-medium text-[#141824]">{asCurrency(netWorth)}</p>
      </section>

      <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        {(["Business", "Personal", "Savings", "Investment"] as const).map((kind) => {
          const account = ACCOUNTS.find((item) => item.kind === kind);
          if (!account) {
            return null;
          }

          return (
            <div key={kind} className="rounded-[20px] border border-[#cfd3da] bg-[#f6f7f9] p-6">
              <p className="inline-flex items-center gap-2 text-lg md:text-xl text-[#70768b]">
                <span className="text-[#7c8094]">{KIND_ICONS[kind]}</span>
                {kind}
              </p>
              <p className="mt-2 text-3xl md:text-4xl font-medium text-[#141824]">{asCurrency(account.balance)}</p>
            </div>
          );
        })}
      </section>

      <section className="rounded-[22px] border border-[#cfd3da] bg-[#f6f7f9] p-6 md:p-8">
        <h2 className="text-2xl md:text-3xl font-medium text-[#171a24]">All Accounts</h2>
        <p className="mt-2 text-base md:text-lg text-[#73788d]">Manage and update your account balances</p>

        <div className="mt-6 grid gap-4 xl:grid-cols-2">
          {ACCOUNTS.map((account) => (
            <article key={account.id} className={`rounded-[18px] border p-5 ${TILE_TONES[account.kind]}`}>
              <div className="flex items-start justify-between gap-4">
                <div className="flex items-center gap-3">
                  <span className={`grid h-12 w-12 place-items-center rounded-2xl text-white ${ICON_TONES[account.kind]}`}>
                    {account.icon}
                  </span>
                  <div>
                    <h3 className="text-xl md:text-2xl text-[#1a1f2b]">{account.name}</h3>
                    <span className="mt-1 inline-flex h-8 items-center rounded-full border border-[#ccd2db] px-3 text-xs md:text-sm text-[#1f2431]">
                      {account.kind}
                    </span>
                  </div>
                </div>

                <button type="button" className="text-[#1a1f2b]">
                  <EditIcon size={24} />
                </button>
              </div>

              <p className="mt-4 text-3xl md:text-4xl font-medium text-[#111827]">{asCurrency(account.balance)}</p>
              <p className="text-sm md:text-base text-[#6d7287]">USD</p>
            </article>
          ))}
        </div>
      </section>
    </div>
  );
}
