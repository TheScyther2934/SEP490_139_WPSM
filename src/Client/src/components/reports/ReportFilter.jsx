import { useState } from "react";
import { Link as RouterLink, useLocation } from "react-router-dom";
import { Alert, Button, Card, CardContent, Stack, Tab, Tabs, TextField } from "@mui/material";
import { PATHS } from "../../routes/paths";

const TABS = [
  { label: "Tổng hợp", to: PATHS.admin.reports },
  { label: "Công suất sảnh", to: PATHS.admin.hallUtilization },
  { label: "Doanh thu thuần", to: PATHS.admin.netRevenue },
  { label: "Thống kê CR", to: PATHS.admin.crStatistics },
];

// Report tabs + date range in one row above the charts; switching tab keeps the range.
export default function ReportFilter({ range }) {
  const { pathname } = useLocation();
  const [draft, setDraft] = useState({ from: range.from, to: range.to });
  const draftValid = draft.from && draft.to && draft.from <= draft.to;

  return (
    <>
      <Tabs value={pathname} variant="scrollable" sx={{ mb: 2, borderBottom: 1, borderColor: "divider" }} aria-label="Loại báo cáo">
        {TABS.map((t) => <Tab key={t.to} value={t.to} label={t.label} component={RouterLink} to={`${t.to}${range.query}`} />)}
      </Tabs>
      <Card sx={{ mb: 3 }}>
        <CardContent>
          <Stack component="form" direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ alignItems: { sm: "center" } }}
            onSubmit={(e) => { e.preventDefault(); if (draftValid) range.setRange(draft); }}>
            <TextField id="reportFrom" label="Từ ngày" type="date" size="small" value={draft.from}
              onChange={(e) => setDraft((d) => ({ ...d, from: e.target.value }))} slotProps={{ inputLabel: { shrink: true } }} />
            <TextField id="reportTo" label="Đến ngày" type="date" size="small" value={draft.to}
              onChange={(e) => setDraft((d) => ({ ...d, to: e.target.value }))} slotProps={{ inputLabel: { shrink: true } }} />
            <Button type="submit" variant="contained" disabled={!draftValid}>Áp dụng</Button>
          </Stack>
          {!draftValid && <Alert severity="warning" sx={{ mt: 2 }}>Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.</Alert>}
        </CardContent>
      </Card>
    </>
  );
}
