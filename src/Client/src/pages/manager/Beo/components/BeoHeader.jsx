import { useEffect, useState } from "react";
import { Link as RouterLink } from "react-router-dom";
import { Alert, Button, Card, CardContent, Chip, Stack, TextField, Typography } from "@mui/material";
import StatusChip from "../../../../components/StatusChip";
import { PATHS } from "../../../../routes/paths";
import { dateTimeVi } from "../../../../utils/format";

// Status, version, freeze checks (T-7, pending CRs - GBR-05) and Manager notes while Draft.
export default function BeoHeader({ projectCode, beo, busy, onFreeze, onReapprove, onSaveNotes }) {
  const [notes, setNotes] = useState(beo.notes ?? "");
  useEffect(() => setNotes(beo.notes ?? ""), [beo.notes]);
  const editable = beo.status === "None" || beo.status === "Draft";
  return (
    <Card>
      <CardContent>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ justifyContent: "space-between", alignItems: { sm: "center" } }}>
          <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
            <StatusChip kind="beo" status={beo.status} />
            {beo.version && <Chip size="small" variant="outlined" label={`Phiên bản v${beo.version}`} />}
            <Chip size="small" variant="outlined" label={beo.tDay >= 0 ? `T-${beo.tDay}` : "Đã diễn ra"} />
          </Stack>
          <Stack direction="row" spacing={1}>
            {editable && <Button variant="contained" onClick={onFreeze} disabled={!beo.canFreeze || busy} data-action="freezeBeo">Duyệt và đóng băng</Button>}
            {beo.status === "Stale" && <Button variant="contained" color="warning" onClick={onReapprove} disabled={busy}>Duyệt lại</Button>}
            {beo.sheets.length > 0 && <Button component={RouterLink} to={PATHS.manager.departmentSheets(projectCode)}>File PDF các bộ phận</Button>}
          </Stack>
        </Stack>
        {beo.frozenAt && <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>Đóng băng lúc {dateTimeVi(beo.frozenAt)} bởi {beo.approvedBy}</Typography>}
        {beo.status === "Stale" && <Alert severity="error" sx={{ mt: 2 }} id="beoStale">BEO đã lỗi thời do có thay đổi được duyệt. Vui lòng duyệt lại (MSG22).</Alert>}
        {editable && beo.freezeBlockReason && <Alert severity="info" sx={{ mt: 2 }} id="beoBlock">{beo.freezeBlockReason}</Alert>}
        {beo.pendingCrCodes.length > 0 && (
          <Stack direction="row" spacing={1} sx={{ mt: 1, flexWrap: "wrap", gap: 1 }}>
            {beo.pendingCrCodes.map((code) => (
              <Chip key={code} label={code} color="warning" component={RouterLink} to={PATHS.manager.changeRequest(projectCode, code)} clickable />
            ))}
          </Stack>
        )}
        {editable && (
          <Stack direction="row" spacing={1} sx={{ mt: 2, alignItems: "flex-start" }}>
            <TextField fullWidth multiline minRows={2} label="Ghi chú của Manager" value={notes} onChange={(e) => setNotes(e.target.value)}
              error={notes.length > 500} helperText={notes.length > 500 ? "Vượt quá độ dài tối đa 500 ký tự." : `${notes.length}/500`} />
            <Button onClick={() => onSaveNotes(notes)} disabled={busy || notes.length > 500}>Lưu</Button>
          </Stack>
        )}
      </CardContent>
    </Card>
  );
}
