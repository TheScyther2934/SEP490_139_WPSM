import { useState } from "react";
import { Link as RouterLink, useParams } from "react-router-dom";
import { useDispatch } from "react-redux";
import {
  Alert,
  Button,
  Card,
  CardActions,
  CardContent,
  Chip,
  Divider,
  Grid,
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
import DownloadOutlinedIcon from "@mui/icons-material/DownloadOutlined";
import SyncIcon from "@mui/icons-material/Sync";
import PageHeader from "../../../components/PageHeader";
import DataState from "../../../components/DataState";
import StatusChip from "../../../components/StatusChip";
import { useAsync } from "../../../hooks/useAsync";
import { projectService } from "../../../services/projectService";
import { notify } from "../../../store/notifySlice";
import { PATHS } from "../../../routes/paths";
import { dateTimeVi, dateVi, errorMessage, slotLabel } from "../../../utils/format";

// M-11: BEO Department Operational Sheets & PDF Export (UC-28 | Kitchen, Service, Decor, Tech-MC)
export default function DepartmentSheets() {
  const { projectCode } = useParams();
  const dispatch = useDispatch();
  const { data: beo, loading, error, reload, setData } = useAsync(
    () => projectService.getBeo(projectCode),
    [projectCode]
  );
  const [downloadingDept, setDownloadingDept] = useState(null);
  const [regenerating, setRegenerating] = useState(false);

  const handleRegenerateSheets = async () => {
    setRegenerating(true);
    try {
      const updated = await projectService.generateDepartmentSheets(projectCode);
      setData(updated);
      dispatch(notify({ message: "Đã đồng bộ lại 4 phiếu điều phối bộ phận từ BEO hiện hành.", severity: "success" }));
    } catch (e) {
      dispatch(notify({ message: errorMessage(e), severity: "error" }));
    } finally {
      setRegenerating(false);
    }
  };

  const handleDownloadPdf = async (department) => {
    setDownloadingDept(department);
    try {
      const blob = await projectService.downloadDepartmentPdf(projectCode, department);
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = `BEO_${projectCode}_v${beo?.version || 1}_${department}.pdf`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      window.URL.revokeObjectURL(url);
      dispatch(notify({ message: `Đã xuất file PDF cho bộ phận ${department}.`, severity: "success" }));
    } catch (e) {
      dispatch(notify({ message: errorMessage(e), severity: "error" }));
    } finally {
      setDownloadingDept(null);
    }
  };

  return (
    <>
      <PageHeader
        code="M-11"
        title={`Phiếu Điều phối Bộ phận BEO (${projectCode})`}
        subtitle="Phân tách kế hoạch vận hành theo 4 bộ phận chuyên trách: Kitchen, Service, Decor và Tech-MC kèm xuất PDF."
        actions={
          <Stack direction="row" spacing={1.5}>
            <Button startIcon={<ArrowBackIcon />} component={RouterLink} to={PATHS.manager.beo(projectCode)}>
              Về BEO Tổng (M-10)
            </Button>
            <Button
              variant="contained"
              startIcon={<SyncIcon />}
              onClick={handleRegenerateSheets}
              disabled={regenerating}
            >
              {regenerating ? "Đang đồng bộ..." : "Tạo lại 4 phiếu bộ phận"}
            </Button>
          </Stack>
        }
      />

      <DataState loading={loading} error={error} onRetry={reload} empty={!beo} emptyText="Không tìm thấy dữ liệu BEO.">
        {beo && (
          <Stack spacing={3}>
            <Alert severity={beo.status === "Stale" ? "warning" : "info"}>
              Đang hiển thị phiếu điều phối thuộc <strong>BEO v{beo.version}</strong> · Sảnh <strong>{beo.hallName}</strong> · Ca{" "}
              <strong>{slotLabel(beo.slotName)}</strong> ngày <strong>{dateVi(beo.eventDate)}</strong>.{" "}
              <StatusChip kind="beo" status={beo.status} />
            </Alert>

            <Grid container spacing={2.5}>
              {beo.departmentSheets?.map((sheet) => (
                <Grid item xs={12} md={6} key={sheet.department}>
                  <Card variant="outlined" sx={{ height: "100%", display: "flex", flexDirection: "column" }}>
                    <CardContent sx={{ flexGrow: 1 }}>
                      <Stack direction="row" justifyContent="space-between" alignItems="center" mb={1}>
                        <Typography variant="subtitle1" fontWeight={800}>
                          {sheet.departmentLabel}
                        </Typography>
                        <Chip size="small" color="primary" label={sheet.department} />
                      </Stack>
                      <Typography variant="caption" color="text.secondary" display="block" mb={1.5}>
                        File: {sheet.pdfFilePath} · Cập nhật: {dateTimeVi(sheet.generatedAt)}
                      </Typography>
                      <Divider sx={{ mb: 1.5 }} />

                      <TableContainer>
                        <Table size="small">
                          <TableHead>
                            <TableRow>
                              <TableCell>Hạng mục thực hiện</TableCell>
                              <TableCell align="right">Số lượng chuẩn bị</TableCell>
                              <TableCell>Ghi chú</TableCell>
                            </TableRow>
                          </TableHead>
                          <TableBody>
                            {sheet.items?.length ? (
                              sheet.items.map((item) => (
                                <TableRow key={item.specItemId}>
                                  <TableCell sx={{ fontWeight: 700 }}>{item.itemName}</TableCell>
                                  <TableCell align="right" sx={{ fontWeight: 800 }}>
                                    {item.effectiveQuantity} {item.unit}
                                  </TableCell>
                                  <TableCell>
                                    {item.description}
                                    {item.changedByCr ? " (Đã cập nhật CR)" : ""}
                                  </TableCell>
                                </TableRow>
                              ))
                            ) : (
                              <TableRow>
                                <TableCell colSpan={3} align="center">
                                  Thực hiện theo quy chuẩn vận hành tiêu chuẩn của bộ phận {sheet.department}.
                                </TableCell>
                              </TableRow>
                            )}
                          </TableBody>
                        </Table>
                      </TableContainer>
                    </CardContent>
                    <Divider />
                    <CardActions sx={{ px: 2, py: 1.5, justifyContent: "flex-end" }}>
                      <Button
                        size="small"
                        variant="contained"
                        startIcon={<DownloadOutlinedIcon />}
                        onClick={() => handleDownloadPdf(sheet.department)}
                        disabled={downloadingDept === sheet.department}
                        id={`downloadPdf_${sheet.department}`}
                      >
                        {downloadingDept === sheet.department ? "Đang xuất PDF..." : `Xuất PDF (${sheet.department})`}
                      </Button>
                    </CardActions>
                  </Card>
                </Grid>
              ))}
            </Grid>
          </Stack>
        )}
      </DataState>
    </>
  );
}
