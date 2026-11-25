import { useState } from "react";
import { Alert, Box, Button, Card, CardContent, LinearProgress, Stack, Typography } from "@mui/material";
import UploadFileIcon from "@mui/icons-material/UploadFile";
import DataState from "../../../components/DataState";
import ServiceBlock from "./components/ServiceBlock";
import EvidenceDialog from "./components/EvidenceDialog";
import TextDialog from "../../../components/TextDialog";
import { useProject } from "../../../hooks/useProject";
import { useAsync } from "../../../hooks/useAsync";
import { useAction } from "../../../hooks/useAction";
import { eventFlowService } from "../../../services/eventFlowService";
import { dateVi } from "../../../utils/format";

const saveBlob = (blob, name) => {
  const url = URL.createObjectURL(blob);
  const a = Object.assign(document.createElement("a"), { href: url, download: name });
  a.click();
  URL.revokeObjectURL(url);
};

// Stage 3: once services are locked the operator books each outside supplier and attaches evidence (transfer note,
// invoice or contract) before the supplier deadline; the system reminds operators after it.
// One list by service, each with its evidence below; services still missing evidence come first.
export default function Suppliers() {
  const { project } = useProject();
  const board = useAsync(() => eventFlowService.suppliers(project.id), [project.id]);
  const { busy, run } = useAction();
  const [uploadFor, setUploadFor] = useState(undefined);   // undefined = closed, "" = any service, id = that service
  const [removing, setRemoving] = useState(null);          // the evidence waiting for the delete confirmation
  const b = board.data;

  const lines = b?.lines ?? [];
  const evidences = b?.evidences ?? [];
  const byService = (id) => evidences.filter((e) => e.specItemId === id);
  const ordered = [...lines].sort((x, y) => (byService(x.specItemId).length === 0 ? 0 : 1) - (byService(y.specItemId).length === 0 ? 0 : 1));
  const others = evidences.filter((e) => !e.specItemId || !lines.some((l) => l.specItemId === e.specItemId));
  const done = lines.filter((l) => byService(l.specItemId).length > 0).length;
  const missing = lines.length - done;

  const save = async (form) => {
    const ok = await run(() => eventFlowService.uploadEvidence(project.id, form), "Đã lưu minh chứng nhà cung cấp.");
    if (ok) { setUploadFor(undefined); board.reload({ silent: true }); }
  };
  const remove = async () => {
    const updated = await run(() => eventFlowService.removeEvidence(project.id, removing.id), "Đã xóa minh chứng nhà cung cấp.");
    if (updated) { setRemoving(null); board.setData(updated); }
  };
  const download = async (e) => {
    const blob = await run(() => eventFlowService.downloadEvidence(project.id, e.id));
    if (blob instanceof Blob) saveBlob(blob, e.fileName);
  };

  return (
    <DataState loading={board.loading} error={board.error} onRetry={board.reload}>
      {b && (
        <Stack spacing={2}>
          <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ justifyContent: "space-between", alignItems: { sm: "flex-start" } }}>
            <Box>
              <Typography variant="h6">Nhà cung cấp</Typography>
              <Typography variant="body2" color="text.secondary">
                Đặt nhà cung cấp sau khi khóa dịch vụ ({dateVi(b.lockDate)}) và tải minh chứng trước <b>{dateVi(b.supplierDeadline)}</b>.
              </Typography>
            </Box>
            <Button variant="contained" startIcon={<UploadFileIcon />} disabled={!b.canUpload} onClick={() => setUploadFor("")} sx={{ flexShrink: 0 }}>
              Tải minh chứng
            </Button>
          </Stack>

          {!b.canUpload && (
            <Alert severity="info">
              {b.status === "Adjusting" ? "Sự kiện còn trong giai đoạn điều chỉnh. Minh chứng được tải sau khi khóa dịch vụ." : "Sự kiện không còn nhận minh chứng."}
            </Alert>
          )}
          {b.overdue && <Alert severity="error">Đã quá hạn đặt nhà cung cấp mà còn {missing} dịch vụ chưa có minh chứng. Hệ thống đang nhắc mỗi ngày.</Alert>}

          {lines.length > 0 && (
            <Card>
              <CardContent sx={{ "&:last-child": { pb: 2 } }}>
                <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "baseline", mb: 1, flexWrap: "wrap", gap: 1 }}>
                  <Typography fontWeight={600}>{done}/{lines.length} dịch vụ đã có minh chứng</Typography>
                  <Typography variant="body2" color={missing ? (b.overdue ? "error.main" : "warning.main") : "success.main"}>
                    {missing ? `Còn thiếu ${missing} · hạn ${dateVi(b.supplierDeadline)}` : "Đã đủ minh chứng"}
                  </Typography>
                </Stack>
                <LinearProgress variant="determinate" value={(done / lines.length) * 100}
                  color={missing ? (b.overdue ? "error" : "warning") : "success"} sx={{ height: 8, borderRadius: 4 }} />
              </CardContent>
            </Card>
          )}

          <Stack spacing={1.5}>
            {ordered.map((l) => (
              <ServiceBlock key={l.specItemId} line={l} evidences={byService(l.specItemId)} overdue={b.overdue} canUpload={b.canUpload}
                busy={busy} onUpload={setUploadFor} onDownload={download} onRemove={setRemoving} />
            ))}
            {others.length > 0 && <ServiceBlock line={null} evidences={others} canUpload={b.canUpload} busy={busy} onDownload={download} onRemove={setRemoving} />}
            {lines.length === 0 && others.length === 0 && (
              <Typography color="text.secondary">Sự kiện không có dịch vụ cần nhà cung cấp bên ngoài.</Typography>
            )}
          </Stack>

          <EvidenceDialog specItemId={uploadFor} lines={lines} busy={busy} onClose={() => setUploadFor(undefined)} onSave={save} />
          <TextDialog open={!!removing} title="Xóa minh chứng nhà cung cấp" color="error" confirmText="Xóa" busy={busy}
            description={`Xóa minh chứng "${removing?.supplierName ?? ""}" (${removing?.fileName ?? ""})? Dịch vụ sẽ quay về trạng thái thiếu minh chứng nếu không còn minh chứng nào khác.`}
            onClose={() => setRemoving(null)} onConfirm={remove} />
        </Stack>
      )}
    </DataState>
  );
}
