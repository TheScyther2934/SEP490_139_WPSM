import { Link as RouterLink, useParams } from "react-router-dom";
import { Alert, Box, Button, Card, CardContent, Stack, Typography } from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import UploadFileIcon from "@mui/icons-material/UploadFile";
import DataState from "../../../components/DataState";
import FileButton from "../../../components/FileButton";
import StatusChip from "../../../components/StatusChip";
import CostBreakdown from "../../../components/changeRequests/CostBreakdown";
import { CrItemsTable } from "../../../components/changeRequests/CrDetailParts";
import { useAsync } from "../../../hooks/useAsync";
import { useAction } from "../../../hooks/useAction";
import { useProject } from "../../../hooks/useProject";
import { changeRequestService } from "../../../services/operationsService";
import { PATHS } from "../../../routes/paths";
import { dateTimeVi, money } from "../../../utils/format";

// M-09 (UC-23, UC-24): cost recalculation, updated balance, BEO effect, addendum PDF and signed copy.
export default function CostImpact() {
  const { crCode } = useParams();
  const { project } = useProject();
  const { data, loading, error, reload, setData } = useAsync(() => changeRequestService.addendum(project.id, crCode), [project.id, crCode]);
  const { busy, run } = useAction();

  const upload = async (file) => {
    if (!file) return;
    if (file.size > 10 * 1024 * 1024 || !/\.(pdf|jpe?g|png)$/i.test(file.name)) {
      await run(() => Promise.reject(new Error("Chỉ chấp nhận tệp JPG, PNG hoặc PDF, tối đa 10 MB (MSG17).")));
      return;
    }
    const updated = await run(() => changeRequestService.uploadSignedCopy(project.id, crCode, file), "Đã lưu bản ký của phụ lục.");
    if (updated) setData(updated);
  };

  return (
    <>
      <Stack direction="row" sx={{ mb: 2, justifyContent: "space-between", alignItems: "center" }}>
        <Typography variant="h6">Chi phí và phụ lục của {crCode} (M-09)</Typography>
        <Button startIcon={<ArrowBackIcon />} component={RouterLink} to={PATHS.manager.changeRequest(project.id, crCode)}>Chi tiết CR</Button>
      </Stack>
      <DataState loading={loading} error={error} onRetry={reload} empty={!data} emptyText="CR chưa được duyệt nên chưa có phụ lục.">
        {data && (
          <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { lg: "3fr 2fr" }, alignItems: "start" }} id="costImpact">
            <Stack spacing={2}>
              <CrItemsTable items={data.items} />
              {data.beoEffect && <Alert severity={data.beoEffect.includes("Stale") ? "warning" : "info"}>{data.beoEffect}</Alert>}
              <Card>
                <CardContent>
                  <Typography variant="subtitle1" fontWeight={600}>Số dư sau khi duyệt</Typography>
                  <Typography>Giá trị hợp đồng gốc: {money(data.baseValue)}</Typography>
                  <Typography>Tổng dự án hiện tại: <b data-field="currentTotal">{money(data.currentTotal)}</b></Typography>
                  <Typography>Đã thanh toán: {money(data.totalPaid)}</Typography>
                  <Typography>Còn lại: <b>{money(data.outstanding)}</b></Typography>
                </CardContent>
              </Card>
            </Stack>
            <Stack spacing={2}>
              <CostBreakdown cost={data.cost} approved />
              <Card>
                <CardContent>
                  <Stack direction="row" spacing={1} sx={{ alignItems: "center", mb: 1 }}>
                    <Typography variant="subtitle1" fontWeight={600} data-field="addendumCode">Phụ lục {data.addendumCode}</Typography>
                    <StatusChip kind="exception" status={data.status === "Signed" ? "Approved" : "Pending"} />
                  </Stack>
                  <Typography variant="body2" color="text.secondary">
                    {data.signedAt ? `Đã lưu bản ký lúc ${dateTimeVi(data.signedAt)}` : "Chưa có bản ký. Hai bên ký trực tiếp rồi Manager tải bản scan lên."}
                  </Typography>
                  <Stack direction="row" sx={{ mt: 2, flexWrap: "wrap", gap: 1 }}>
                    <FileButton fileId={data.pdfFileId}>{data.pdfFileId ? "Tải PDF phụ lục" : "PDF đang được tạo"}</FileButton>
                    {data.signedFileId && <FileButton fileId={data.signedFileId}>Bản đã ký</FileButton>}
                    <Button component="label" size="small" variant="outlined" startIcon={<UploadFileIcon />} disabled={busy}>
                      Tải lên bản ký
                      <input hidden type="file" accept=".pdf,.jpg,.jpeg,.png" data-field="signedCopy"
                        onChange={(e) => { upload(e.target.files?.[0]); e.target.value = ""; }} />
                    </Button>
                  </Stack>
                </CardContent>
              </Card>
            </Stack>
          </Box>
        )}
      </DataState>
    </>
  );
}
