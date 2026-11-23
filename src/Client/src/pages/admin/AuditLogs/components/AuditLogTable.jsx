import { useEffect, useRef, useState } from "react";
import { Link as RouterLink } from "react-router-dom";
import {
  Box, Button, Card, CardContent, Chip, Divider, Drawer, IconButton, InputAdornment, Link, MenuItem, Stack, Table, TableBody, TableCell,
  TableContainer, TableHead, TablePagination, TableRow, TextField, Typography,
} from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import DownloadIcon from "@mui/icons-material/Download";
import CloseIcon from "@mui/icons-material/Close";
import FilterAltOffIcon from "@mui/icons-material/FilterAltOff";
import DataState from "../../../../components/DataState";
import { useAsync } from "../../../../hooks/useAsync";
import { useAction } from "../../../../hooks/useAction";
import { adminService } from "../../../../services/adminService";
import { saveBlobAsFile } from "../../../../services/paymentService";
import { PATHS } from "../../../../routes/paths";
import { dateTimeVi, isoDate } from "../../../../utils/format";
import { ACTIONS, FIELDS, IMPACTS, ROLES, label } from "./auditLabels";

const EMPTY = { search: "", action: "", impact: "", role: "", userId: "", from: "", to: "" };
const shift = (days) => { const d = new Date(); d.setDate(d.getDate() + days); return isoDate(d); };
const QUICK = [
  { label: "Hôm nay", from: () => shift(0) },
  { label: "7 ngày", from: () => shift(-6) },
  { label: "30 ngày", from: () => shift(-29) },
];

