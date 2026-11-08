import { Box, Card, CardContent, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Tooltip, Typography } from "@mui/material";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import { weekdayVi } from "../../../../utils/format";

const dayLabel = (day) => `${weekdayVi(day)} ${day.slice(8, 10)}/${day.slice(5, 7)}`;

const cellTone = (usage, total) => {
  if (!usage) return { bgcolor: "transparent", color: "text.secondary" };
  const committed = usage.reservedQuantity + usage.approvedQuantity;
  if (usage.hasConflict) return { bgcolor: "rgba(186, 26, 26, 0.10)", color: "error.dark" };
  if (usage.pendingQuantity > 0) return { bgcolor: "rgba(245, 158, 11, 0.14)", color: "warning.dark" };
  if (committed === 0) return { bgcolor: "transparent", color: "text.secondary" };
  if (committed >= total) return { bgcolor: "rgba(245, 158, 11, 0.14)", color: "warning.dark" };
  return { bgcolor: "rgba(21, 128, 61, 0.08)", color: "success.dark" };
};

// Lưới 2: shared resources × days. Each cell shows committed quantity / total capacity for that day;
// red cells are over capacity (GBR-09), amber cells are waiting for the Admin decision.
export default function ResourceGrid({ lanes, days, view, onlyConflicts, onOpenUsage }) {
  const visible = onlyConflicts ? lanes.filter((lane) => lane.usages.some((usage) => usage.hasConflict)) : lanes;

  return (
    <Card>
      <CardContent>
        <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "baseline", mb: 1.5, flexWrap: "wrap", gap: 1 }}>
          <Box>
            <Typography variant="h6">Lưới 2: Phân bổ tài nguyên dùng chung &amp; nhân sự kỹ thuật</Typography>
            <Typography variant="body2" color="text.secondary">
              Số đã giữ / tổng công suất mỗi ngày · bấm vào ô để xem phân bổ hoặc xử lý xung đột
            </Typography>
          </Box>
          <Stack direction="row" spacing={2} sx={{ alignItems: "center", flexWrap: "wrap" }}>
            <Stack direction="row" spacing={0.5} sx={{ alignItems: "center" }}>
              <Box sx={{ width: 12, height: 12, borderRadius: 0.5, bgcolor: "rgba(186, 26, 26, 0.10)", border: 1, borderColor: "error.light" }} />
              <Typography variant="caption" color="text.secondary">Xung đột công suất</Typography>
            </Stack>
            <Stack direction="row" spacing={0.5} sx={{ alignItems: "center" }}>
              <Box sx={{ width: 12, height: 12, borderRadius: 0.5, bgcolor: "rgba(245, 158, 11, 0.14)", border: 1, borderColor: "warning.light" }} />
              <Typography variant="caption" color="text.secondary">Chờ Admin</Typography>
            </Stack>
          </Stack>
        </Stack>
        <TableContainer sx={{ maxHeight: 620 }}>
          <Table size="small" stickyHeader id="resource-grid">
            <TableHead>
              <TableRow>
                <TableCell sx={{ minWidth: 220 }}>Tài nguyên dùng chung</TableCell>
                <TableCell align="center" sx={{ minWidth: 60 }}>Tổng</TableCell>
                {days.map((day) => <TableCell key={day} align="center" sx={{ minWidth: view === "day" ? 240 : 92 }}>{dayLabel(day)}</TableCell>)}
              </TableRow>
            </TableHead>
            <TableBody>
              {visible.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={days.length + 2} align="center" sx={{ py: 4 }}>
                    {onlyConflicts ? "Không có xung đột công suất trong kỳ đang xem." : "Chưa có tài nguyên nào được khai báo."}
                  </TableCell>
                </TableRow>
              ) : visible.map((lane) => (
                <TableRow key={lane.resourceId} hover>
                  <TableCell>
                    <Typography variant="body2" fontWeight={600}>{lane.name}</Typography>
                    <Typography variant="caption" color="text.secondary">{lane.typeLabel}</Typography>
                  </TableCell>
                  <TableCell align="center"><Typography variant="body2" fontWeight={600}>{lane.totalQuantity}</Typography></TableCell>
                  {days.map((day) => {
                    const usage = lane.usages.find((entry) => entry.date === day);
                    const committed = usage ? usage.reservedQuantity + usage.approvedQuantity : 0;
                    const tone = cellTone(usage, lane.totalQuantity);
                    return (
                      <TableCell key={day} align="center" sx={{ p: 0.5 }}>
                        <Tooltip title={usage ? `${usage.allocations.length} phân bổ · Đã giữ ${committed} · Chờ ${usage.pendingQuantity} · Còn lại ${Math.max(0, lane.totalQuantity - committed)}` : "Chưa phân bổ"}>
                          <Box role="button" tabIndex={0} onClick={() => onOpenUsage(lane, day)}
                            onKeyDown={(event) => { if (event.key === "Enter" || event.key === " ") onOpenUsage(lane, day); }}
                            sx={{
                              cursor: "pointer", borderRadius: 1.5, px: 0.5, py: 0.75, border: 1, borderColor: usage?.hasConflict ? "error.light" : "transparent",
                              ...tone, "&:hover": { outline: "1px solid", outlineColor: "primary.main" },
                            }}>
                            <Stack direction="row" spacing={0.25} sx={{ justifyContent: "center", alignItems: "center" }}>
                              {usage?.hasConflict && <WarningAmberIcon sx={{ fontSize: 14 }} />}
                              <Typography variant="caption" fontWeight={600}>{committed}/{lane.totalQuantity}</Typography>
                            </Stack>
                            {usage?.pendingQuantity > 0 && (
                              <Typography variant="caption" component="p" sx={{ fontSize: 10 }}>+{usage.pendingQuantity} chờ</Typography>
                            )}
                          </Box>
                        </Tooltip>
                      </TableCell>
                    );
                  })}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      </CardContent>
    </Card>
  );
}
