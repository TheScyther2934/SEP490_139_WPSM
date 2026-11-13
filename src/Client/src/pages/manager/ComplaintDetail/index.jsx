import { useState } from "react";
import { Link as RouterLink, useParams } from "react-router-dom";
import { useDispatch } from "react-redux";
import {
  Alert,
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
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutlined";
import EscalatorWarningIcon from "@mui/icons-material/EscalatorWarning";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import StatusChip from "../../../components/StatusChip";
import EscalateComplaintDialog from "./components/EscalateComplaintDialog";
import { useAsync } from "../../../hooks/useAsync";
import { projectService } from "../../../services/projectService";
import { notify } from "../../../store/notifySlice";
import { PATHS } from "../../../routes/paths";
import { dateTimeVi, dateVi, errorMessage, money } from "../../../utils/format";

// M-19, M-20: Manager Complaint Detail, Response & Escalate to Admin (UC-35, UC-36 | BF-08)
export default function ComplaintDetail() {
  const { projectCode, complaintCode } = useParams();
  const dispatch = useDispatch();

  const { data: item, loading, error, reload, setData } = useAsync(
    () => projectService.getComplaint(complaintCode),
    [complaintCode]
  );

  const [resolutionNote, setResolutionNote] = useState("");
  const [validationError, setValidationError] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [escalateOpen, setEscalateOpen] = useState(false);

  const handleResolve = async () => {
    const trimmed = resolutionNote.trim();
    if (!trimmed) {
      setValidationError("Vui lòng nhập phương án giải quyết khiếu nại để gửi cho khách hàng.");
      return;
    }
    setValidationError("");
    setSubmitting(true);
    try {
      const updated = await projectService.actOnComplaint(complaintCode, "Resolve", trimmed);
      setData(updated);
      dispatch(
        notify({
          message: `Đã phản hồi và chốt giải quyết khiếu nại ${complaintCode} thành công.`,
          severity: "success",
        })
      );
    } catch (e) {
      dispatch(notify({ message: errorMessage(e), severity: "error" }));
    } finally {
      setSubmitting(false);
    }
  };

  const handleEscalate = async (escalationNote) => {
    setSubmitting(true);
    try {
      const updated = await projectService.actOnComplaint(complaintCode, "Escalate", escalationNote);
      setData(updated);
      setEscalateOpen(false);
      dispatch(
        notify({
          message: `Đã chuyển cấp khiếu nại ${complaintCode} lên Ban Quản trị (Admin).`,
          severity: "warning",
        })
      );
    } catch (e) {
      dispatch(notify({ message: errorMessage(e), severity: "error" }));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <>
      <PageHeader
        code="M-19"
        title={`Chi tiết & Xử lý Khiếu nại (${complaintCode})`}
        subtitle="Đối chiếu phản ánh của khách hàng với cam kết trong BEO, phản hồi giải quyết trực tiếp hoặc chuyển cấp lên Admin (M-20)."
        actions={
          <Stack direction="row" spacing={1.5}>
            <Button startIcon={<ArrowBackIcon />} component={RouterLink} to={PATHS.manager.complaints(projectCode)}>
              Danh sách khiếu nại (M-18)
            </Button>
            {item?.status === "Open" && (
              <Button
                variant="outlined"
                color="error"
                startIcon={<EscalatorWarningIcon />}
                onClick={() => setEscalateOpen(true)}
                disabled={submitting}
                id="openEscalateBtn"
              >
                Chuyển cấp lên Admin (M-20)
              </Button>
            )}
          </Stack>
        }
      />

      <DataState loading={loading} error={error} onRetry={reload} empty={!item} emptyText="Không tìm thấy hồ sơ khiếu nại.">
        {item && (
          <Stack spacing={3}>
            {item.status === "Escalated" && (
              <Alert severity="warning">
                Khiếu nại này đã được chuyển cấp lên <strong>Admin</strong> vào lúc <strong>{dateTimeVi(item.escalatedAt)}</strong>. Đang chờ Admin ra quyết định tại màn hình A-16/A-17.
              </Alert>
            )}
            {item.status === "Resolved" && (
              <Alert severity="success">
                Khiếu nại đã được giải quyết hoàn tất vào lúc <strong>{dateTimeVi(item.resolvedAt)}</strong>.
              </Alert>
            )}

            <Grid container spacing={2.5}>
              <Grid item xs={12} md={6}>
                <Card variant="outlined" sx={{ height: "100%" }}>
                  <CardContent>
                    <Stack direction="row" justifyContent="space-between" alignItems="center" mb={1.5}>
                      <Typography variant="subtitle1" fontWeight={800}>
                        Hồ sơ Khiếu nại & Bằng chứng
                      </Typography>
                      <StatusChip kind="complaint" status={item.status} />
                    </Stack>
                    <Divider sx={{ mb: 2 }} />
                    <Stack spacing={1.2}>
                      <Stack direction="row" justifyContent="space-between">
                        <Typography variant="body2" color="text.secondary">Mã khiếu nại:</Typography>
                        <Typography fontWeight={800}>{item.complaintCode}</Typography>
                      </Stack>
                      <Stack direction="row" justifyContent="space-between">
                        <Typography variant="body2" color="text.secondary">Khách hàng:</Typography>
                        <Typography fontWeight={700}>{item.coupleName}</Typography>
                      </Stack>
                      <Stack direction="row" justifyContent="space-between">
                        <Typography variant="body2" color="text.secondary">Sảnh & Ngày tiệc:</Typography>
                        <Typography fontWeight={700}>
                          {item.hallName} · {dateVi(item.eventDate)}
                        </Typography>
                      </Stack>
                      <Stack direction="row" justifyContent="space-between">
                        <Typography variant="body2" color="text.secondary">BEO đối chiếu:</Typography>
                        <Chip size="small" label={`BEO v${item.beoVersion} (${item.beoStatus})`} color="primary" variant="outlined" />
                      </Stack>
                      <Stack direction="row" justifyContent="space-between">
                        <Typography variant="body2" color="text.secondary">Ảnh nghiệm thu hiện trường:</Typography>
                        <Typography fontWeight={700}>{item.proofPhotosCount} ảnh đã tải lên</Typography>
                      </Stack>
                      <Divider />
                      <Typography variant="caption" color="text.secondary">Nội dung khách hàng phản ánh:</Typography>
                      <Typography sx={{ whiteSpace: "pre-wrap" }} fontWeight={600}>
                        {item.description}
                      </Typography>
                    </Stack>
                  </CardContent>
                </Card>
              </Grid>

              <Grid item xs={12} md={6}>
                <Card variant="outlined" sx={{ height: "100%", bgcolor: "grey.50" }}>
                  <CardContent>
                    <Typography variant="subtitle1" fontWeight={800} mb={1.5}>
                      Xử lý & Phản hồi của Quản lý (M-19)
                    </Typography>
                    <Divider sx={{ mb: 2 }} />

                    {item.status === "Open" ? (
                      <Stack spacing={2}>
                        <TextField
                          label="Phương án giải quyết & Phản hồi khách hàng (tối đa 1000 ký tự) *"
                          multiline
                          minRows={5}
                          value={resolutionNote}
                          onChange={(e) => {
                            setResolutionNote(e.target.value);
                            if (validationError) setValidationError("");
                          }}
                          error={Boolean(validationError)}
                          helperText={validationError || `${resolutionNote.length}/1000 ký tự`}
                          inputProps={{ maxLength: 1000 }}
                          placeholder="Nhập kết quả xác minh với bộ phận vận hành và phương án giải quyết / khấu trừ thỏa thuận với khách hàng..."
                          bgcolor="background.paper"
                          fullWidth
                        />
                        <Stack direction="row" justifyContent="flex-end" spacing={1.5}>
                          <Button
                            variant="outlined"
                            color="error"
                            startIcon={<EscalatorWarningIcon />}
                            onClick={() => setEscalateOpen(true)}
                            disabled={submitting}
                          >
                            Chuyển lên Admin (M-20)
                          </Button>
                          <Button
                            variant="contained"
                            color="success"
                            startIcon={<CheckCircleOutlineIcon />}
                            onClick={handleResolve}
                            disabled={submitting}
                            id="resolveComplaintBtn"
                          >
                            {submitting ? "Đang lưu..." : "Phản hồi & Chốt giải quyết"}
                          </Button>
                        </Stack>
                      </Stack>
                    ) : (
                      <Stack spacing={2}>
                        {item.escalationNote && (
                          <Paper variant="outlined" sx={{ p: 1.5, bgcolor: "warning.50" }}>
                            <Typography variant="caption" fontWeight={700} color="warning.dark">
                              Ghi chú chuyển cấp của Manager ({dateTimeVi(item.escalatedAt)}):
                            </Typography>
                            <Typography variant="body2" sx={{ mt: 0.5 }}>
                              {item.escalationNote}
                            </Typography>
                          </Paper>
                        )}
                        {item.resolutionNote && (
                          <Paper variant="outlined" sx={{ p: 2, bgcolor: "background.paper" }}>
                            <Typography variant="caption" fontWeight={700} color="success.main">
                              Quyết định giải quyết ({dateTimeVi(item.resolvedAt)}):
                            </Typography>
                            <Typography variant="body1" fontWeight={600} sx={{ mt: 0.5, whiteSpace: "pre-wrap" }}>
                              {item.resolutionNote}
                            </Typography>
                          </Paper>
                        )}
                      </Stack>
                    )}
                  </CardContent>
                </Card>
              </Grid>
            </Grid>

            <Paper variant="outlined" sx={{ p: 2.5 }}>
              <Typography variant="subtitle1" fontWeight={800} mb={1.5}>
                Đối chiếu Hạng mục Vận hành trong BEO v{item.beoVersion}
              </Typography>
              <TableContainer>
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>Bộ phận</TableCell>
                      <TableCell>Hạng mục dịch vụ</TableCell>
                      <TableCell>Chi tiết cam kết</TableCell>
                      <TableCell align="right">SL Baseline</TableCell>
                      <TableCell align="right">SL BEO hiệu lực</TableCell>
                      <TableCell align="right">Thành tiền</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {item.disputedBeoItems?.map((line) => (
                      <TableRow key={line.specItemId}>
                        <TableCell>
                          <Chip size="small" label={line.department} variant="outlined" />
                        </TableCell>
                        <TableCell sx={{ fontWeight: 700 }}>{line.itemName}</TableCell>
                        <TableCell>{line.description}</TableCell>
                        <TableCell align="right">
                          {line.baselineQuantity} {line.unit}
                        </TableCell>
                        <TableCell align="right" sx={{ fontWeight: 800 }}>
                          {line.effectiveQuantity} {line.unit}
                        </TableCell>
                        <TableCell align="right">{money(line.lineTotal)}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </TableContainer>
            </Paper>
          </Stack>
        )}
      </DataState>

      <EscalateComplaintDialog
        open={escalateOpen}
        complaint={item}
        submitting={submitting}
        onClose={() => setEscalateOpen(false)}
        onConfirm={handleEscalate}
      />
    </>
  );
}
