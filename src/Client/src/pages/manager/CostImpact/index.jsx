import { useState } from "react";
import { Link as RouterLink, useNavigate, useParams } from "react-router-dom";
import { useDispatch } from "react-redux";
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  Divider,
  FormControl,
  Grid,
  InputLabel,
  MenuItem,
  Paper,
  Select,
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
import FactCheckOutlinedIcon from "@mui/icons-material/FactCheckOutlined";
import UploadFileOutlinedIcon from "@mui/icons-material/UploadFileOutlined";
import AssignmentLateOutlinedIcon from "@mui/icons-material/AssignmentLateOutlined";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import StatusChip from "../../../components/StatusChip";
import GenerateAddendumDialog from "./components/GenerateAddendumDialog";
import { useAsync } from "../../../hooks/useAsync";
import { projectService } from "../../../services/projectService";
import { notify } from "../../../store/notifySlice";
import { PATHS } from "../../../routes/paths";
import { dateTimeVi, dateVi, errorMessage, money, percent, roleLabel, signedMoney } from "../../../utils/format";

// M-09: Cost Impact & Addendum - Manager (UC-23, UC-24 | GBR-03, GBR-05 | Single transaction: addendum + balance + BEO status)
export default function CostImpact() {
  const { projectCode, crCode } = useParams();
  const navigate = useNavigate();
  const dispatch = useDispatch();
  const [dialogOpen, setDialogOpen] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  const { data, loading, error, reload, setData } = useAsync(async () => {
    const allCrs = await projectService.getChangeRequests(projectCode);
    const targetCode = crCode && crCode !== "latest" ? crCode : allCrs[0]?.crCode;
    const current = targetCode
      ? allCrs.find((c) => c.crCode === targetCode) || (await projectService.getCostImpact(projectCode, targetCode))
      : null;
    return { allCrs, current };
  }, [projectCode, crCode]);

  const cr = data?.current;
  const allCrs = data?.allCrs || [];

  const handleGenerateAddendum = async ({ note, file }) => {
    if (!cr) return;
    setSubmitting(true);
    try {
      let signedFileId = null;
      if (file) {
        const uploaded = await projectService.uploadFile(file, "SignedAddendum", projectCode);
        signedFileId = uploaded.fileId;
      }
      const updated = await projectService.generateAddendum(projectCode, cr.crCode, {
        signedFileId,
        note,
      });
      setData((prev) => ({
        ...prev,
        current: updated,
        allCrs: (prev?.allCrs || []).map((item) => (item.crCode === updated.crCode ? updated : item)),
      }));
      setDialogOpen(false);
      dispatch(
        notify({
          message: `Đã phê duyệt ${updated.crCode} và phát hành phụ lục ${updated.addendumCode} thành công.`,
          severity: "success",
        })
      );
    } catch (e) {
      dispatch(notify({ message: errorMessage(e), severity: "error" }));
    } finally {
      setSubmitting(false);
    }
  };

  const handleAttachSignedFile = async (e) => {
    const file = e.target.files?.[0];
    if (!file || !cr) return;
    setSubmitting(true);
    try {
      const uploaded = await projectService.uploadFile(file, "SignedAddendum", projectCode);
      const updated = await projectService.attachSignedAddendum(projectCode, cr.crCode, uploaded.fileId);
      setData((prev) => ({
        ...prev,
        current: updated,
        allCrs: (prev?.allCrs || []).map((item) => (item.crCode === updated.crCode ? updated : item)),
      }));
      dispatch(notify({ message: `Đã đính kèm bản ký cho phụ lục ${updated.addendumCode}.`, severity: "success" }));
    } catch (err) {
      dispatch(notify({ message: errorMessage(err), severity: "error" }));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <>
      <PageHeader
        code="M-09"
        title={`Đánh giá Tác động Chi phí & Phụ lục Hợp đồng (${cr?.crCode || crCode})`}
        subtitle="Tính toán chênh lệch hạng mục, phí điều chỉnh theo mốc T-day, cập nhật công nợ và đồng bộ trạng thái BEO trong một giao dịch."
        actions={
          <Stack direction="row" spacing={1.5} alignItems="center">
            {allCrs.length > 1 && (
              <FormControl size="small" sx={{ minWidth: 170 }}>
                <InputLabel>Chuyển yêu cầu CR</InputLabel>
                <Select
                  label="Chuyển yêu cầu CR"
                  value={cr?.crCode || ""}
                  onChange={(e) => navigate(PATHS.manager.costImpact(projectCode, e.target.value))}
                >
                  {allCrs.map((item) => (
                    <MenuItem key={item.crCode} value={item.crCode}>
                      {item.crCode} ({item.status})
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
            )}
            <Button startIcon={<ArrowBackIcon />} component={RouterLink} to={PATHS.manager.project(projectCode)}>
              Về Project Hub
            </Button>
            {cr && cr.status !== "Rejected" && !cr.addendumCode && (
              <Button
                variant="contained"
                startIcon={<FactCheckOutlinedIcon />}
                onClick={() => setDialogOpen(true)}
                id="openGenerateAddendumBtn"
              >
                Phê duyệt & Phát hành Phụ lục
              </Button>
            )}
            {cr?.addendumCode && cr.addendumStatus !== "Signed" && (
              <Button
                variant="contained"
                color="secondary"
                component="label"
                startIcon={<UploadFileOutlinedIcon />}
                disabled={submitting}
              >
                Tải lên Phụ lục đã ký
                <input type="file" hidden accept=".pdf,.jpg,.jpeg,.png" onChange={handleAttachSignedFile} />
              </Button>
            )}
          </Stack>
        }
      />

      <DataState
        loading={loading}
        error={error}
        onRetry={reload}
        empty={!cr}
        emptyText="Dự án chưa có yêu cầu thay đổi (CR) nào để tính tác động chi phí."
      >
        {cr && (
          <Stack spacing={3}>
            {cr.beoStatus === "Stale" && (
              <Alert
                severity="warning"
                icon={<AssignmentLateOutlinedIcon />}
                action={
                  <Button color="inherit" size="small" component={RouterLink} to={PATHS.manager.beo(projectCode)}>
                    Mở BEO (M-12)
                  </Button>
                }
              >
                BEO của dự án đang ở trạng thái <strong>Stale</strong> do thay đổi từ phụ lục hợp đồng (GBR-05). Vui lòng rà soát và duyệt lại BEO.
              </Alert>
            )}

            <Grid container spacing={2.5}>
              <Grid item xs={12} md={7}>
                <Card variant="outlined" sx={{ height: "100%" }}>
                  <CardContent>
                    <Stack direction="row" justifyContent="space-between" alignItems="center" mb={1.5}>
                      <Typography variant="subtitle1" fontWeight={800}>
                        Thông tin CR & Trạng thái Phụ lục
                      </Typography>
                      <Stack direction="row" spacing={1}>
                        <StatusChip kind="cr" status={cr.status} />
                        {cr.addendumStatus && <StatusChip kind="addendum" status={cr.addendumStatus} />}
                      </Stack>
                    </Stack>
                    <Divider sx={{ mb: 2 }} />
                    <Grid container spacing={2}>
                      <Grid item xs={6} sm={4}>
                        <Typography variant="caption" color="text.secondary">Mã CR</Typography>
                        <Typography fontWeight={700}>{cr.crCode}</Typography>
                      </Grid>
                      <Grid item xs={6} sm={4}>
                        <Typography variant="caption" color="text.secondary">Phân loại</Typography>
                        <Typography fontWeight={700}>{cr.type}</Typography>
                      </Grid>
                      <Grid item xs={6} sm={4}>
                        <Typography variant="caption" color="text.secondary">Khoảng cách ngày tiệc</Typography>
                        <Typography fontWeight={700}>
                          {cr.daysUntilEvent >= 0 ? `T-${cr.daysUntilEvent}` : "Đã qua ngày tiệc"} ({dateVi(cr.eventDate)})
                        </Typography>
                      </Grid>
                      <Grid item xs={12}>
                        <Typography variant="caption" color="text.secondary">Mô tả yêu cầu</Typography>
                        <Typography sx={{ mt: 0.5 }}>{cr.description}</Typography>
                      </Grid>
                      <Grid item xs={6} sm={4}>
                        <Typography variant="caption" color="text.secondary">Khách hàng phê duyệt</Typography>
                        <Typography fontWeight={600} color={cr.customerApprovedAt ? "success.main" : "warning.dark"}>
                          {cr.customerApprovedAt ? dateTimeVi(cr.customerApprovedAt) : "Chưa duyệt"}
                        </Typography>
                      </Grid>
                      <Grid item xs={6} sm={4}>
                        <Typography variant="caption" color="text.secondary">Manager phê duyệt</Typography>
                        <Typography fontWeight={600} color={cr.managerApprovedAt ? "success.main" : "warning.dark"}>
                          {cr.managerApprovedAt ? dateTimeVi(cr.managerApprovedAt) : "Chưa duyệt"}
                        </Typography>
                      </Grid>
                      <Grid item xs={12} sm={4}>
                        <Typography variant="caption" color="text.secondary">Mã phụ lục phát hành</Typography>
                        <Typography fontWeight={800} color="primary.main">
                          {cr.addendumCode ? `${cr.addendumCode} (${cr.addendumStatus})` : "Chưa phát hành"}
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
                      Bảng tính Tác động Tài chính & Công nợ
                    </Typography>
                    <Divider sx={{ mb: 2 }} />
                    <Stack spacing={1.1}>
                      <Stack direction="row" justifyContent="space-between">
                        <Typography color="text.secondary">Chênh lệch giá trị dịch vụ (Cost Impact):</Typography>
                        <Typography fontWeight={700}>{signedMoney(cr.costImpact)}</Typography>
                      </Stack>
                      <Stack direction="row" justifyContent="space-between">
                        <Typography color="text.secondary">Phí điều chỉnh cận ngày ({percent(cr.penaltyRate)}):</Typography>
                        <Typography fontWeight={700} color={cr.penaltyFee > 0 ? "warning.dark" : "text.primary"}>
                          {money(cr.penaltyFee)}
                        </Typography>
                      </Stack>
                      <Divider />
                      <Stack direction="row" justifyContent="space-between" alignItems="center">
                        <Typography fontWeight={800}>Tổng biến động phụ lục (Total Variance):</Typography>
                        <Typography variant="h6" fontWeight={800} color="primary.main">
                          {signedMoney(cr.totalVariance)}
                        </Typography>
                      </Stack>
                      <Divider />
                      <Stack direction="row" justifyContent="space-between">
                        <Typography variant="body2" color="text.secondary">Hợp đồng gốc (Baseline):</Typography>
                        <Typography variant="body2" fontWeight={600}>{money(cr.baseContractValue)}</Typography>
                      </Stack>
                      <Stack direction="row" justifyContent="space-between">
                        <Typography variant="body2" color="text.secondary">Tổng hợp đồng sau phụ lục:</Typography>
                        <Typography variant="body2" fontWeight={800}>{money(cr.projectedProjectTotal)}</Typography>
                      </Stack>
                      <Stack direction="row" justifyContent="space-between">
                        <Typography variant="body2" color="text.secondary">Đã thu từ khách hàng:</Typography>
                        <Typography variant="body2" fontWeight={700} color="success.main">
                          {money(cr.paidAmount)}
                        </Typography>
                      </Stack>
                      <Stack direction="row" justifyContent="space-between">
                        <Typography variant="body2" color="text.secondary">Số dư công nợ sau phụ lục:</Typography>
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
                Bảng đối chiếu hạng mục thay đổi (CR Items)
              </Typography>
              <TableContainer>
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>Hạng mục</TableCell>
                      <TableCell>Danh mục</TableCell>
                      <TableCell align="right">SL hiện tại</TableCell>
                      <TableCell align="right">Đơn giá cũ</TableCell>
                      <TableCell align="right">SL điều chỉnh</TableCell>
                      <TableCell align="right">Đơn giá mới</TableCell>
                      <TableCell align="right">Chênh lệch thành tiền</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {cr.items?.map((item) => (
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
                        <TableCell align="right" sx={{ fontWeight: 800, color: "primary.main" }}>
                          {signedMoney(item.deltaAmount)}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </TableContainer>
            </Paper>

            <Paper variant="outlined" sx={{ p: 2.5 }}>
              <Typography variant="subtitle1" fontWeight={800} mb={1.5}>
                Lịch sử thẩm định & thương lượng CR
              </Typography>
              <Stack spacing={1.2}>
                {cr.history?.map((h) => (
                  <Box
                    key={h.historyId}
                    sx={{ p: 1.5, borderRadius: 1.5, bgcolor: "grey.50", border: "1px solid", borderColor: "divider" }}
                  >
                    <Stack direction="row" justifyContent="space-between" alignItems="center">
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
                      <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
                        {h.note}
                      </Typography>
                    )}
                  </Box>
                ))}
              </Stack>
            </Paper>
          </Stack>
        )}
      </DataState>

      <GenerateAddendumDialog
        open={dialogOpen}
        cr={cr}
        submitting={submitting}
        onClose={() => setDialogOpen(false)}
        onConfirm={handleGenerateAddendum}
      />
    </>
  );
}
