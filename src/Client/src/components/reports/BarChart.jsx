import { Box, Tooltip, Typography } from "@mui/material";

// Single-series bar chart: one hue (theme primary, ≥ 4.5:1 on white), 4px rounded tops on a shared baseline, 2px gaps, hover tooltip per bar.
// The table beside each chart is the accessible data view, so values here are labels, not the only source.
export default function BarChart({ bars, format, max, label }) {
  if (!bars.length) return <Typography color="text.secondary">Không có dữ liệu.</Typography>;
  const top = max ?? (Math.max(...bars.map((b) => b.value)) || 1);
  return (
    <Box role="img" aria-label={label}>
      <Box sx={{ display: "flex", alignItems: "flex-end", gap: "2px", height: 240, borderBottom: 1, borderColor: "divider" }}>
        {bars.map((b) => (
          <Tooltip key={b.label} title={`${b.label}: ${format(b.value)}`} placement="top">
            <Box sx={{ flex: 1, minWidth: 0, height: "100%", display: "flex", flexDirection: "column", justifyContent: "flex-end", alignItems: "center", cursor: "default" }}>
              <Typography variant="caption" color="text.secondary" noWrap sx={{ mb: 0.5 }}>{format(b.value)}</Typography>
              <Box sx={{ width: "70%", maxWidth: 56, bgcolor: "primary.main", borderRadius: "4px 4px 0 0", height: `${(Math.max(0, b.value) / top) * 85}%` }} />
            </Box>
          </Tooltip>
        ))}
      </Box>
      <Box sx={{ display: "flex", gap: "2px", mt: 1 }}>
        {bars.map((b) => (
          <Typography key={b.label} variant="caption" color="text.secondary" noWrap sx={{ flex: 1, minWidth: 0, textAlign: "center" }}>{b.label}</Typography>
        ))}
      </Box>
    </Box>
  );
}
