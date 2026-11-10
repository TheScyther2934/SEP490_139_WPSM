import { useState } from "react";
import { Link as RouterLink, useParams } from "react-router-dom";
import { useDispatch } from "react-redux";
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  Divider,
  Grid,
  Paper,
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
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutlined";
import DescriptionOutlinedIcon from "@mui/icons-material/DescriptionOutlined";
import GavelOutlinedIcon from "@mui/icons-material/GavelOutlined";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import StatusChip from "../../../components/StatusChip";
import CustomerCrDecisionDialog from "./components/CustomerCrDecisionDialog";
import { useAsync } from "../../../hooks/useAsync";
import { customerService } from "../../../services/customerService";
import { notify } from "../../../store/notifySlice";
import { PATHS } from "../../../routes/paths";
import { dateTimeVi, dateVi, errorMessage, money, percent, roleLabel, signedMoney } from "../../../utils/format";

// C-07, C-08: Customer CR Detail, Cost Impact & Approve/Reject (UC-09, UC-10 | GBR-03, GBR-04, GBR-13 | MSG20)
export default function CustomerChangeRequestDetail() {
  const { crCode } = useParams();
  const dispatch = useDispatch();
  const [dialogOpen, setDialogOpen] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  const { data: cr, loading, error, reload, setData } = useAsync(
    () => customerService.getChangeRequest(crCode),
    [crCode]
  );

  const handleDecision = async (action, note) => {
    setSubmitting(true);
    try {
      const updated = await customerService.decideChangeRequest(crCode, action, note);
      setData(updated);
      setDialogOpen(false);
      dispatch(
        notify({
          message:
            action === "Approve"
              ? `Đã phê duyệt tác động chi phí cho ${crCode}.`
              : action === "Reject"
              ? `Đã từ chối yêu cầu thay đổi ${crCode}.`
              : `Đã gửi yêu cầu điều chỉnh phương án cho ${crCode}.`,
          severity: "success",
        })
      );
    } catch (e) {
      dispatch(notify({ message: errorMessage(e), severity: "error" }));
    } finally {
      setSubmitting(false);
    }
  };

  // Per SRS §4.3 & GBR-04: Customer only confirms (C-08) when Manager has sent a quote/counter-proposal (managerApprovedAt != null && !customerApprovedAt)
  const waitingForCustomer =
    cr && cr.status === "Negotiating" && Boolean(cr.managerApprovedAt) && !cr.customerApprovedAt;
  const waitingForManager =
    cr && cr.status === "Negotiating" && Boolean(cr.customerApprovedAt) && !cr.managerApprovedAt;

  return (
    <>
      <PageHeader
        code="C-07"
        title={`Chi tiết yêu cầu thay đổi & Tác động chi phí (${crCode})`}
        subtitle="Xem bảng tính chênh lệch chi phí, phí điều chỉnh theo mốc T-day và lịch sử thương lượng với Quản lý dự án."
        actions={
          <Stack direction="row" spacing={1.5}>
            <Button startIcon={<ArrowBackIcon />} component={RouterLink} to={PATHS.customer.changeRequests}>
              Danh sách CR
            </Button>
            {cr?.addendum?.code && (
              <Button
                variant="outlined"
                startIcon={<DescriptionOutlinedIcon />}
                component={RouterLink}
                to={PATHS.customer.addenda}
              >
                Xem phụ lục ({cr.addendum.code})
              </Button>
            )}
            {waitingForCustomer && (
              <Button
                variant="contained"
                startIcon={<GavelOutlinedIcon />}
                onClick={() => setDialogOpen(true)}
                id="openCrDecisionBtn"
              >
                Xác nhận báo giá chi phí (C-08)
              </Button>
            )}
          </Stack>
        }
      />

      <DataState loading={loading} error={error} onRetry={reload} empty={!cr} emptyText="Không tìm thấy yêu cầu thay đổi.">
        {cr && (
          <Stack spacing={3}>
            {waitingForManager && (
              <Alert severity="info" icon={<CheckCircleOutlineIcon />}>
                Yêu cầu thay đổi của bạn (gửi lúc <strong>{dateTimeVi(cr.customerApprovedAt)}</strong>) đang chờ{" "}
                <strong>Quản lý dự án (Manager)</strong> thẩm định năng lực đáp ứng và phê duyệt.
              </Alert>
            )}
            {waitingForCustomer && (
              <Alert severity="warning">
                Quản lý dự án đã thẩm định và gửi bảng tính tác động chi phí vào lúc{" "}
                <strong>{dateTimeVi(cr.managerApprovedAt)}</strong>. Theo quy tắc <strong>GBR-04 (Đồng thuận 2 bên)</strong>, vui lòng kiểm tra chi phí phát sinh bên dưới và bấm{" "}
                <strong>Xác nhận báo giá chi phí (C-08)</strong> nếu bạn đồng ý với mức phí này.
              </Alert>
            )}

            <Grid container spacing={2.5}>
              <Grid item xs={12} md={7}>
                <Card variant="outlined" sx={{ height: "100%" }}>
                  <CardContent>
                    <Stack direction="row" justifyContent="space-between" alignItems="center" mb={1.5}>
                      <Typography variant="subtitle1" fontWeight={800}>
                        Thông tin yêu cầu thay đổi
                      </Typography>
                      <StatusChip kind="cr" status={cr.status} />
                    </Stack>
                    <Divider sx={{ mb: 2 }} />
                    <Grid container spacing={2}>
                      <Grid item xs={6} sm={4}>
                        <Typography variant="caption" color="text.secondary">Mã yêu cầu</Typography>
                        <Typography fontWeight={700}>{cr.crCode}</Typography>
                      </Grid>
                      <Grid item xs={6} sm={4}>
                        <Typography variant="caption" color="text.secondary">Loại thay đổi</Typography>
                        <Typography fontWeight={700}>{cr.type}</Typography>
                      </Grid>
                      <Grid item xs={6} sm={4}>
                        <Typography variant="caption" color="text.secondary">Mốc thời gian</Typography>
                        <Typography fontWeight={700}>
                          {cr.daysUntilEvent >= 0 ? `T-${cr.daysUntilEvent}` : "Đã qua ngày tiệc"} ({dateVi(cr.eventDate)})
                        </Typography>
                      </Grid>
                      <Grid item xs={12}>
                        <Typography variant="caption" color="text.secondary">Nội dung đề xuất</Typography>
                        <Typography sx={{ mt: 0.5 }}>{cr.description}</Typography>
                      </Grid>
                      <Grid item xs={6}>
                        <Typography variant="caption" color="text.secondary">Xác nhận của Khách hàng</Typography>
                        <Typography fontWeight={600} color={cr.customerApprovedAt ? "success.main" : "text.secondary"}>
                          {cr.customerApprovedAt ? `Đã duyệt (${dateTimeVi(cr.customerApprovedAt)})` : "Chưa xác nhận"}
                        </Typography>
                      </Grid>
                      <Grid item xs={6}>
                        <Typography variant="caption" color="text.secondary">Xác nhận của Quản lý</Typography>
                        <Typography fontWeight={600} color={cr.managerApprovedAt ? "success.main" : "text.secondary"}>
                          {cr.managerApprovedAt ? `Đã duyệt (${dateTimeVi(cr.managerApprovedAt)})` : "Chưa xác nhận"}
                        </Typography>
                      </Grid>
                    </Grid>
                  </CardContent>
                </Card>
              </Grid>

              <Grid item xs={12} md={5}>
                <Card variant="outlined" sx={{ height: "100%", bgcolor: "grey.50" }}>
                  <CardContent>
                    <Typography variant="subtitle1" fontWeight={800} mb={1.5}>
                      Tổng hợp tác động chi phí (GBR-03)
                    </Typography>
                    <Divider sx={{ mb: 2 }} />
                    <Stack spacing={1.2}>
                      <Stack direction="row" justifyContent="space-between">
                        <Typography color="text.secondary">Chênh lệch hạng mục (Δ giá trị):</Typography>
                        <Typography fontWeight={700}>{signedMoney(cr.costImpact)}</Typography>
                      </Stack>
                      <Stack direction="row" justifyContent="space-between">
                        <Typography color="text.secondary">
                          Phí điều chỉnh ({percent(cr.penaltyRate)} theo chính sách T-day):
                        </Typography>
                        <Typography fontWeight={700} color={cr.penaltyFee > 0 ? "warning.dark" : "text.primary"}>
                          {money(cr.penaltyFee)}
                        </Typography>
                      </Stack>
                      <Divider />
                      <Stack direction="row" justifyContent="space-between" alignItems="center">
                        <Typography fontWeight={800}>Tổng tác động CR (Total Variance):</Typography>
                        <Typography variant="h6" fontWeight={800} color={cr.totalVariance >= 0 ? "primary.main" : "success.main"}>
                          {signedMoney(cr.totalVariance)}
                        </Typography>
                      </Stack>
                      <Divider />
                      <Stack direction="row" justifyContent="space-between">
                        <Typography variant="body2" color="text.secondary">Tổng hợp đồng hiện tại:</Typography>
                        <Typography variant="body2" fontWeight={600}>{money(cr.currentProjectTotal)}</Typography>
                      </Stack>
                      <Stack direction="row" justifyContent="space-between">
                        <Typography variant="body2" color="text.secondary">Tổng hợp đồng sau CR:</Typography>
                        <Typography variant="body2" fontWeight={800}>{money(cr.projectedProjectTotal)}</Typography>
                      </Stack>
                      <Stack direction="row" justifyContent="space-between">
                        <Typography variant="body2" color="text.secondary">Công nợ còn lại dự kiến:</Typography>
                        <Typography variant="body2" fontWeight={800} color="error.main">
                          {money(cr.projectedBalance)}
                        </Typography>
                      </Stack>
                    </Stack>
                  </CardContent>
                </Card>
              </Grid>
            </Grid>

            <Paper variant="outlined" sx={{ p: 2.5 }}>
              <Typography variant="subtitle1" fontWeight={800} mb={1.5}>
                Chi tiết hạng mục thay đổi (Trước & Sau CR)
              </Typography>
              <TableContainer>
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>Hạng mục dịch vụ</TableCell>
                      <TableCell>Nhóm</TableCell>
                      <TableCell align="right">SL cũ</TableCell>
                      <TableCell align="right">Đơn giá cũ</TableCell>
                      <TableCell align="right">SL mới</TableCell>
                      <TableCell align="right">Đơn giá mới</TableCell>
                      <TableCell align="right">Chênh lệch (Δ)</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {cr.items?.length ? (
                      cr.items.map((item) => (
                        <TableRow key={item.crItemId} hover>
                          <TableCell sx={{ fontWeight: 700 }}>{item.itemName}</TableCell>
                          <TableCell>{item.category}</TableCell>
                          <TableCell align="right">
                            {item.oldQuantity} {item.unit}
                          </TableCell>
                          <TableCell align="right">{money(item.oldUnitPrice)}</TableCell>
                          <TableCell align="right" sx={{ fontWeight: 700 }}>
                            {item.newQuantity} {item.unit}
                          </TableCell>
                          <TableCell align="right" sx={{ fontWeight: 700 }}>
                            {money(item.newUnitPrice)}
                          </TableCell>
                          <TableCell
                            align="right"
                            sx={{
                              fontWeight: 800,
                              color: item.deltaAmount >= 0 ? "primary.main" : "success.main",
                            }}
                          >
                            {signedMoney(item.deltaAmount)}
                          </TableCell>
                        </TableRow>
                      ))
                    ) : (
                      <TableRow>
                        <TableCell colSpan={7} align="center">
                          Yêu cầu thay đổi tổng hợp (Chênh lệch: {signedMoney(cr.costImpact)})
                        </TableCell>
                      </TableRow>
                    )}
                  </TableBody>
                </Table>
              </TableContainer>
            </Paper>

            <Paper variant="outlined" sx={{ p: 2.5 }}>
              <Typography variant="subtitle1" fontWeight={800} mb={1.5}>
                Nhật ký thương lượng & phê duyệt (GBR-13 · Append-only)
              </Typography>
              <Stack spacing={1.5}>
                {cr.history?.map((h) => (
                  <Box
                    key={h.historyId}
                    sx={{
                      p: 1.5,
                      borderRadius: 1.5,
                      bgcolor: "grey.50",
                      border: "1px solid",
                      borderColor: "divider",
                    }}
                  >
                    <Stack direction="row" justifyContent="space-between" alignItems="center" flexWrap="wrap" gap={1}>
                      <Stack direction="row" spacing={1} alignItems="center">
                        <Typography fontWeight={700} variant="body2">
                          {h.action}
                        </Typography>
                        <Chip size="small" label={`${h.actorName} (${roleLabel(h.actorRole)})`} variant="outlined" />
                      </Stack>
                      <Typography variant="caption" color="text.secondary">
                        {dateTimeVi(h.createdAt)}
                      </Typography>
                    </Stack>
                    {h.note && (
                      <Typography variant="body2" color="text.secondary" sx={{ mt: 0.75 }}>
                        Ghi chú: {h.note}
                      </Typography>
                    )}
                  </Box>
                ))}
              </Stack>
            </Paper>
          </Stack>
        )}
      </DataState>

      <CustomerCrDecisionDialog
        open={dialogOpen}
        cr={cr}
        submitting={submitting}
        onClose={() => setDialogOpen(false)}
        onSubmit={handleDecision}
      />
    </>
  );
}
