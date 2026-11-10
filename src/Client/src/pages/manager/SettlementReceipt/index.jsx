import { Link as RouterLink } from "react-router-dom";
import { Alert, Button, Card, CardContent, Stack, Typography } from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import DataState from "../../../components/DataState";
import FileButton from "../../../components/FileButton";
import PaymentSummary from "../../../components/PaymentSummary";
import PaymentTable from "../../../components/PaymentTable";
import { useAsync } from "../../../hooks/useAsync";
import { useAction } from "../../../hooks/useAction";
import { useProject } from "../../../hooks/useProject";
import { paymentService } from "../../../services/operationsService";
import { PATHS } from "../../../routes/paths";
import { dateTimeVi } from "../../../utils/format";

// M-15 (UC-34): settlement receipt PDF; issued automatically with the final payment, or here when the balance is already zero.
export default function SettlementReceipt() {
  const { project } = useProject();
  const { data, loading, error, reload, setData } = useAsync(() => paymentService.get(project.id), [project.id]);
  const { busy, run } = useAction();
  const issue = async () => {
    const updated = await run(() => paymentService.issueSettlementReceipt(project.id), "Đã phát hành biên nhận quyết toán.");
    if (updated) setData(updated);
  };
  return (
    <>
      <Stack direction="row" sx={{ mb: 2, justifyContent: "space-between", alignItems: "center" }}>
        <Typography variant="h6">Biên nhận quyết toán (M-15)</Typography>
        <Button startIcon={<ArrowBackIcon />} component={RouterLink} to={PATHS.manager.payments(project.id)}>Về thanh toán</Button>
      </Stack>
      <DataState loading={loading} error={error} onRetry={reload}>
        {data && (
          <Stack spacing={2}>
            <PaymentSummary payments={data} />
            <Card>
              <CardContent>
                {data.settlementReceiptFileId ? (
                  <Stack spacing={1}>
                    <Typography>Phát hành lúc {dateTimeVi(data.settledAt)}. Dự án đã chuyển sang trạng thái Đã quyết toán.</Typography>
                    <div><FileButton fileId={data.settlementReceiptFileId} variant="contained">Tải / in biên nhận</FileButton></div>
                  </Stack>
                ) : data.outstanding === 0 && data.projectStatus === "Event Completed" ? (
                  <Button variant="contained" onClick={issue} disabled={busy}>Phát hành biên nhận quyết toán</Button>
                ) : (
                  <Alert severity="info">Biên nhận được phát hành sau khi tiệc diễn ra và số dư về 0.</Alert>
                )}
              </CardContent>
            </Card>
            <PaymentTable payments={data.payments} />
          </Stack>
        )}
      </DataState>
    </>
  );
}
