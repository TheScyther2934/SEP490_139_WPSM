import { useState } from "react";
import { Link as RouterLink } from "react-router-dom";
import { useDispatch } from "react-redux";
import { Alert, Button, Stack, Typography } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DownloadIcon from "@mui/icons-material/Download";
import DataState from "../../../components/DataState";
import PaymentTable from "../../../components/PaymentTable";
import PaymentSummary from "../../../components/PaymentSummary";
import FileButton from "../../../components/FileButton";
import RecordPaymentDialog from "./components/RecordPaymentDialog";
import { useAsync } from "../../../hooks/useAsync";
import { useAction } from "../../../hooks/useAction";
import { useProject } from "../../../hooks/useProject";
import { paymentService } from "../../../services/operationsService";
import { projectService } from "../../../services/projectService";
import { loadProject } from "../../../store/projectSlice";
import { notify } from "../../../store/notifySlice";
import { PATHS } from "../../../routes/paths";
import { errorMessage } from "../../../utils/format";

// M-13 (UC-33) with the M-14 dialog. The final payment issues the settlement receipt (UC-34) and the project becomes Settled.
export default function Payments() {
  const { project } = useProject();
  const dispatch = useDispatch();
  const { data, loading, error, reload, setData } = useAsync(() => paymentService.get(project.id), [project.id]);
  const { busy, run } = useAction();
  const [open, setOpen] = useState(false);

  const record = async (form) => {
    const updated = await run(() => paymentService.record(project.id, form), "Đã ghi nhận thanh toán.");
    if (!updated) return;
    setData(updated);
    setOpen(false);
    dispatch(loadProject(project.id));
  };
  // The NRD receipt is stored as bytes on the payment row (UC-18), so it keeps its own blob download.
  const downloadNrd = async () => {
    try {
      const blob = await projectService.downloadNrdReceipt(project.id);
      const url = URL.createObjectURL(blob);
      Object.assign(document.createElement("a"), { href: url, download: `NRD-${project.id}` }).click();
      setTimeout(() => URL.revokeObjectURL(url), 60000);
    } catch (e) {
      dispatch(notify({ message: errorMessage(e), severity: "error" }));
    }
  };

  return (
    <>
      <Stack direction="row" sx={{ mb: 2, justifyContent: "space-between", alignItems: "center" }}>
        <Typography variant="h6">Thanh toán và quyết toán (M-13)</Typography>
        <Stack direction="row" spacing={1}>
          {data?.settlementReceiptFileId && <Button component={RouterLink} to={PATHS.manager.settlementReceipt(project.id)}>Biên nhận quyết toán</Button>}
          <Button variant="contained" startIcon={<AddIcon />} onClick={() => setOpen(true)} disabled={!data || data.outstanding <= 0 || busy}>Ghi nhận thanh toán</Button>
        </Stack>
      </Stack>
      <DataState loading={loading} error={error} onRetry={reload}>
        {data && (
          <>
            <PaymentSummary payments={data} />
            {data.canRecordFinal && data.outstanding > 0 && (
              <Alert severity="warning" sx={{ mb: 2 }}>Tiệc đã diễn ra. Số tiền quyết toán cần thu: {data.outstanding.toLocaleString("vi-VN")} ₫.</Alert>
            )}
            <PaymentTable payments={data.payments} action={(p) => (
              p.receiptFileId ? <FileButton fileId={p.receiptFileId}>Chứng từ</FileButton>
                : p.type === "NRD" && p.hasReceipt ? <Button size="small" startIcon={<DownloadIcon />} onClick={downloadNrd}>Biên nhận NRD</Button> : null
            )} />
            <RecordPaymentDialog open={open} payments={data} busy={busy} onClose={() => setOpen(false)} onSave={record} />
          </>
        )}
      </DataState>
    </>
  );
}
