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
import { useProject } from "../../../hooks/useProject";
import { projectService } from "../../../services/projectService";
import { notify } from "../../../store/notifySlice";
import { PATHS } from "../../../routes/paths";
import { dateTimeVi, dateVi, errorMessage, money, slotLabel } from "../../../utils/format";

// Coordination sheet: freeze it, and re-approve it after a confirmed addon contract.| GBR-05 | MSG18/22 | BF-04)
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
  const { project } = useProject();
  // The sheet is frozen only while the event is locked (same check as the API). Addon contracts waiting for the customer do not block.
  const blocker = project.status === "Locked" ? null : project.status === "Adjusting" ? "adjusting" : "closed";
  const newLines = beo?.items?.filter((i) => i.isNew).length || 0;
  const addonLines = beo?.items?.filter((i) => i.changedByAddon).length || 0;

  useEffect(() => {
    if (beo) setNotes(beo.notes || "");
  }, [beo]);

  const handleSaveDraft = async () => {
    setSaving(true);
    try {
      const updated = await projectService.saveDraftBeo(projectCode, notes);
      setData(updated);
      dispatch(notify({ message: "Đã lưu ghi chú bản thảo phiếu điều phối.", severity: "success" }));
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
          message: `Đã phê duyệt và chốt phiếu điều phối v${updated.version} đồng thời cập nhật 4 phiếu bộ phận.`,
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
        title={`Phiếu điều phối tiệc (${projectCode})`}
        subtitle="Tổng hợp dịch vụ đã chốt và các hợp đồng phát sinh khách đã xác nhận thành kế hoạch cho 4 bộ phận."
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
              Phiếu 4 bộ phận
            </Button>
            {beo && (
              <Button
                variant="contained"
                color={beo.status === "Stale" ? "warning" : "primary"}
                startIcon={beo.status === "Stale" ? <AutorenewIcon /> : <LockOutlinedIcon />}
                onClick={() => setDialogOpen(true)}
                disabled={saving || !!blocker}
                id="openFreezeBeoBtn"
              >
                {beo.status === "Stale"
                  ? `Duyệt lại phiếu điều phối`
                  : beo.status === "Frozen"
                  ? `Cập nhật phiên bản phiếu điều phối mới`
                  : `Chốt phiếu điều phối`}
              </Button>
            )}
          </Stack>
        }
      />

      <DataState loading={loading} error={error} onRetry={reload} empty={!beo} emptyText="Không tìm thấy dữ liệu phiếu điều phối.">
        {beo && (
          <Stack spacing={3}>
            {!blocker && project.pendingAddons > 0 && (
              <Alert severity="info"
                action={<Button color="inherit" size="small" component={RouterLink} to={PATHS.manager.addons(projectCode)}>Xem hợp đồng phát sinh</Button>}>
                Còn {project.pendingAddons} hợp đồng phát sinh đang chờ khách xác nhận. Phiếu điều phối vẫn chốt được bình thường; hợp đồng đó chỉ
                được đưa vào phiếu khi khách xác nhận, lúc đó phiếu sẽ cần duyệt lại thêm một lần.
              </Alert>
            )}
            {blocker === "adjusting" && (
              <Alert severity="info">Sự kiện còn trong giai đoạn điều chỉnh. Phiếu điều phối được chốt sau khi khóa dịch vụ ({dateVi(project.lockDate)}).</Alert>
            )}
            {blocker === "closed" && (
              <Alert severity="info">Sự kiện đã diễn ra, đã tất toán hoặc đã hủy. Phiếu điều phối chỉ còn để xem.</Alert>
            )}
            {beo.status === "Stale" && !blocker && (
              <Alert
                severity="warning"
                action={
                  <Button color="inherit" size="small" onClick={() => setDialogOpen(true)}>
                    Duyệt lại ngay
                  </Button>
                }
              >
                <strong>Cần duyệt lại:</strong> khách đã xác nhận hợp đồng phát sinh sau khi chốt phiếu v{beo.version}{newLines ? `, có ${newLines} dòng dịch vụ mới (đánh dấu "Mới" bên dưới)` : ""}. Các bộ phận chưa nhận được phần này. Hãy kiểm tra và duyệt lại để phát hành phiếu v{beo.version + 1}.
              </Alert>
            )}

            <Grid container spacing={2.5}>
              <Grid size={{ xs: 12, md: 8 }}>
                <Card variant="outlined" sx={{ height: "100%" }}>
                  <CardContent>
                    <Stack direction="row" justifyContent="space-between" alignItems="center" mb={1.5}>
                      <Typography variant="subtitle1" fontWeight={800}>
                        Thông tin phiếu điều phối
                      </Typography>
                      <Stack direction="row" spacing={1} alignItems="center">
                        <Chip size="small" label={`Phiên bản v${beo.version}`} color="primary" variant="outlined" />
                        <StatusChip kind="beo" status={beo.status} />
                      </Stack>
                    </Stack>
                    <Divider sx={{ mb: 2 }} />
                    <Grid container spacing={2}>
                      <Grid size={{ xs: 6, sm: 4 }}>
                        <Typography variant="caption" color="text.secondary">Cặp đôi khách hàng</Typography>
                        <Typography fontWeight={700}>{beo.coupleName}</Typography>
                      </Grid>
                      <Grid size={{ xs: 6, sm: 4 }}>
                        <Typography variant="caption" color="text.secondary">Sảnh & Ca tiệc</Typography>
                        <Typography fontWeight={700}>
                          {beo.hall} · Ca {slotLabel(beo.slot)}
                        </Typography>
                      </Grid>
                      <Grid size={{ xs: 6, sm: 4 }}>
                        <Typography variant="caption" color="text.secondary">Ngày tổ chức</Typography>
                        <Typography fontWeight={700}>
                          {dateVi(beo.eventDate)} ({beo.daysUntilEvent >= 0 ? `T-${beo.daysUntilEvent}` : "Đã diễn ra"})
                        </Typography>
                      </Grid>
                      <Grid size={{ xs: 6, sm: 4 }}>
                        <Typography variant="caption" color="text.secondary">Số bàn / Quy mô</Typography>
                        <Typography fontWeight={700}>{beo.guestCount} bàn</Typography>
                      </Grid>
                      <Grid size={{ xs: 6, sm: 4 }}>
                        <Typography variant="caption" color="text.secondary">Phát sinh đã tích hợp</Typography>
                        <Typography fontWeight={700} color="primary.main">
                          {addonLines} dòng dịch vụ
                        </Typography>
                      </Grid>
                      <Grid size={{ xs: 6, sm: 4 }}>
                        <Typography variant="caption" color="text.secondary">Lần chốt gần nhất</Typography>
                        <Typography fontWeight={600}>
                          {beo.frozenAt ? `${dateTimeVi(beo.frozenAt)} (${beo.approvedByName || "Manager"})` : "Chưa chốt"}
                        </Typography>
                      </Grid>
                    </Grid>
                  </CardContent>
                </Card>
              </Grid>

              <Grid size={{ xs: 12, md: 4 }}>
                <Card variant="outlined" sx={{ height: "100%" }}>
                  <CardContent>
                    <Typography variant="subtitle1" fontWeight={800} mb={1.5}>
                      Ghi chú điều phối
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
                        Lưu ghi chú
                      </Button>
                    </Stack>
                  </CardContent>
                </Card>
              </Grid>
            </Grid>

            <Paper variant="outlined" sx={{ p: 2.5 }}>
              <Stack direction="row" justifyContent="space-between" alignItems="center" mb={1.5}>
                <Typography variant="subtitle1" fontWeight={800}>
                  Dịch vụ cần thực hiện (hợp đồng và phát sinh đã xác nhận)
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
                      <TableCell align="right">SL theo hợp đồng</TableCell>
                      <TableCell align="right">SL thực hiện</TableCell>
                      <TableCell align="right">Đơn giá</TableCell>
                      <TableCell align="right">Thành tiền</TableCell>
                      <TableCell align="center">Nguồn</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {beo.items?.map((item) => (
                      <TableRow key={item.specItemId} hover sx={item.isNew ? { bgcolor: "rgba(237, 108, 2, 0.06)" } : undefined}>
                        <TableCell>
                          <Chip size="small" label={item.department} variant="outlined" />
                        </TableCell>
                        <TableCell sx={{ fontWeight: 700 }}>{item.name}</TableCell>
                        <TableCell>{item.description}</TableCell>
                        <TableCell align="right">
                          {item.contractQuantity} {item.unit}
                        </TableCell>
                        <TableCell align="right" sx={{ fontWeight: 800 }}>
                          {item.effectiveQuantity} {item.unit}
                        </TableCell>
                        <TableCell align="right">{money(item.unitPrice)}</TableCell>
                        <TableCell align="right" sx={{ fontWeight: 700 }}>
                          {money(item.lineTotal)}
                        </TableCell>
                        <TableCell align="center">
                          <Stack direction="row" spacing={0.5} justifyContent="center">
                            <Chip
                              size="small"
                              color={item.changedByAddon ? "warning" : "default"}
                              label={item.changedByAddon ? "Từ hợp đồng phát sinh" : "Theo hợp đồng"}
                            />
                            {item.isNew && <Chip size="small" color="error" label="Mới" />}
                          </Stack>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </TableContainer>
            </Paper>

            <Paper variant="outlined" sx={{ p: 2.5 }}>
              <Typography variant="subtitle1" fontWeight={800} mb={1.5}>
                Lịch sử các phiên bản phiếu điều phối của sự kiện
              </Typography>
              <Stack spacing={1.2}>
                {beo.history?.length ? (
                  beo.history.map((v) => (
                    <Box
                      key={v.beoId}
                      sx={{ p: 1.5, borderRadius: 1.5, bgcolor: "grey.50", border: "1px solid", borderColor: "divider" }}
                    >
                      <Stack direction="row" justifyContent="space-between" alignItems="center">
                        <Stack direction="row" spacing={1} alignItems="center">
                          <Typography fontWeight={800}>Phiếu điều phối v{v.version}</Typography>
                          <StatusChip kind="beo" status={v.status} />
                        </Stack>
                        <Typography variant="caption" color="text.secondary">
                          {v.frozenAt ? `Chốt: ${dateTimeVi(v.frozenAt)} bởi ${v.approvedByName || "Manager"}` : "Bản thảo"}
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
                    Chưa chốt phiên bản nào. Mỗi lần chốt hoặc duyệt lại sẽ thêm một phiên bản vào đây.
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
