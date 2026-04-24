"use client";

import type { ReactNode } from "react";
import { useEffect, useState } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { cn } from "@/lib/utils";
import { useThemeSetting } from "@/lib/currency-settings";
import {
  ArrowRightIcon,
  AccountsIcon,
  AppLogoIcon,
  BusinessIcon,
  CalendarIcon,
  ChevronLeftIcon,
  ChevronRightIcon,
  CloseIcon,
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
    href: "/accounts/general",
    label: "Accounts",
    aliases: ["/accounts"],
    icon: AccountsIcon
  },
  {
    href: "/projects",
    label: "Projects",
    aliases: ["/projects"],
    icon: BusinessIcon
  },
  {
    href: "/settings",
    label: "Settings",
    aliases: ["/settings"],
    icon: SettingsIcon
  }
] as const;

const NAV_COLLAPSE_KEY = "budget.nav.collapsed.v1";

function isItemActive(pathname: string, aliases: readonly string[]): boolean {
  if (aliases.includes("/")) {
    return pathname === "/";
  }

  return aliases.some((alias) => pathname === alias || pathname.startsWith(`${alias}/`));
}

export function BudgetShell({ children }: { children: ReactNode }) {
  useThemeSetting();
  const pathname = usePathname();
  const [collapsed, setCollapsed] = useState<boolean>(false);
  const [mobileMenuOpen, setMobileMenuOpen] = useState<boolean>(false);

  useEffect(() => {
    const raw = window.localStorage.getItem(NAV_COLLAPSE_KEY);
    if (raw === "1") {
      setCollapsed(true);
    }
  }, []);

  useEffect(() => {
    setMobileMenuOpen(false);
  }, [pathname]);

  useEffect(() => {
    if (!mobileMenuOpen) {
      return;
    }

    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";

    return () => {
      document.body.style.overflow = previousOverflow;
    };
  }, [mobileMenuOpen]);

  function toggleCollapsed() {
    setCollapsed((current) => {
      const next = !current;
      window.localStorage.setItem(NAV_COLLAPSE_KEY, next ? "1" : "0");
      return next;
    });
  }

  function renderNavItems(options?: {
    desktopCompact?: boolean;
    onItemClick?: () => void;
    lightPalette?: boolean;
  }) {
    return NAV_ITEMS.map((item) => {
      const active = isItemActive(pathname, item.aliases);
      const Icon = item.icon;

      return (
        <Link
          key={item.href}
          href={item.href}
          onClick={options?.onItemClick}
          className={cn(
            "flex h-11 items-center gap-2.5 rounded-xl px-3 text-sm transition-colors",
            options?.desktopCompact && "lg:justify-center lg:px-0",
            active
              ? "bg-[#040426] text-white"
              : options?.lightPalette
                ? "text-[#171923] hover:bg-[#f3f4f7]"
                : "ui-text ui-hover-soft"
          )}
        >
          <Icon size={24} className={active ? "text-white" : options?.lightPalette ? "text-[#151827]" : "ui-nav-icon"} />
          <span className={cn(options?.desktopCompact && "lg:hidden")}>{item.label}</span>
        </Link>
      );
    });
  }

  return (
    <main className="ui-page-bg min-h-screen">
      <div className="ui-border mx-auto flex min-h-screen max-w-[1720px] flex-col border-x lg:flex-row">
        <div className="ui-border flex items-center justify-between border-b px-5 py-6 lg:hidden">
          <div className="flex items-center gap-4">
            <div className="grid h-12 w-12 place-items-center rounded-xl bg-[#030426] text-white">
              <AppLogoIcon size={24} />
            </div>
            <div>
              <p className="ui-text-strong text-lg leading-tight font-semibold tracking-[-0.02em]">Budget Tracker</p>
              <p className="ui-text-muted text-sm">B2B Finance</p>
            </div>
          </div>

          <button
            type="button"
            onClick={() => setMobileMenuOpen(true)}
            className="ui-border ui-surface-soft ui-hover-soft text-[#171923] grid h-10 w-10 place-items-center rounded-lg border"
            aria-label="Open navigation menu"
            title="Open navigation menu"
          >
            <span className="sr-only">Open navigation menu</span>
            <span className="relative block h-4 w-5">
              <span className="absolute inset-x-0 top-0 h-0.5 rounded bg-current" />
              <span className="absolute inset-x-0 top-[7px] h-0.5 rounded bg-current" />
              <span className="absolute inset-x-0 top-[14px] h-0.5 rounded bg-current" />
            </span>
          </button>
        </div>

        <aside
          className={cn(
            "ui-border hidden w-full shrink-0 flex-col border-b transition-[width,basis] duration-200 lg:flex lg:flex-none lg:self-start lg:sticky lg:top-0 lg:h-screen lg:border-r lg:border-b-0",
            collapsed ? "lg:w-[84px] lg:basis-[84px]" : "lg:w-[300px] lg:basis-[300px]"
          )}
        >
          <div className="ui-border relative border-b px-5 py-6">
            <button
              type="button"
              onClick={toggleCollapsed}
              className="ui-border ui-surface-soft ui-hover-soft ui-text absolute right-3 top-3 hidden h-8 w-8 place-items-center rounded-lg border lg:grid"
              aria-label={collapsed ? "Expand sidebar" : "Collapse sidebar"}
              title={collapsed ? "Expand sidebar" : "Collapse sidebar"}
            >
              {collapsed ? <ChevronRightIcon size={16} /> : <ChevronLeftIcon size={16} />}
            </button>

            <div className={cn("flex items-center gap-4 pt-1", collapsed && "lg:justify-center lg:gap-0")}>
              <div className="grid h-12 w-12 place-items-center rounded-xl bg-[#030426] text-white">
                <AppLogoIcon size={24} />
              </div>
              <div className={cn(collapsed && "lg:hidden")}>
                <p className="ui-text-strong text-lg md:text-xl leading-tight font-semibold tracking-[-0.02em]">Budget Tracker</p>
                <p className="ui-text-muted text-sm">B2B Finance</p>
              </div>
            </div>
          </div>

          <nav className="flex-1 space-y-1.5 overflow-y-auto px-3 py-4">
            {renderNavItems({ desktopCompact: collapsed })}
          </nav>

          <div className="ui-border mt-auto border-t px-5 py-5">
            <form action="/api/auth/logout" method="post" className="mt-3">
              <button
                type="submit"
                className={cn(
                  "ui-btn-secondary ui-border ui-hover-soft h-10 rounded-lg border px-3 text-sm",
                  collapsed && "lg:grid lg:h-9 lg:w-9 lg:place-items-center lg:px-0"
                )}
                aria-label="Sign out"
                title="Sign out"
              >
                {collapsed ? (
                  <>
                    <span className="lg:grid lg:place-items-center hidden">
                      <ArrowRightIcon size={14} />
                    </span>
                    <span className="lg:hidden">Sign out</span>
                  </>
                ) : (
                  "Sign out"
                )}
              </button>
            </form>
          </div>
        </aside>

        {mobileMenuOpen ? (
          <aside className="fixed inset-0 z-50 flex h-screen flex-col bg-white lg:hidden">
            <div className="flex items-center justify-between border-b border-[#d6d9de] px-5 py-6">
              <div className="flex items-center gap-4">
                <div className="grid h-12 w-12 place-items-center rounded-xl bg-[#030426] text-white">
                  <AppLogoIcon size={24} />
                </div>
                <div>
                  <p className="text-lg leading-tight font-semibold tracking-[-0.02em] text-[#0f1321]">Budget Tracker</p>
                  <p className="text-sm text-[#6f7489]">B2B Finance</p>
                </div>
              </div>

              <button
                type="button"
                onClick={() => setMobileMenuOpen(false)}
                className="grid h-10 w-10 place-items-center rounded-lg border border-[#d6d9de] bg-[#f6f7f9] text-[#171923] hover:bg-[#eef0f4]"
                aria-label="Close navigation menu"
                title="Close navigation menu"
              >
                <CloseIcon size={18} />
              </button>
            </div>

            <nav className="flex-1 space-y-1.5 overflow-y-auto px-3 py-4">{renderNavItems({ onItemClick: () => setMobileMenuOpen(false), lightPalette: true })}</nav>

            <div className="border-t border-[#d6d9de] px-5 py-5">
              <form action="/api/auth/logout" method="post" className="mt-3">
                <button
                  type="submit"
                  className="h-10 rounded-lg border border-[#d1d5dd] bg-[#f3f4f6] px-3 text-sm text-[#171b27] hover:bg-[#eef0f4]"
                  aria-label="Sign out"
                  title="Sign out"
                >
                  Sign out
                </button>
              </form>
            </div>
          </aside>
        ) : null}

        <section className="min-w-0 flex-1 px-4 py-5 sm:px-6 lg:px-8 lg:py-6">{children}</section>
      </div>
    </main>
  );
}
