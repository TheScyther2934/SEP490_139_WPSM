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
  Divider,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import PrintIcon from "@mui/icons-material/Print";
import DownloadIcon from "@mui/icons-material/Download";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import ReceiptLongIcon from "@mui/icons-material/ReceiptLong";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import { useAsync } from "../../../hooks/useAsync";
import { projectService } from "../../../services/projectService";
import { saveBlobAsFile } from "../../../services/paymentService";
import { notify } from "../../../store/notifySlice";
import { dateVi, dateTimeVi, money, signedMoney, errorMessage } from "../../../utils/format";

// M-15: Final Settlement Receipt
// Computes final settlement, issues official settlement receipt, and renders printable/downloadable receipt.
export default function SettlementReceipt() {
  const { projectCode } = useParams();
  const dispatch = useDispatch();

  const { data: paymentsData, loading, error, reload, setData } = useAsync(
    () => projectService.getManagerPayments(projectCode),
    [projectCode]
  );

  const [generating, setGenerating] = useState(false);
  const [downloading, setDownloading] = useState(false);

  const summary = paymentsData?.summary;
  const settlement = paymentsData?.settlement;
  const history = paymentsData?.history ?? [];

  const handleGenerateReceipt = async () => {
    try {
      setGenerating(true);
      await projectService.generateReceipt(projectCode);
      dispatch(notify({ message: "Đã phát hành biên nhận quyết toán thành công.", severity: "success" }));
      reload();
    } catch (err) {
      dispatch(notify({ message: errorMessage(err), severity: "error" }));
    } finally {
      setGenerating(false);
    }
  };

  const handleDownloadPdf = async () => {
    try {
      setDownloading(true);
      const blob = await projectService.downloadSettlementReceipt(projectCode);
      saveBlobAsFile(blob, `Bien_nhan_quyet_toan_${projectCode}.pdf`);
      dispatch(notify({ message: "Đã tải xuống file PDF biên nhận quyết toán.", severity: "success" }));
    } catch (err) {
      dispatch(notify({ message: errorMessage(err), severity: "error" }));
    } finally {
      setDownloading(false);
    }
  };

  const handlePrint = () => {
    window.print();
  };

  const isEventCompleted = settlement?.IsEventCompleted ?? false;
  const isSettled = settlement?.IsSettled ?? false;
  const canSettle = settlement?.CanSettle ?? false;
  const finalAmountDue = settlement?.FinalAmountDue ?? 0;

  return (
    <Box>
      <Box sx={{ "@media print": { display: "none" } }}>
        <PageHeader
          code="M-15"
          title="Quyết toán hợp đồng & Biên nhận"
          subtitle={summary ? `Dự án: ${summary.ProjectCode} — ${summary.CoupleName}` : undefined}
          actions={
            <Stack direction="row" spacing={1.5}>
              <Button
                variant="outlined"
                startIcon={<ArrowBackIcon />}
                component={RouterLink}
                to={`/manager/projects/${encodeURIComponent(projectCode)}/payments`}
              >
                Về danh sách thanh toán
              </Button>
              {isSettled && (
                <>
                  <Button
                    variant="outlined"
                    startIcon={<PrintIcon />}
                    onClick={handlePrint}
                  >
                    In biên nhận
                  </Button>
                  <Button
                    variant="contained"
                    startIcon={downloading ? <CircularProgress size={16} color="inherit" /> : <DownloadIcon />}
                    disabled={downloading}
                    onClick={handleDownloadPdf}
                  >
                    Tải PDF
                  </Button>
                </>
              )}
            </Stack>
          }
        />
      </Box>

      <DataState loading={loading} error={error} onRetry={reload}>
        {settlement && (
          <Box>
            {/* Settlement Status Banners (hidden on print) */}
            <Box sx={{ mb: 3, "@media print": { display: "none" } }}>
              {!isEventCompleted && (
                <Alert severity="warning" sx={{ mb: 2 }}>
                  Sự kiện cưới chưa diễn ra hoặc chưa hoàn tất (Event Date: {dateVi(summary?.EventDate)}).
                  Theo quy định hệ thống, quyết toán hợp đồng chỉ được thực hiện sau khi sự kiện cưới hoàn tất.
                </Alert>
              )}

              {isEventCompleted && finalAmountDue > 0 && (
                <Alert
                  severity="info"
                  action={
                    <Button
                      color="inherit"
                      size="small"
                      component={RouterLink}
                      to={`/manager/projects/${encodeURIComponent(projectCode)}/payments`}
                    >
                      Thu Final Payment (M-13)
                    </Button>
                  }
                  sx={{ mb: 2 }}
                >
                  Dự án còn số dư chưa thanh toán ({money(finalAmountDue)}). Cần thu đủ số dư trước khi phát hành biên nhận quyết toán.
                </Alert>
              )}

              {canSettle && !isSettled && (
                <Alert
                  severity="success"
                  icon={<CheckCircleIcon fontSize="inherit" />}
                  action={
                    <Button
                      variant="contained"
                      color="success"
                      size="small"
                      disabled={generating}
                      startIcon={generating ? <CircularProgress size={14} color="inherit" /> : <ReceiptLongIcon />}
                      onClick={handleGenerateReceipt}
                    >
                      {generating ? "Đang phát hành..." : "Phát hành biên nhận quyết toán"}
                    </Button>
                  }
                  sx={{ mb: 2 }}
                >
                  Sự kiện đã hoàn tất và số dư đã đối soát = 0 đ. Dự án đủ điều kiện quyết toán chính thức!
                </Alert>
              )}
            </Box>

            {/* 1. Settlement Calculation Table (hidden on print) */}
            <Box sx={{ mb: 4, "@media print": { display: "none" } }}>
              <Typography variant="h6" sx={{ mb: 1.5, fontWeight: 600 }}>
                Bảng đối soát quyết toán (Settlement Calculation)
              </Typography>
              <Card>
                <TableContainer>
                  <Table>
                    <TableHead>
                      <TableRow>
                        <TableCell>Hạng mục tài chính</TableCell>
                        <TableCell align="right">Số tiền (VND)</TableCell>
                        <TableCell>Ghi chú</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      <TableRow>
                        <TableCell>Giá trị hợp đồng gốc (Base Contract Value)</TableCell>
                        <TableCell align="right" sx={{ fontWeight: 600 }}>
                          {money(settlement.BaseContractValue)}
                        </TableCell>
                        <TableCell color="text.secondary">Giá trị cố định từ Contract Baseline</TableCell>
                      </TableRow>
                      <TableRow>
                        <TableCell>Chênh lệch CR đã duyệt (Approved CR Variance)</TableCell>
                        <TableCell
                          align="right"
                          sx={{
                            fontWeight: 600,
                            color: settlement.ApprovedCrVariance > 0 ? "warning.main" : settlement.ApprovedCrVariance < 0 ? "info.main" : "text.secondary",
                          }}
                        >
                          {signedMoney(settlement.ApprovedCrVariance)}
                        </TableCell>
                        <TableCell color="text.secondary">Tổng phát sinh từ các CR trạng thái Approved</TableCell>
                      </TableRow>
                      <TableRow sx={{ bgcolor: "action.hover" }}>
                        <TableCell sx={{ fontWeight: 700 }}>Tổng giá trị dự án hiện tại (Current Project Total)</TableCell>
                        <TableCell align="right" sx={{ fontWeight: 700, color: "primary.main" }}>
                          {money(settlement.CurrentProjectTotal)}
                        </TableCell>
                        <TableCell color="text.secondary">Giá trị gốc + Chênh lệch CR</TableCell>
                      </TableRow>
                      <TableRow>
                        <TableCell>Đặt cọc NRD đã thu (NRD Paid)</TableCell>
                        <TableCell align="right" sx={{ fontWeight: 600, color: "success.main" }}>
                          - {money(settlement.NrdPaid)}
                        </TableCell>
                        <TableCell color="text.secondary">Thu tại bước khởi tạo dự án</TableCell>
                      </TableRow>
                      <TableRow>
                        <TableCell>Đợt hai T-30 đã thu (T-30 Paid)</TableCell>
                        <TableCell align="right" sx={{ fontWeight: 600, color: "success.main" }}>
                          - {money(settlement.T30Paid)}
                        </TableCell>
                        <TableCell color="text.secondary">Thu tại mốc T-30 ngày trước tiệc</TableCell>
                      </TableRow>
                      {settlement.OtherRecordedPayments > 0 && (
                        <TableRow>
                          <TableCell>Các khoản đã thu khác / Final Payment</TableCell>
                          <TableCell align="right" sx={{ fontWeight: 600, color: "success.main" }}>
                            - {money(settlement.OtherRecordedPayments)}
                          </TableCell>
                          <TableCell color="text.secondary">Các đợt ghi nhận thanh toán bổ sung</TableCell>
                        </TableRow>
                      )}
                      <TableRow sx={{ bgcolor: settlement.FinalAmountDue > 0 ? "error.light" : "success.light" }}>
                        <TableCell sx={{ fontWeight: 800 }}>Số tiền quyết toán còn lại (Final Amount Due)</TableCell>
                        <TableCell
                          align="right"
                          sx={{
                            fontWeight: 800,
                            fontSize: "1.1rem",
                            color: settlement.FinalAmountDue > 0 ? "error.main" : "success.dark",
                          }}
                        >
                          {money(settlement.FinalAmountDue)}
                        </TableCell>
                        <TableCell sx={{ fontWeight: 600 }}>
                          {settlement.FinalAmountDue === 0 ? "ĐÃ ĐỐI SOÁT XONG (0 VND)" : "CẦN THANH TOÁN TIẾP"}
                        </TableCell>
                      </TableRow>
                    </TableBody>
                  </Table>
                </TableContainer>
              </Card>
            </Box>

            {/* 2. Official Final Settlement Receipt Preview (Always visible, styled for print) */}
            <Box sx={{ mt: 3 }}>
              <Typography variant="h6" sx={{ mb: 1.5, fontWeight: 600, "@media print": { display: "none" } }}>
                Mẫu biên nhận quyết toán chính thức (Preview)
              </Typography>

              <Card
                sx={{
                  p: 4,
                  maxWidth: 860,
                  mx: "auto",
                  border: "1px solid",
                  borderColor: "divider",
                  boxShadow: 2,
                  bgcolor: "#fff",
                  color: "#000",
                  "@media print": {
                    boxShadow: "none",
                    border: "none",
                    p: 0,
                    maxWidth: "100%",
                  },
                }}
              >
                {/* Header */}
                <Stack direction="row" justifyContent="space-between" alignItems="flex-start" sx={{ mb: 2 }}>
                  <Box>
                    <Typography variant="h5" sx={{ fontWeight: 700, letterSpacing: 0.5, color: "#1a237e" }}>
                      WPSM WEDDING CENTER
                    </Typography>
                    <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                      Hệ thống Trung tâm Tiệc cưới & Sự kiện Cao cấp WPSM
                    </Typography>
                  </Box>
                  <Box sx={{ textAlign: "right" }}>
                    <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                      Số biên nhận: <strong>FSR-{projectCode}</strong>
                    </Typography>
                    <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                      Ngày phát hành: {dateTimeVi(settlement.CalculatedAt)}
                    </Typography>
                  </Box>
                </Stack>

                <Divider sx={{ my: 2 }} />

                <Box sx={{ textAlign: "center", my: 2 }}>
                  <Typography variant="h5" sx={{ fontWeight: 800, textTransform: "uppercase" }}>
                    BIÊN NHẬN QUYẾT TOÁN HỢP ĐỒNG
                  </Typography>
                  <Typography variant="subtitle2" sx={{ color: "text.secondary" }}>
                    FINAL SETTLEMENT RECEIPT
                  </Typography>
                </Box>

                {/* Customer info */}
                <Box sx={{ my: 3, p: 2, bgcolor: "action.hover", borderRadius: 1 }}>
                  <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1, textTransform: "uppercase" }}>
                    Thông tin dự án & Khách hàng
                  </Typography>
                  <Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" spacing={2}>
                    <Box>
                      <Typography variant="body2">Cặp đôi: <strong>{summary?.CoupleName}</strong></Typography>
                      <Typography variant="body2">Mã dự án: <strong>{projectCode}</strong></Typography>
                    </Box>
                    <Box>
                      <Typography variant="body2">Ngày cưới: <strong>{dateVi(summary?.EventDate)}</strong></Typography>
                      <Typography variant="body2">Sảnh tiệc: <strong>{summary?.HallName}</strong> ({summary?.SlotName})</Typography>
                    </Box>
                  </Stack>
                </Box>

                {/* Itemized summary */}
                <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1, textTransform: "uppercase" }}>
                  Tổng kết quyết toán tài chính
                </Typography>
                <Table size="small" sx={{ mb: 3 }}>
                  <TableBody>
                    <TableRow>
                      <TableCell sx={{ pl: 0 }}>1. Giá trị hợp đồng gốc (Base Contract Value)</TableCell>
                      <TableCell align="right" sx={{ fontWeight: 600 }}>{money(settlement.BaseContractValue)}</TableCell>
                    </TableRow>
                    <TableRow>
                      <TableCell sx={{ pl: 0 }}>2. Tổng chênh lệch Change Requests đã phê duyệt</TableCell>
                      <TableCell align="right" sx={{ fontWeight: 600 }}>{signedMoney(settlement.ApprovedCrVariance)}</TableCell>
                    </TableRow>
                    <TableRow sx={{ borderTop: "2px solid #ccc" }}>
                      <TableCell sx={{ pl: 0, fontWeight: 700 }}>Tổng giá trị quyết toán thực tế</TableCell>
                      <TableCell align="right" sx={{ fontWeight: 700, color: "primary.main" }}>{money(settlement.CurrentProjectTotal)}</TableCell>
                    </TableRow>
                    <TableRow>
                      <TableCell sx={{ pl: 0 }}>3. Tổng số tiền đã thanh toán (Recorded Payments)</TableCell>
                      <TableCell align="right" sx={{ fontWeight: 600, color: "success.main" }}>{money(settlement.TotalRecordedPayments)}</TableCell>
                    </TableRow>
                    <TableRow sx={{ borderTop: "2px solid #333", bgcolor: "#f8f9fa" }}>
                      <TableCell sx={{ pl: 0, fontWeight: 800 }}>4. Dư nợ còn lại (Final Outstanding Balance)</TableCell>
                      <TableCell align="right" sx={{ fontWeight: 800, color: settlement.FinalAmountDue === 0 ? "success.dark" : "error.main" }}>
                        {money(settlement.FinalAmountDue)} {settlement.FinalAmountDue === 0 ? "(HOÀN TẤT)" : ""}
                      </TableCell>
                    </TableRow>
                  </TableBody>
                </Table>

                {/* Payment history list */}
                <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1, textTransform: "uppercase" }}>
                  Chi tiết các đợt đã thanh toán
                </Typography>
                <Table size="small" sx={{ mb: 4 }}>
                  <TableHead>
                    <TableRow>
                      <TableCell sx={{ pl: 0 }}>Đợt</TableCell>
                      <TableCell>Ngày thu</TableCell>
                      <TableCell>Phương thức</TableCell>
                      <TableCell align="right">Số tiền</TableCell>
                      <TableCell>Người ghi nhận</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {history.map((h, i) => (
                      <TableRow key={i}>
                        <TableCell sx={{ pl: 0 }}>{h.PaymentType}</TableCell>
                        <TableCell>{dateVi(h.PaymentDate)}</TableCell>
                        <TableCell>{h.Method || "—"}</TableCell>
                        <TableCell align="right" sx={{ fontWeight: 600 }}>{money(h.Amount)}</TableCell>
                        <TableCell>{h.RecordedByName || "Ban Quản Lý"}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>

                {/* Signatures */}
                <Stack direction="row" justifyContent="space-between" sx={{ mt: 5, pt: 2 }}>
                  <Box sx={{ textAlign: "center", width: 220 }}>
                    <Typography variant="body2" sx={{ fontWeight: 700 }}>Đại diện Khách hàng</Typography>
                    <Typography variant="caption" color="text.secondary">(Ký và ghi rõ họ tên)</Typography>
                    <Box sx={{ height: 60 }} />
                    <Typography variant="body2">{summary?.CoupleName}</Typography>
                  </Box>
                  <Box sx={{ textAlign: "center", width: 220 }}>
                    <Typography variant="body2" sx={{ fontWeight: 700 }}>Đại diện Ban Điều Phối WPSM</Typography>
                    <Typography variant="caption" color="text.secondary">(Ký và đóng dấu)</Typography>
                    <Box sx={{ height: 60 }} />
                    <Typography variant="body2">Quản lý sự kiện</Typography>
                  </Box>
                </Stack>
              </Card>
            </Box>
          </Box>
        )}
      </DataState>
    </Box>
  );
}
