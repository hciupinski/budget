import { ImageResponse } from "next/og";

export const size = {
  width: 180,
  height: 180
};

export const contentType = "image/png";

export default function AppleIcon() {
  return new ImageResponse(
    (
      <div
        style={{
          display: "flex",
          width: "100%",
          height: "100%",
          alignItems: "center",
          justifyContent: "center",
          background: "linear-gradient(145deg, #0b1220 0%, #1e293b 100%)"
        }}
      >
        <div
          style={{
            display: "flex",
            width: 124,
            height: 124,
            borderRadius: 28,
            border: "8px solid #14b8a6",
            alignItems: "center",
            justifyContent: "center",
            color: "#f8fafc",
            fontSize: 74,
            fontWeight: 800,
            lineHeight: 1,
            fontFamily: "ui-sans-serif, system-ui, -apple-system"
          }}
        >
          $
        </div>
      </div>
    ),
    {
      ...size
    }
  );
}
