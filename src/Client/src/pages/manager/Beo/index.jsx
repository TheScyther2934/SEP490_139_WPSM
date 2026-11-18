import { useEffect, useState } from "react";
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
  TextField,
  Typography,
} from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import LockOutlinedIcon from "@mui/icons-material/LockOutlined";
import AutorenewIcon from "@mui/icons-material/Autorenew";
import PictureAsPdfOutlinedIcon from "@mui/icons-material/PictureAsPdfOutlined";
import SaveOutlinedIcon from "@mui/icons-material/SaveOutlined";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import StatusChip from "../../../components/StatusChip";
import ReapproveBeoDialog from "./components/ReapproveBeoDialog";
import { useAsync } from "../../../hooks/useAsync";
import { projectService } from "../../../services/projectService";
import { notify } from "../../../store/notifySlice";
import { PATHS } from "../../../routes/paths";
import { dateTimeVi, dateVi, errorMessage, money, slotLabel } from "../../../utils/format";

// M-10, M-12: Master BEO & Re-approve Stale BEO (UC-27, UC-29 | GBR-05 | MSG18/22 | BF-04)
export default function Beo() {
  const { projectCode } = useParams();
  const dispatch = useDispatch();
  const { data: beo, loading, error, reload, setData } = useAsync(
    () => projectService.getBeo(projectCode),
    [projectCode]
  );

  const [notes, setNotes] = useState("");
  const [saving, setSaving] = useState(false);
  const [dialogOpen, setDialogOpen] = useState(false);

  useEffect(() => {
    if (beo) setNotes(beo.notes || "");
  }, [beo]);

  const handleSaveDraft = async () => {
    setSaving(true);
    try {
      const updated = await projectService.saveDraftBeo(projectCode, notes);
      setData(updated);
      dispatch(notify({ message: "Đã lưu ghi chú bản thảo BEO.", severity: "success" }));
    } catch (e) {
      dispatch(notify({ message: errorMessage(e), severity: "error" }));
    } finally {
      setSaving(false);
    }
  };

  const handleFreezeOrReapprove = async (dialogNotes) => {
    setSaving(true);
    try {
      const updated = await projectService.freezeBeo(projectCode, dialogNotes || notes);
      setData(updated);
      setDialogOpen(false);
      dispatch(
        notify({
          message: `Đã phê duyệt và đóng băng BEO v${updated.version} đồng thời cập nhật 4 phiếu bộ phận (MSG18).`,
          severity: "success",
        })
      );
    } catch (e) {
      dispatch(notify({ message: errorMessage(e), severity: "error" }));
    } finally {
      setSaving(false);
    }
  };

  return (
    <>
      <PageHeader
        code={beo?.status === "Stale" ? "M-12" : "M-10"}
        title={`Hồ sơ Điều phối Vận hành BEO (${projectCode})`}
        subtitle="Tổng hợp đặc tả hợp đồng gốc và toàn bộ phụ lục đã duyệt thành kế hoạch vận hành chuẩn cho 4 bộ phận."
        actions={
          <Stack direction="row" spacing={1.5}>
            <Button startIcon={<ArrowBackIcon />} component={RouterLink} to={PATHS.manager.project(projectCode)}>
              Về Project Hub
            </Button>
            <Button
              variant="outlined"
              startIcon={<PictureAsPdfOutlinedIcon />}
              component={RouterLink}
              to={PATHS.manager.departmentSheets(projectCode)}
            >
              Phiếu 4 bộ phận (M-11)
            </Button>
            {beo && (
              <Button
                variant="contained"
                color={beo.status === "Stale" ? "warning" : "primary"}
                startIcon={beo.status === "Stale" ? <AutorenewIcon /> : <LockOutlinedIcon />}
                onClick={() => setDialogOpen(true)}
                disabled={saving}
                id="openFreezeBeoBtn"
              >
                {beo.status === "Stale"
                  ? `Duyệt lại BEO Stale (M-12)`
                  : beo.status === "Frozen"
                  ? `Cập nhật phiên bản BEO mới`
                  : `Đóng băng BEO (M-10)`}
              </Button>
            )}
          </Stack>
        }
      />

      <DataState loading={loading} error={error} onRetry={reload} empty={!beo} emptyText="Không tìm thấy dữ liệu BEO.">
        {beo && (
          <Stack spacing={3}>
            {beo.status === "Stale" && (
              <Alert
                severity="warning"
                action={
                  <Button color="inherit" size="small" onClick={() => setDialogOpen(true)}>
                    Duyệt lại ngay (M-12)
                  </Button>
                }
              >
                <strong>Cảnh báo MSG22 / GBR-05:</strong> BEO v{beo.version} đã chuyển sang trạng thái <strong>Stale</strong> do có phụ lục hợp đồng mới được phê duyệt sau khi đóng băng. Vui lòng kiểm tra các hạng mục thay đổi và duyệt lại BEO để phát hành phiên bản v{beo.version + 1}.
              </Alert>
            )}

            <Grid container spacing={2.5}>
              <Grid item xs={12} md={8}>
                <Card variant="outlined" sx={{ height: "100%" }}>
                  <CardContent>
                    <Stack direction="row" justifyContent="space-between" alignItems="center" mb={1.5}>
                      <Typography variant="subtitle1" fontWeight={800}>
                        Thông tin BEO & Sự kiện
                      </Typography>
                      <Stack direction="row" spacing={1} alignItems="center">
                        <Chip size="small" label={`Phiên bản v${beo.version}`} color="primary" variant="outlined" />
                        <StatusChip kind="beo" status={beo.status} />
                      </Stack>
                    </Stack>
                    <Divider sx={{ mb: 2 }} />
                    <Grid container spacing={2}>
                      <Grid item xs={6} sm={4}>
                        <Typography variant="caption" color="text.secondary">Cặp đôi khách hàng</Typography>
                        <Typography fontWeight={700}>{beo.coupleName}</Typography>
                      </Grid>
                      <Grid item xs={6} sm={4}>
                        <Typography variant="caption" color="text.secondary">Sảnh & Ca tiệc</Typography>
                        <Typography fontWeight={700}>
                          {beo.hallName} · Ca {slotLabel(beo.slotName)} ({beo.slotTime})
                        </Typography>
                      </Grid>
                      <Grid item xs={6} sm={4}>
                        <Typography variant="caption" color="text.secondary">Ngày tổ chức</Typography>
                        <Typography fontWeight={700}>
                          {dateVi(beo.eventDate)} ({beo.daysUntilEvent >= 0 ? `T-${beo.daysUntilEvent}` : "Đã diễn ra"})
                        </Typography>
                      </Grid>
                      <Grid item xs={6} sm={4}>
                        <Typography variant="caption" color="text.secondary">Số bàn / Quy mô</Typography>
                        <Typography fontWeight={700}>{beo.guestCount} bàn</Typography>
                      </Grid>
                      <Grid item xs={6} sm={4}>
                        <Typography variant="caption" color="text.secondary">Phụ lục CR đã tích hợp</Typography>
                        <Typography fontWeight={700} color="primary.main">
                          {beo.approvedCrCount} yêu cầu thay đổi
                        </Typography>
                      </Grid>
                      <Grid item xs={6} sm={4}>
                        <Typography variant="caption" color="text.secondary">Lần đóng băng gần nhất</Typography>
                        <Typography fontWeight={600}>
                          {beo.frozenAt ? `${dateTimeVi(beo.frozenAt)} (${beo.approvedByName || "Manager"})` : "Chưa đóng băng"}
                        </Typography>
                      </Grid>
                    </Grid>
                  </CardContent>
                </Card>
              </Grid>

              <Grid item xs={12} md={4}>
                <Card variant="outlined" sx={{ height: "100%" }}>
                  <CardContent>
                    <Typography variant="subtitle1" fontWeight={800} mb={1.5}>
                      Ghi chú chỉ đạo vận hành (M-10)
                    </Typography>
                    <Stack spacing={1.5}>
                      <TextField
                        multiline
                        minRows={3}
                        value={notes}
                        onChange={(e) => setNotes(e.target.value)}
                        placeholder="Nhập ghi chú điều phối chung cho 4 bộ phận..."
                        size="small"
                        fullWidth
                      />
                      <Button
                        variant="outlined"
                        startIcon={<SaveOutlinedIcon />}
                        onClick={handleSaveDraft}
                        disabled={saving}
                        sx={{ alignSelf: "flex-end" }}
                      >
                        Lưu ghi chú BEO
                      </Button>
                    </Stack>
                  </CardContent>
                </Card>
              </Grid>
            </Grid>

            <Paper variant="outlined" sx={{ p: 2.5 }}>
              <Stack direction="row" justifyContent="space-between" alignItems="center" mb={1.5}>
                <Typography variant="subtitle1" fontWeight={800}>
                  Danh mục Hạng mục Vận hành Tổng hợp (Baseline + Phụ lục CR)
                </Typography>
                <Typography variant="body2" fontWeight={700} color="primary.main">
                  Tổng giá trị hiệu lực: {money(beo.currentTotal)}
                </Typography>
              </Stack>
              <TableContainer>
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>Bộ phận phụ trách</TableCell>
                      <TableCell>Hạng mục dịch vụ</TableCell>
                      <TableCell>Chi tiết thực hiện</TableCell>
                      <TableCell align="right">SL Baseline</TableCell>
                      <TableCell align="right">SL Hiệu lực (BEO)</TableCell>
                      <TableCell align="right">Đơn giá</TableCell>
                      <TableCell align="right">Thành tiền</TableCell>
                      <TableCell align="center">Nguồn</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {beo.items?.map((item) => (
                      <TableRow key={item.specItemId} hover>
                        <TableCell>
                          <Chip size="small" label={item.department} variant="outlined" />
                        </TableCell>
                        <TableCell sx={{ fontWeight: 700 }}>{item.itemName}</TableCell>
                        <TableCell>{item.description}</TableCell>
                        <TableCell align="right">
                          {item.baselineQuantity} {item.unit}
                        </TableCell>
                        <TableCell align="right" sx={{ fontWeight: 800 }}>
                          {item.effectiveQuantity} {item.unit}
                        </TableCell>
                        <TableCell align="right">{money(item.unitPrice)}</TableCell>
                        <TableCell align="right" sx={{ fontWeight: 700 }}>
                          {money(item.lineTotal)}
                        </TableCell>
                        <TableCell align="center">
                          <Chip
                            size="small"
                            color={item.changedByCr ? "warning" : "default"}
                            label={item.changedByCr ? "Cập nhật từ CR" : "Baseline gốc"}
                          />
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </TableContainer>
            </Paper>

            <Paper variant="outlined" sx={{ p: 2.5 }}>
              <Typography variant="subtitle1" fontWeight={800} mb={1.5}>
                Lịch sử các phiên bản BEO của dự án
              </Typography>
              <Stack spacing={1.2}>
                {beo.versionHistory?.length ? (
                  beo.versionHistory.map((v) => (
                    <Box
                      key={v.beoId}
                      sx={{ p: 1.5, borderRadius: 1.5, bgcolor: "grey.50", border: "1px solid", borderColor: "divider" }}
                    >
                      <Stack direction="row" justifyContent="space-between" alignItems="center">
                        <Stack direction="row" spacing={1} alignItems="center">
                          <Typography fontWeight={800}>BEO Phiên bản v{v.version}</Typography>
                          <StatusChip kind="beo" status={v.status} />
                        </Stack>
                        <Typography variant="caption" color="text.secondary">
                          {v.frozenAt ? `Đóng băng: ${dateTimeVi(v.frozenAt)} bởi ${v.approvedByName || "Manager"}` : "Bản thảo"}
                        </Typography>
                      </Stack>
                      {v.notes && (
                        <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
                          Ghi chú: {v.notes}
                        </Typography>
                      )}
                    </Box>
                  ))
                ) : (
                  <Typography variant="body2" color="text.secondary">
                    Chưa có phiên bản BEO nào được lưu trong cơ sở dữ liệu.
                  </Typography>
                )}
              </Stack>
            </Paper>
          </Stack>
        )}
      </DataState>

      <ReapproveBeoDialog
        open={dialogOpen}
        beo={beo}
        initialNotes={notes}
        submitting={saving}
        onClose={() => setDialogOpen(false)}
        onConfirm={handleFreezeOrReapprove}
      />
    </>
  );
}
