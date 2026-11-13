import { Box, Card, CardContent, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from "@mui/material";
import PageHeader from "../PageHeader";
import DataState from "../DataState";
import ReportFilter from "./ReportFilter";
import { useReportRange } from "./useReportRange";
import { useAsync } from "../../hooks/useAsync";
import { adminService } from "../../services/adminService";

// Frame of A-09..A-12: filter, then `render(report)` for the screen's own content.
export default function ReportLayout({ code, title, render }) {
  const range = useReportRange();
  const { data, loading, error, reload } = useAsync(
    () => (range.valid ? adminService.getReport(range.from, range.to) : Promise.reject(new Error("Ngày bắt đầu phải trước hoặc bằng ngày kết thúc."))),
    [range.from, range.to],
  );
  return (
    <>
      <PageHeader code={code} title={title} />
      <ReportFilter key={range.query} range={range} />
      <DataState loading={loading} error={error} onRetry={reload}>{data && render(data)}</DataState>
    </>
  );
}

// Chart and its data table side by side; the table is the accessible view of the chart.
export function ChartWithTable({ chart, columns, rows, chartTitle }) {
  return (
    <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { lg: "1fr 1fr" } }}>
      <Card>
        <CardContent>
          <Typography variant="h6" gutterBottom>{chartTitle}</Typography>
          {chart}
        </CardContent>
      </Card>
      <Card>
        <TableContainer>
          <Table size="small">
            <TableHead>
              <TableRow>{columns.map((c) => <TableCell key={c.label} align={c.align}>{c.label}</TableCell>)}</TableRow>
            </TableHead>
            <TableBody>
              {rows.length === 0 ? (
                <TableRow><TableCell colSpan={columns.length} align="center">Không có dữ liệu.</TableCell></TableRow>
              ) : rows.map((row, i) => (
                <TableRow key={i}>{columns.map((c) => <TableCell key={c.label} align={c.align}>{c.value(row)}</TableCell>)}</TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      </Card>
    </Box>
  );
}
