import { FILE_ACCEPT, FILE_TYPES_LABEL } from "../../../../utils/files";
import { useEffect, useRef, useState } from "react";
import { Button, Dialog, DialogActions, DialogContent, DialogTitle, MenuItem, Stack, TextField, Typography } from "@mui/material";
import UploadFileIcon from "@mui/icons-material/UploadFile";
import { EVIDENCE_TYPES } from "./EvidenceItem";

const empty = (specItemId) => ({ supplierName: "", evidenceType: "Transfer", specItemId: specItemId ?? "", note: "", file: null });

// Upload form: supplier, service (preselected when opened from a service), type, note, one PDF or photo (iPhone HEIC accepted).
// `specItemId`: undefined = closed, "" = no service chosen yet.
export default function EvidenceDialog({ specItemId, lines, busy, onClose, onSave }) {
  const open = specItemId !== undefined;
  const [form, setForm] = useState(empty());
  const fileRef = useRef(null);
  useEffect(() => { if (open) setForm(empty(specItemId)); }, [open, specItemId]);
  const set = (key) => (e) => setForm({ ...form, [key]: e.target.value });

  return (
    <Dialog open={open} onClose={busy ? undefined : onClose} fullWidth maxWidth="sm">
      <DialogTitle>Tải minh chứng nhà cung cấp</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          <TextField label="Tên nhà cung cấp" required value={form.supplierName} onChange={set("supplierName")} />
          <TextField select label="Dịch vụ" value={form.specItemId} onChange={set("specItemId")}>
            <MenuItem value="">Không gắn dịch vụ cụ thể</MenuItem>
            {lines.map((l) => <MenuItem key={l.specItemId} value={l.specItemId}>{l.serviceName}</MenuItem>)}
          </TextField>
          <TextField select label="Loại minh chứng" value={form.evidenceType} onChange={set("evidenceType")}>
            {Object.entries(EVIDENCE_TYPES).map(([k, v]) => <MenuItem key={k} value={k}>{v}</MenuItem>)}
          </TextField>
          <TextField label="Ghi chú" multiline minRows={2} value={form.note} onChange={set("note")} />
          <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
            <input ref={fileRef} type="file" hidden accept={FILE_ACCEPT}
              onChange={(e) => setForm({ ...form, file: e.target.files?.[0] ?? null })} />
            <Button variant="outlined" startIcon={<UploadFileIcon />} onClick={() => fileRef.current?.click()}>Chọn tệp *</Button>
            <Typography variant="body2" sx={{ minWidth: 0, overflowWrap: "anywhere" }}>{form.file ? form.file.name : FILE_TYPES_LABEL}</Typography>
          </Stack>
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={busy}>Hủy</Button>
        <Button variant="contained" onClick={() => onSave(form)} disabled={busy || form.supplierName.trim().length < 2 || !form.file}>Lưu</Button>
      </DialogActions>
    </Dialog>
  );
}