// Audit log list with search, date range, activity/impact/role/user filters, paging, CSV export and a details drawer.
// `fixedFilters` (e.g. { projectCode }) narrows the list for the project History tab and hides the project column.
export default function AuditLogTable({ fixedFilters = {}, initial = {}, onFiltersChange }) {
  const [filters, setFilters] = useState({ ...EMPTY, ...initial });
  const [query, setQuery] = useState(filters.search);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [selected, setSelected] = useState(null);
  const timer = useRef(null);
  const { busy, run } = useAction();
  const invalidRange = filters.from && filters.to && filters.from > filters.to;
  const params = { ...Object.fromEntries(Object.entries(filters).filter(([, v]) => v)), ...fixedFilters };

  const options = useAsync(() => adminService.getAuditFilters());
  const { data, loading, error, reload } = useAsync(
    () => (invalidRange ? Promise.resolve(null) : adminService.getAuditLogs({ ...params, page, pageSize })),
    [JSON.stringify(params), page, pageSize]
  );
  useEffect(() => () => window.clearTimeout(timer.current), []);

  const change = (patch) => {
    const next = { ...filters, ...patch };
    setFilters(next);
    setPage(1);
    onFiltersChange?.(next);
  };
  const search = (text) => {
    setQuery(text);
    window.clearTimeout(timer.current);
    timer.current = window.setTimeout(() => change({ search: text.trim() }), 350);
  };
  const exportCsv = () => run(async () => {
    const blob = await adminService.exportAuditLogs(params);
    saveBlobAsFile(blob, `nhat-ky-hoat-dong_${isoDate(new Date())}.csv`);
  }, "Đã xuất nhật ký ra CSV.");

  const select = (key, text, allText, items) => (
    <TextField select size="small" label={text} value={filters[key]} onChange={(e) => change({ [key]: e.target.value })} sx={{ minWidth: 160 }}
      slotProps={{ select: { displayEmpty: true }, inputLabel: { shrink: true } }}>
      <MenuItem value="">{allText}</MenuItem>
      {items.map(([value, name]) => <MenuItem key={value} value={String(value)}>{name}</MenuItem>)}
    </TextField>
  );
  const date = (key, text) => (
    <TextField type="date" size="small" label={text} value={filters[key]} onChange={(e) => change({ [key]: e.target.value })}
      error={!!invalidRange} helperText={invalidRange && key === "to" ? "Phải sau ngày bắt đầu" : ""} slotProps={{ inputLabel: { shrink: true } }} />
  );
  const showProject = !fixedFilters.projectCode;
  const hasFilters = Object.values(filters).some(Boolean);

  return (
    <>
      <Card sx={{ mb: 2 }}>
        <CardContent>
          <Stack spacing={2}>
            <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
              <TextField id="auditSearch" size="small" label="Tìm theo người thực hiện, email, mã sự kiện, nội dung thay đổi" value={query}
                onChange={(e) => search(e.target.value)} sx={{ flex: 1 }}
                slotProps={{ input: { startAdornment: <InputAdornment position="start"><SearchIcon /></InputAdornment> } }} />
              <Button variant="outlined" startIcon={<DownloadIcon />} onClick={exportCsv} disabled={busy || !data?.total}>Xuất CSV</Button>
            </Stack>
            <Stack direction={{ xs: "column", md: "row" }} spacing={2} useFlexGap flexWrap="wrap">
              {date("from", "Từ ngày")}
              {date("to", "Đến ngày")}
              {select("action", "Hoạt động", "Tất cả hoạt động", (options.data?.actions ?? []).map((a) => [a, label(ACTIONS, a)]))}
              {select("impact", "Ảnh hưởng", "Tất cả phạm vi", (options.data?.impacts ?? []).map((i) => [i, label(IMPACTS, i)]))}
              {select("role", "Vai trò", "Tất cả vai trò", (options.data?.roles ?? []).map((r) => [r, label(ROLES, r)]))}
              {select("userId", "Người thực hiện", "Tất cả mọi người", (options.data?.users ?? []).map((u) => [u.id, u.name]))}
            </Stack>
            <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap" alignItems="center">
              {QUICK.map((q) => <Chip key={q.label} label={q.label} variant="outlined" onClick={() => change({ from: q.from(), to: shift(0) })} />)}
              <Button size="small" startIcon={<FilterAltOffIcon />} disabled={!hasFilters} sx={{ ml: { md: "auto" } }}
                onClick={() => { setQuery(""); change(EMPTY); }}>Xóa bộ lọc</Button>
            </Stack>
          </Stack>
        </CardContent>
      </Card>

      <DataState loading={loading} error={error} onRetry={reload}>
        {data && (
          <Card>
            <TableContainer>
              <Table size="small" id="auditTable">
                <TableHead>
                  <TableRow>
                    <TableCell>Thời gian</TableCell><TableCell>Người thực hiện</TableCell><TableCell align="center">Hoạt động</TableCell>
                    <TableCell>Ảnh hưởng</TableCell>{showProject && <TableCell>Sự kiện</TableCell>}<TableCell>Nội dung</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {data.items.length === 0 ? (
                    <TableRow><TableCell colSpan={showProject ? 6 : 5} align="center" sx={{ py: 5 }}>Không có hoạt động nào khớp bộ lọc.</TableCell></TableRow>
                  ) : data.items.map((r) => (
                    <TableRow key={r.auditId} hover sx={{ cursor: "pointer" }} onClick={() => setSelected(r)} data-audit={r.auditId}>
                      <TableCell sx={{ whiteSpace: "nowrap" }}>{dateTimeVi(r.occurredAt)}</TableCell>
                      <TableCell>
                        <Typography variant="body2" fontWeight={600}>{r.accountName ?? "—"}</Typography>
                        <Typography variant="caption" color="text.secondary">{label(ROLES, r.role)}{r.email ? ` · ${r.email}` : ""}</Typography>
                      </TableCell>
                      <TableCell align="center">
                        <Chip size="small" label={label(ACTIONS, r.action)} color={ACTIONS[r.action]?.color ?? "default"} variant={r.action === "Login" ? "outlined" : "filled"} />
                      </TableCell>
                      <TableCell>{label(IMPACTS, r.impact)}</TableCell>
                      {showProject && (
                        <TableCell onClick={(e) => e.stopPropagation()}>
                          {r.projectCode ? <Link component={RouterLink} to={PATHS.admin.project(r.projectCode)}>{r.projectCode}</Link> : "—"}
                        </TableCell>
                      )}
                      <TableCell sx={{ maxWidth: 420 }}><Typography variant="body2" noWrap title={r.summary}>{r.summary}</Typography></TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
            <TablePagination component="div" count={data.total} page={data.page - 1} rowsPerPage={data.pageSize} rowsPerPageOptions={[25, 50, 100]}
              onPageChange={(_, next) => setPage(next + 1)} onRowsPerPageChange={(e) => { setPageSize(Number(e.target.value)); setPage(1); }}
              labelRowsPerPage="Số dòng" labelDisplayedRows={({ from, to, count }) => `${from}–${to} trên ${count} hoạt động`} />
          </Card>
        )}
      </DataState>

      <Drawer anchor="right" open={!!selected} onClose={() => setSelected(null)} slotProps={{ paper: { sx: { width: { xs: "100%", sm: 520 } } } }}>
        {selected && <AuditDetail log={selected} onClose={() => setSelected(null)} />}
      </Drawer>
    </>
  );
}

const show = (value) => (value === null || value === undefined || value === "" ? "—" : typeof value === "boolean" ? (value ? "Có" : "Không") : String(value));

function AuditDetail({ log, onClose }) {
  let changes = [];
  try { changes = Object.entries(JSON.parse(log.changes || "{}")); } catch { changes = []; }
  const facts = [
    ["Thời gian", dateTimeVi(log.occurredAt)],
    ["Người thực hiện", `${log.accountName ?? "—"}${log.email ? ` (${log.email})` : ""}`],
    ["Vai trò", label(ROLES, log.role)],
    ["Hoạt động", label(ACTIONS, log.action)],
    ["Ảnh hưởng", label(IMPACTS, log.impact)],
    ["Đối tượng", `${log.entityType}${log.entityId ? ` #${log.entityId}` : ""}`],
    ["Sự kiện", log.projectCode ?? "—"],
    ["Địa chỉ IP", log.ipAddress ?? "—"],
  ];
  return (
    <Box sx={{ p: 3 }} id="auditDetail">
      <Stack direction="row" justifyContent="space-between" alignItems="flex-start" spacing={2}>
        <Typography variant="h6">{log.summary}</Typography>
        <IconButton onClick={onClose} aria-label="Đóng"><CloseIcon /></IconButton>
      </Stack>
      <Box sx={{ display: "grid", gridTemplateColumns: "140px 1fr", rowGap: 1, columnGap: 2, my: 2 }}>
        {facts.map(([k, v]) => [
          <Typography key={`${k}-k`} variant="body2" color="text.secondary">{k}</Typography>,
          <Typography key={`${k}-v`} variant="body2" sx={{ overflowWrap: "anywhere" }}>{v}</Typography>,
        ])}
      </Box>
      <Divider sx={{ mb: 2 }} />
      <Typography variant="subtitle1" fontWeight={600} gutterBottom>Giá trị thay đổi</Typography>
      {changes.length === 0 ? (
        <Typography variant="body2" color="text.secondary">Không có chi tiết giá trị (ví dụ: đăng nhập).</Typography>
      ) : (
        <Table size="small">
          <TableHead><TableRow><TableCell>Trường</TableCell><TableCell>Trước</TableCell><TableCell>Sau</TableCell></TableRow></TableHead>
          <TableBody>
            {changes.map(([field, v]) => (
              <TableRow key={field}>
                <TableCell sx={{ fontWeight: 600 }}>{FIELDS[field] ?? field}</TableCell>
                <TableCell sx={{ color: "error.main", overflowWrap: "anywhere" }}>{show(v?.old)}</TableCell>
                <TableCell sx={{ color: "success.main", overflowWrap: "anywhere" }}>{show(v?.new)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </Box>
  );
}
