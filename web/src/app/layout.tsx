import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Budget App",
  description: "Local-first budgeting workspace"
};

const themeInitScript = `
  try {
    const raw = localStorage.getItem("budget.general.theme.v1");
    const next = (raw || "LIGHT").toUpperCase() === "DARK" ? "dark" : "light";
    document.documentElement.setAttribute("data-theme", next);
  } catch {
    document.documentElement.setAttribute("data-theme", "light");
  }
`;

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" data-theme="light" suppressHydrationWarning>
      <head>
        <script dangerouslySetInnerHTML={{ __html: themeInitScript }} />
      </head>
      <body>{children}</body>
    </html>
  );
}
