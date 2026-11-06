import { useState } from "react";
import { useParams, Link as RouterLink } from "react-router-dom";
import { useDispatch } from "react-redux";
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  CircularProgress,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from "@mui/material";
import DownloadIcon from "@mui/icons-material/Download";
import AddCardIcon from "@mui/icons-material/AddCard";
import ReceiptLongIcon from "@mui/icons-material/ReceiptLong";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import StatusChip from "../../../components/StatusChip";
import RecordPaymentDialog from "./components/RecordPaymentDialog";
import { useAsync } from "../../../hooks/useAsync";
import { projectService } from "../../../services/projectService";
import { saveBlobAsFile } from "../../../services/paymentService";
import { notify } from "../../../store/notifySlice";
import { dateVi, money, signedMoney, errorMessage } from "../../../utils/format";

// M-13: Payments & Settlement (Manager)
// Manages contract balance breakdown, milestone payment tracking, payment history, and links to M-14 and M-15.
export default function ManagerPayments() {
  const { projectCode } = useParams();
  const dispatch = useDispatch();

  const { data, loading, error, reload, setData } = useAsync(
    () => projectService.getManagerPayments(projectCode),
    [projectCode]
  );

  const [recordDialogOpen, setRecordDialogOpen] = useState(false);
  const [downloadingType, setDownloadingType] = useState(null);

  const handleDownloadReceipt = async (paymentType) => {
    try {
      setDownloadingType(paymentType);
      let blob;
      if (paymentType === "FINAL" && data?.summary?.isSettled) {
        blob = await projectService.downloadSettlementReceipt(projectCode);
      } else {
        blob = await projectService.downloadPaymentReceipt(projectCode, paymentType);
      }
      saveBlobAsFile(blob, `Receipt_${projectCode}_${paymentType}.pdf`);
      dispatch(notify({ message: `Đã tải xuống chứng từ đợt ${paymentType}.`, severity: "success" }));
    } catch (err) {
      dispatch(notify({ message: errorMessage(err), severity: "error" }));
    } finally {
      setDownloadingType(null);
    }
  };

  const handleRecordSuccess = (result) => {
    dispatch(notify({ message: result.message, severity: "success" }));
    if (result.overview) {
      setData(result.overview);
    } else {
      reload();
    }
  };

  const summary = data?.summary;
  const milestones = data?.milestones ?? [];
  const history = data?.history ?? [];
  const isSettled = summary?.isSettled ?? false;

  return (
    <Box>
      <PageHeader
        code="M-13"
        title="Thanh toán & Quyết toán"
        subtitle={
          summary
            ? `${summary.CoupleName} — Sảnh ${summary.HallName} (${dateVi(summary.EventDate)})`
            : undefined
        }
        actions={
          <Stack direction="row" spacing={1.5}>
            <Button
              variant="contained"
              startIcon={<AddCardIcon />}
              onClick={() => setRecordDialogOpen(true)}
              disabled={isSettled}
            >
              Ghi nhận thanh toán (M-14)
            </Button>
            <Button
              variant="outlined"
              color="primary"
              startIcon={<ReceiptLongIcon />}
              component={RouterLink}
              to={`/manager/projects/${encodeURIComponent(projectCode)}/payments/settlement-receipt`}
            >
              Quyết toán hợp đồng (M-15)
            </Button>
          </Stack>
        }
      />

      <DataState loading={loading} error={error} onRetry={reload}>
        {data && (
          <>
            {isSettled && (
              <Alert severity="success" sx={{ mb: 2 }}>
                Dự án này đã hoàn tất quyết toán hợp đồng (Settled). Toàn bộ nghĩa vụ tài chính đã hoàn tất.
              </Alert>
            )}

            {/* 1. Project Financial Summary */}
            <Box
              sx={{
                display: "grid",
                gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)", md: "repeat(3, 1fr)", lg: "repeat(5, 1fr)" },
                gap: 2,
                mb: 3,
              }}
            >
              <Card>
                <CardContent>
                  <Typography variant="body2" color="text.secondary">Giá trị hợp đồng gốc</Typography>
                  <Typography variant="h5" sx={{ fontWeight: 700, mt: 0.5 }}>{money(summary.BaseContractValue)}</Typography>
                  <Typography variant="caption" color="text.secondary">Theo Contract Baseline</Typography>
                </CardContent>
              </Card>

              <Card>
                <CardContent>
                  <Typography variant="body2" color="text.secondary">Chênh lệch CR đã duyệt</Typography>
                  <Typography
                    variant="h5"
                    sx={{
                      fontWeight: 700,
                      mt: 0.5,
                      color: summary.TotalApprovedCrVariance > 0 ? "warning.main" : summary.TotalApprovedCrVariance < 0 ? "info.main" : "text.secondary",
                    }}
                  >
                    {signedMoney(summary.TotalApprovedCrVariance)}
                  </Typography>
                  <Typography variant="caption" color="text.secondary">
                    {summary.TotalApprovedCrVariance !== 0 ? "Tổng chi phí phát sinh" : "Chưa có phát sinh"}
                  </Typography>
                </CardContent>
              </Card>

              <Card>
                <CardContent>
                  <Typography variant="body2" color="text.secondary">Tổng giá trị hiện tại</Typography>
                  <Typography variant="h5" sx={{ fontWeight: 700, mt: 0.5, color: "primary.main" }}>
                    {money(summary.CurrentProjectTotal)}
                  </Typography>
                  <Typography variant="caption" color="text.secondary">Giá trị gốc + CR được duyệt</Typography>
                </CardContent>
              </Card>

              <Card>
                <CardContent>
                  <Typography variant="body2" color="text.secondary">Tổng đã thanh toán</Typography>
                  <Typography variant="h5" sx={{ fontWeight: 700, mt: 0.5, color: "success.main" }}>
                    {money(summary.TotalAmountPaid)}
                  </Typography>
                  <Typography variant="caption" color="text.secondary">Tổng các khoản đã thu</Typography>
                </CardContent>
              </Card>

              <Card
                sx={{
                  border: "1px solid",
                  borderColor: summary.OutstandingBalance > 0 ? "error.light" : "success.light",
                  bgcolor: summary.OutstandingBalance > 0 ? "action.hover" : undefined,
                }}
              >
                <CardContent>
                  <Typography variant="body2" color="text.secondary">Còn phải thu</Typography>
                  <Typography
                    variant="h5"
                    sx={{
                      fontWeight: 700,
                      mt: 0.5,
                      color: summary.OutstandingBalance > 0 ? "error.main" : "success.main",
                    }}
                  >
                    {money(summary.OutstandingBalance)}
                  </Typography>
                  <Typography variant="caption" color="text.secondary">
                    {summary.OutstandingBalance === 0 ? "Đã thu đủ toàn bộ" : "Dư nợ cần thu"}
                  </Typography>
                </CardContent>
              </Card>
            </Box>

            {/* 2. Payment Milestones Table */}
            <Typography variant="h6" sx={{ mb: 1.5, fontWeight: 600 }}>
              Tiến độ thanh toán theo mốc (Milestones)
            </Typography>
            <Card sx={{ mb: 3 }}>
              <TableContainer>
                <Table>
                  <TableHead>
                    <TableRow>
                      <TableCell>Đợt thanh toán</TableCell>
                      <TableCell>Hạn thanh toán</TableCell>
                      <TableCell align="right">Số tiền kế hoạch</TableCell>
                      <TableCell align="right">Đã thu</TableCell>
                      <TableCell>Trạng thái</TableCell>
                      <TableCell>Phương thức</TableCell>
                      <TableCell>Ngày thu</TableCell>
                      <TableCell>Người ghi nhận</TableCell>
                      <TableCell align="center">Chứng từ / Biên nhận</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {milestones.map((m) => {
                      const isDownloading = downloadingType === m.PaymentType;
                      return (
                        <TableRow key={m.PaymentType} hover>
                          <TableCell>
                            <Stack spacing={0.25}>
                              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                                {m.MilestoneName}
                              </Typography>
                              <Typography variant="caption" color="text.secondary">
                                Mã: {m.PaymentType}
                              </Typography>
                            </Stack>
                          </TableCell>
                          <TableCell>{dateVi(m.DueDate)}</TableCell>
                          <TableCell align="right" sx={{ fontWeight: 600 }}>
                            {money(m.Amount)}
                          </TableCell>
                          <TableCell align="right">
                            {m.PaidAmount > 0 ? (
                              <Typography variant="body2" color="success.main" sx={{ fontWeight: 600 }}>
                                {money(m.PaidAmount)}
                              </Typography>
                            ) : (
                              "—"
                            )}
                          </TableCell>
                          <TableCell>
                            <StatusChip kind="payment" status={m.Status} />
                          </TableCell>
                          <TableCell>{m.PaymentMethod || "—"}</TableCell>
                          <TableCell>{dateVi(m.PaidDate)}</TableCell>
                          <TableCell>{m.RecordedByName || "—"}</TableCell>
                          <TableCell align="center">
                            {m.ReceiptAvailable ? (
                              <Button
                                size="small"
                                variant="outlined"
                                startIcon={isDownloading ? <CircularProgress size={14} color="inherit" /> : <DownloadIcon />}
                                disabled={isDownloading}
                                onClick={() => handleDownloadReceipt(m.PaymentType)}
                              >
                                Tải chứng từ
                              </Button>
                            ) : (
                              <Typography variant="caption" color="text.secondary">
                                {m.Status === "Paid" ? "Chưa có file" : "Chưa phát hành"}
                              </Typography>
                            )}
                          </TableCell>
                        </TableRow>
                      );
                    })}
                  </TableBody>
                </Table>
              </TableContainer>
            </Card>

            {/* 3. Payment History */}
            <Typography variant="h6" sx={{ mb: 1.5, fontWeight: 600 }}>
              Lịch sử ghi nhận thanh toán (Audit History)
            </Typography>
            <Card>
              <TableContainer>
                <Table>
                  <TableHead>
                    <TableRow>
                      <TableCell>Đợt</TableCell>
                      <TableCell align="right">Số tiền</TableCell>
                      <TableCell>Ngày thu</TableCell>
                      <TableCell>Phương thức</TableCell>
                      <TableCell>Trạng thái</TableCell>
                      <TableCell>Người ghi nhận</TableCell>
                      <TableCell align="center">Chứng từ</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {history.length === 0 ? (
                      <TableRow>
                        <TableCell colSpan={7} align="center">
                          Chưa có bản ghi thanh toán nào.
                        </TableCell>
                      </TableRow>
                    ) : (
                      history.map((h) => {
                        const isDownloading = downloadingType === h.PaymentType;
                        return (
                          <TableRow key={h.PaymentId} hover>
                            <TableCell sx={{ fontWeight: 500 }}>{h.PaymentType}</TableCell>
                            <TableCell align="right" sx={{ fontWeight: 600 }}>{money(h.Amount)}</TableCell>
                            <TableCell>{dateVi(h.PaymentDate)}</TableCell>
                            <TableCell>{h.Method || "—"}</TableCell>
                            <TableCell><StatusChip kind="payment" status={h.Status} /></TableCell>
                            <TableCell>{h.RecordedByName || "—"}</TableCell>
                            <TableCell align="center">
                              {h.ReceiptAvailable ? (
                                <Button
                                  size="small"
                                  startIcon={isDownloading ? <CircularProgress size={14} color="inherit" /> : <DownloadIcon />}
                                  disabled={isDownloading}
                                  onClick={() => handleDownloadReceipt(h.PaymentType)}
                                >
                                  Tải file
                                </Button>
                              ) : (
                                "—"
                              )}
                            </TableCell>
                          </TableRow>
                        );
                      })
                    )}
                  </TableBody>
                </Table>
              </TableContainer>
            </Card>

            {/* Record Payment Dialog (M-14) */}
            <RecordPaymentDialog
              open={recordDialogOpen}
              onClose={() => setRecordDialogOpen(false)}
              projectCode={projectCode}
              summary={summary}
              onSuccess={handleRecordSuccess}
            />
          </>
        )}
      </DataState>
    </Box>
  );
}
