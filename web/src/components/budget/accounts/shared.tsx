import type { ReactNode } from "react";
import { Button } from "@/components/ui/button";

export function AccountsIconActionButton({
  children,
  label,
  tone,
  onClick,
  disabled
}: {
  children: ReactNode;
  label: string;
  tone: "default" | "danger";
  onClick: () => void;
  disabled?: boolean;
}) {
  return (
    <Button
      type="button"
      size="sm"
      variant="outline"
      className={`h-8 w-8 rounded-md p-0 ${
        tone === "danger"
          ? "border-[#dc2626] text-[#dc2626] hover:bg-[#dc2626]/10"
          : "ui-btn-secondary ui-border"
      }`}
      onClick={onClick}
      disabled={disabled}
      aria-label={label}
      title={label}
    >
      {children}
    </Button>
  );
}

export function AccountsStatCard({ label, value, tone = "ui-text-strong" }: { label: string; value: string; tone?: string }) {
  return (
    <div className="ui-border ui-surface rounded-[20px] border p-4">
      <p className="ui-text-muted text-sm md:text-base">{label}</p>
      <p className={`mt-1 text-xl md:text-2xl font-medium ${tone}`}>{value}</p>
    </div>
  );
}
