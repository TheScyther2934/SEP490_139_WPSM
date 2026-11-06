import { useEffect, useState } from "react";
import { Alert, Box, Button, Card, CardContent, MenuItem, Stack, TextField } from "@mui/material";
import CrItemsEditor, { linesValid } from "./CrItemsEditor";
import CostBreakdown from "./CostBreakdown";
import DataState from "../DataState";
import { CR_TYPES } from "./crLabels";
import { useAsync } from "../../hooks/useAsync";
import { useAction } from "../../hooks/useAction";
import { changeRequestService } from "../../services/operationsService";

// C-06 form, also used by the Manager on M-07 to record a request received by phone (UC-22 alternative flow).
// The cost preview is recalculated 400 ms after the last edit (SDS III.2.2) and is never stored.
export default function CrCreateForm({ projectCode, onCreated, onCancel }) {
  const specs = useAsync(() => changeRequestService.specItems(projectCode), [projectCode]);
  const [type, setType] = useState("");
  const [reason, setReason] = useState("");
  const [lines, setLines] = useState([{ specItemId: null, newQuantity: 1, newUnitPrice: 0 }]);
  const [showErrors, setShowErrors] = useState(false);
  const [preview, setPreview] = useState(null);
  const [previewError, setPreviewError] = useState("");
  const { busy, run } = useAction();
  const specItems = specs.data ?? [];
  const valid = linesValid(lines, specItems);

  useEffect(() => {
    if (!valid) { setPreview(null); return undefined; }
    const timer = setTimeout(() => {
      changeRequestService.preview(projectCode, lines)
        .then((cost) => { setPreview(cost); setPreviewError(""); })
        .catch((e) => { setPreview(null); setPreviewError(e.response?.data?.message ?? ""); });
    }, 400);
    return () => clearTimeout(timer);
  }, [projectCode, lines, valid]);

  const save = async (submit) => {
    setShowErrors(true);
    if (!type || !reason.trim() || reason.length > 500 || !valid) return;
    const created = await run(() => changeRequestService.create(projectCode, { type, reason: reason.trim(), items: lines, submit }),
      submit ? "Đã gửi yêu cầu thay đổi (MSG19)." : "Đã lưu bản nháp.");
    if (created) onCreated(created);
  };

  return (
    <DataState loading={specs.loading} error={specs.error} onRetry={specs.reload}>
      <Box component="form" id="crCreateForm" noValidate onSubmit={(e) => { e.preventDefault(); save(true); }}
        sx={{ display: "grid", gap: 2, gridTemplateColumns: { lg: "3fr 2fr" }, alignItems: "start" }}>
        <Card>
          <CardContent>
            <Stack spacing={2}>
              <TextField id="crType" select label="Loại thay đổi" required value={type} onChange={(e) => setType(e.target.value)}
                error={showErrors && !type} helperText={showErrors && !type ? "Trường Loại thay đổi là bắt buộc." : " "}
                slotProps={{ htmlInput: { "data-field": "crType" } }}>
                {CR_TYPES.map((t) => <MenuItem key={t.value} value={t.value}>{t.label}</MenuItem>)}
              </TextField>
              <CrItemsEditor specItems={specItems} lines={lines} onChange={setLines} showErrors={showErrors} />
              <TextField label="Lý do" required multiline minRows={3} value={reason} onChange={(e) => setReason(e.target.value)}
                error={showErrors && (!reason.trim() || reason.length > 500)}
                helperText={showErrors && !reason.trim() ? "Trường Lý do là bắt buộc." : reason.length > 500 ? "Vượt quá độ dài tối đa 500 ký tự." : `${reason.length}/500`}
                slotProps={{ htmlInput: { "data-field": "crReason" } }} />
              <Stack direction="row" spacing={1} sx={{ justifyContent: "flex-end" }}>
                {onCancel && <Button onClick={onCancel} disabled={busy}>Hủy</Button>}
                <Button onClick={() => save(false)} disabled={busy}>Lưu nháp</Button>
                <Button type="submit" variant="contained" disabled={busy}>Gửi yêu cầu</Button>
              </Stack>
            </Stack>
          </CardContent>
        </Card>
        <Stack spacing={2}>
          {previewError && <Alert severity="error">{previewError}</Alert>}
          {preview ? <CostBreakdown cost={preview} estimate /> : <Alert severity="info">Chọn hạng mục và giá trị mới để xem ước tính chi phí.</Alert>}
        </Stack>
      </Box>
    </DataState>
  );
}
