import { useState } from "react";
import { Alert, Box, Button, Card, CardContent, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from "@mui/material";
import DownloadIcon from "@mui/icons-material/Download";
import DataState from "../../../../components/DataState";
import StatusChip from "../../../../components/StatusChip";
import InvoiceCard from "../../../../components/InvoiceCard";
import { useAsync } from "../../../../hooks/useAsync";
import { projectService } from "../../../../services/projectService";
import { saveBlobAsFile } from "../../../../services/paymentService";
import { dateVi, errorMessage, money } from "../../../../utils/format";

const METHOD = { Cash: "Tiền mặt", Bank: "Chuyển khoản" };

// Deposit and settlement with receipts (same figures as the Manager screen) and the final invoice; Admin may download every receipt.
export default function PaymentsTab({ projectCode }) {
  const { data, loading, error, reload } = useAsync(() => projectService.getManagerPayments(projectCode), [projectCode]);
  const [downloadError, setDownloadError] = useState("");

  const download = async (m) => {
    setDownloadError("");
    try {
      const settlement = m.paymentType === "Settlement" && data.summary.isSettled;
      const blob = settlement
        ? await projectService.downloadSettlementReceipt(projectCode)
        : await projectService.downloadPaymentReceipt(projectCode, m.paymentType);
      saveBlobAsFile(blob, `${settlement ? "Bien_nhan_tat_toan" : `Bien_nhan_${m.paymentType}`}_${projectCode}.pdf`);
    } catch (e) {
      setDownloadError(errorMessage(e));
    }
  };

  return (
    <DataState loading={loading} error={error} onRetry={reload}>
      {data && (
        <Box sx={{ display: "grid", gap: 2 }}>
          {downloadError && <Alert severity="error" onClose={() => setDownloadError("")}>{downloadError}</Alert>}
          <Card>
            <CardContent sx={{ pb: 1 }}>
              <Typography variant="h6">Hai lần thanh toán</Typography>
              <Typography variant="body2" color="text.secondary">
                Tổng hiện tại {money(data.summary.currentProjectTotal)} · đã thu {money(data.summary.totalAmountPaid)} · còn lại {money(data.summary.outstandingBalance)}
              </Typography>
            </CardContent>
            <TableContainer>
              <Table size="small" id="adminPayments">
                <TableHead><TableRow>
                  <TableCell>Đợt</TableCell><TableCell align="right">Số tiền</TableCell><TableCell>Hạn</TableCell>
                  <TableCell align="center">Trạng thái</TableCell><TableCell>Ngày thu</TableCell><TableCell>Hình thức</TableCell>
                  <TableCell>Người ghi nhận</TableCell><TableCell align="right">Hóa đơn / biên nhận</TableCell>
                </TableRow></TableHead>
                <TableBody>
                  {data.milestones.map((m) => (
                    <TableRow key={m.paymentType}>
                      <TableCell>{m.milestoneName}</TableCell>
                      <TableCell align="right">{money(m.amount)}</TableCell>
                      <TableCell>{dateVi(m.dueDate)}</TableCell>
                      <TableCell align="center"><StatusChip kind="payment" status={m.status} /></TableCell>
                      <TableCell>{m.paidDate ? dateVi(m.paidDate) : "—"}</TableCell>
                      <TableCell>{METHOD[m.paymentMethod] ?? (m.status === "Paid" ? m.paymentMethod : "—")}</TableCell>
                      <TableCell>{m.recordedByName ?? "—"}</TableCell>
                      <TableCell align="right">
                        {m.receiptAvailable
                          ? <Button size="small" startIcon={<DownloadIcon />} onClick={() => download(m)}>Tải</Button>
                          : <Typography variant="caption" color="text.secondary">Chưa có</Typography>}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          </Card>
          <InvoiceCard invoice={data.invoice} />
        </Box>
      )}
    </DataState>
  );
}
