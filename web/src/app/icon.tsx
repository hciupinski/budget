import { ImageResponse } from "next/og";

export const size = {
  width: 512,
  height: 512
};

export const contentType = "image/png";

export default function Icon() {
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
            width: 340,
            height: 340,
            borderRadius: 68,
            border: "18px solid #14b8a6",
            alignItems: "center",
            justifyContent: "center",
            color: "#f8fafc",
            fontSize: 172,
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
