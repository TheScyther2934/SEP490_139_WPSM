import { useEffect, useState } from "react";
import { Link as RouterLink } from "react-router-dom";
import { useDispatch } from "react-redux";
import {
  Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, MenuItem, Stack, TextField, Typography,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import SaveIcon from "@mui/icons-material/Save";
import UndoIcon from "@mui/icons-material/Undo";
import SpecSummary from "./components/SpecSummary";
import SpecTable from "./components/SpecTable";
import CatalogPickerDialog from "./components/CatalogPickerDialog";
import { lineError, specTotal, toSpecificationBody } from "./rules";
import { useProject } from "../../../hooks/useProject";
import { projectService } from "../../../services/projectService";
import { projectUpdated } from "../../../store/projectSlice";
import { notify } from "../../../store/notifySlice";
import { PATHS } from "../../../routes/paths";
import { dateTimeVi, dateVi, errorMessage, money } from "../../../utils/format";

const toRows = (specification) => (specification ?? []).map((s) => ({ ...s, description: s.description ?? "" }));
const NOTE_TYPES = [
  { value: "Adjustment", label: "Điều chỉnh dịch vụ" },
  { value: "Tasting", label: "Sau buổi thử món" },
  { value: "Fitting", label: "Sau buổi thử đồ" },
];

// Services booked for the event. Stage 2 (until the lock date): the customer tells the operator what to change, the operator
// edits the lines and writes a note; the total may go up freely but not below the agreed floor. After the lock: read-only.
export default function Specification() {
  const dispatch = useDispatch();
  const { project, catalog, priceFrames } = useProject();
  const [rows, setRows] = useState(() => toRows(project.specification));
  const [pickerOpen, setPickerOpen] = useState(false);
  const [noteOpen, setNoteOpen] = useState(false);
  const [note, setNote] = useState("");
  const [noteType, setNoteType] = useState("Adjustment");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => { setRows(toRows(project.specification)); }, [project]);

  const readOnly = !project.canAdjust;
  const savedIds = new Set((project.specification ?? []).map((s) => Number(s.catalogItemId)));
  const rowsValid = rows.length > 0 && rows.every((r) => !lineError(r, priceFrames, catalog, !savedIds.has(Number(r.catalogItemId))));
  const total = specTotal(rows);
  const belowFloor = total < project.minimumTotal;
  const dirty = JSON.stringify(rows.map((r) => [r.catalogItemId, Number(r.quantity), Number(r.unitPrice), r.description]))
    !== JSON.stringify(toRows(project.specification).map((r) => [r.catalogItemId, Number(r.quantity), Number(r.unitPrice), r.description]));

  const change = (catalogItemId, patch) => setRows((list) => list.map((r) => (r.catalogItemId === catalogItemId ? { ...r, ...patch } : r)));
  const remove = (catalogItemId) => setRows((list) => list.filter((r) => r.catalogItemId !== catalogItemId));
  const add = (item) => setRows((list) => [...list, {
    catalogItemId: Number(item.id), name: item.name, category: item.category, description: item.name,
    quantity: 1, unit: item.unit, unitPrice: item.basePrice, lineTotal: item.basePrice,
  }]);

  const save = async () => {
    setSaving(true);
    setError("");
    try {
      const saved = await projectService.saveSpecification(project.id, toSpecificationBody(rows, note.trim(), noteType));
      dispatch(projectUpdated(saved));
      dispatch(notify("Đã lưu điều chỉnh và ghi chú."));
      setNoteOpen(false);
      setNote("");
    } catch (e) {
      setError(errorMessage(e));
      setNoteOpen(false);
    } finally {
      setSaving(false);
    }
  };

  return (
    <>
      <Stack direction={{ xs: "column", md: "row" }} spacing={2} sx={{ mb: 2, justifyContent: "space-between" }}>
        <div>
          <Typography variant="h6">Dịch vụ đã đặt</Typography>
          <Typography variant="body2" color="text.secondary">
            {readOnly ? "Dịch vụ đã khóa, chỉ xem." : `Khách báo thay đổi, bạn cập nhật và ghi chú lại. Hạn điều chỉnh: ${dateVi(project.lockDate)}.`}
          </Typography>
        </div>
        {!readOnly && (
          <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap", gap: 1, alignItems: "flex-start" }}>
            <Button variant="outlined" startIcon={<AddIcon />} disabled={saving} onClick={() => setPickerOpen(true)}>Thêm dịch vụ</Button>
            <Button variant="text" startIcon={<UndoIcon />} disabled={saving || !dirty} onClick={() => setRows(toRows(project.specification))}>Hoàn tác</Button>
            <Button id="saveSpecificationBtn" variant="contained" startIcon={<SaveIcon />} disabled={saving || !dirty || !rowsValid || belowFloor}
              onClick={() => setNoteOpen(true)}>Lưu điều chỉnh</Button>
          </Stack>
        )}
      </Stack>

      {error && <Alert severity="error" role="alert" sx={{ mb: 2 }}>{error}</Alert>}
      {readOnly && project.status !== "Cancelled" && (
        <Alert severity="info" sx={{ mb: 2 }}
          action={project.canAddAddon && <Button color="inherit" component={RouterLink} to={PATHS.manager.addons(project.id)}>Tạo hợp đồng phát sinh</Button>}>
          Dịch vụ đã khóa{project.lockedAt ? ` lúc ${dateTimeVi(project.lockedAt)}` : ` từ ${dateVi(project.lockDate)}`}. Khách cần thêm gì thì tạo hợp đồng phát sinh.
        </Alert>
      )}
      {!readOnly && belowFloor && (
        <Alert severity="error" sx={{ mb: 2 }}>
          Tổng sau khi sửa ({money(total)}) thấp hơn mức tối thiểu {money(project.minimumTotal)}. Khách không được giảm quá 30% giá trị lúc ký.
        </Alert>
      )}
      <SpecSummary signedValue={project.baseValue} savedTotal={project.currentTotal} total={total} minimumTotal={project.minimumTotal} />
      <SpecTable rows={rows} priceFrames={priceFrames} catalog={catalog} readOnly={readOnly} onChange={change} onRemove={remove} total={total}
        isNew={(id) => !savedIds.has(Number(id))} />

      <CatalogPickerDialog open={pickerOpen} catalog={catalog} priceFrames={priceFrames}
        selectedIds={new Set(rows.map((r) => Number(r.catalogItemId)))} onAdd={add} onClose={() => setPickerOpen(false)} />

      <Dialog open={noteOpen} onClose={saving ? undefined : () => setNoteOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>Ghi chú điều chỉnh</DialogTitle>
        <DialogContent>
          <Typography sx={{ mb: 2 }}>
            Tổng giá trị: {money(project.currentTotal)} → <b>{money(total)}</b>. Ghi rõ khách yêu cầu gì để mọi người phụ trách cùng theo dõi.
          </Typography>
          <TextField select fullWidth label="Lý do" value={noteType} onChange={(e) => setNoteType(e.target.value)} sx={{ mb: 2 }}>
            {NOTE_TYPES.map((t) => <MenuItem key={t.value} value={t.value}>{t.label}</MenuItem>)}
          </TextField>
          <TextField autoFocus fullWidth multiline minRows={3} label="Nội dung ghi chú" required value={note}
            onChange={(e) => setNote(e.target.value)} helperText={`${note.length}/1000 · tối thiểu 5 ký tự`}
            slotProps={{ htmlInput: { maxLength: 1000 } }} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setNoteOpen(false)} disabled={saving}>Hủy</Button>
          <Button variant="contained" onClick={save} disabled={saving || note.trim().length < 5}>Lưu</Button>
        </DialogActions>
      </Dialog>
    </>
  );
}
