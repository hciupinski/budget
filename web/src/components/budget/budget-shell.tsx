"use client";

import type { ReactNode } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { cn } from "@/lib/utils";
import {
  AccountsIcon,
  AppLogoIcon,
  CalendarIcon,
  DashboardIcon,
  SettingsIcon,
  TransactionsIcon
} from "@/components/budget/icons";

const NAV_ITEMS = [
  {
    href: "/",
    label: "Dashboard",
    aliases: ["/"],
    icon: DashboardIcon
  },
  {
    href: "/annual",
    label: "Annual Planning",
    aliases: ["/annual", "/annual-planner"],
    icon: CalendarIcon
  },
  {
    href: "/monthly",
    label: "Monthly Planning",
    aliases: ["/monthly", "/monthly-workspace"],
    icon: CalendarIcon
  },
  {
    href: "/transactions",
    label: "Transactions",
    aliases: ["/transactions"],
    icon: TransactionsIcon
  },
  {
    href: "/accounts",
    label: "Accounts",
    aliases: ["/accounts"],
    icon: AccountsIcon
  },
  {
    href: "/settings",
    label: "Settings",
    aliases: ["/settings"],
    icon: SettingsIcon
  }
] as const;

function isItemActive(pathname: string, aliases: readonly string[]): boolean {
  if (aliases.includes("/")) {
    return pathname === "/";
  }

  return aliases.some((alias) => pathname === alias || pathname.startsWith(`${alias}/`));
}

export function BudgetShell({ children }: { children: ReactNode }) {
  const pathname = usePathname();

  return (
    <main className="min-h-screen bg-[#f3f4f6] text-[#171923]">
      <div className="mx-auto flex min-h-screen max-w-[1720px] flex-col border-x border-[#d6d9de] lg:flex-row">
        <aside className="flex w-full flex-col border-b border-[#d6d9de] lg:w-[300px] lg:border-r lg:border-b-0">
          <div className="relative border-b border-[#d6d9de] px-5 py-6">
            <div className="flex items-center gap-4 pt-1">
              <div className="grid h-12 w-12 place-items-center rounded-xl bg-[#030426] text-white">
                <AppLogoIcon size={24} />
              </div>
              <div>
                <p className="text-lg md:text-xl leading-tight font-semibold tracking-[-0.02em]">Budget Tracker</p>
                <p className="text-sm text-[#6f748a]">B2B Finance</p>
              </div>
            </div>
          </div>

          <nav className="space-y-1.5 px-3 py-4">
            {NAV_ITEMS.map((item) => {
              const active = isItemActive(pathname, item.aliases);
              const Icon = item.icon;

              return (
                <Link
                  key={item.href}
                  href={item.href}
                  className={cn(
                    "flex h-11 items-center gap-2.5 rounded-xl px-3 text-sm transition-colors",
                    active
                      ? "bg-[#040426] text-white"
                      : "text-[#141824] hover:bg-[#e6e8ee]"
                  )}
                >
                  <Icon size={24} className={active ? "text-white" : "text-[#151827]"} />
                  <span>{item.label}</span>
                </Link>
              );
            })}
          </nav>

          <div className="mt-auto border-t border-[#d6d9de] px-5 py-5">
            <div className="rounded-xl bg-[#e9ebef] p-3 text-sm leading-tight text-[#656a80]">
              Manage your B2B finances with ease
            </div>
            <form action="/api/auth/logout" method="post" className="mt-3">
              <button
                type="submit"
                className="h-10 rounded-lg border border-[#c8ccd4] px-3 text-sm text-[#44495f] hover:bg-[#e7e9ee]"
              >
                Sign out
              </button>
            </form>
          </div>
        </aside>

        <section className="flex-1 px-4 py-5 sm:px-6 lg:px-8 lg:py-6">{children}</section>
      </div>
    </main>
  );
}
