import { useState } from "react";
import { Button, Card, CardContent, Chip, List, ListItem, ListItemText, MenuItem, Stack, TextField, Typography } from "@mui/material";
import NoteAddIcon from "@mui/icons-material/NoteAdd";
import { useAction } from "../../../../hooks/useAction";
import { eventFlowService } from "../../../../services/eventFlowService";
import { dateTimeVi, money } from "../../../../utils/format";

export const NOTE_TYPES = {
  Tasting: { label: "Thử món", color: "secondary" },
  Fitting: { label: "Thử đồ", color: "secondary" },
  Adjustment: { label: "Điều chỉnh dịch vụ", color: "primary" },
  Supplier: { label: "Nhà cung cấp", color: "info" },
  General: { label: "Lưu ý chung", color: "default" },
};

// Notes the operators keep per stage (what the customer chose at the tasting, the fitting, special requests).
// Adjustment notes are written by the service editor together with the total before / after.
export default function StageNotes({ code, notes, readOnly, onAdded, showForm = true }) {
  const { busy, run } = useAction();
  const [type, setType] = useState("Tasting");
  const [content, setContent] = useState("");
  const add = async () => {
    if (content.trim().length < 3) return;
    const ok = await run(() => eventFlowService.addNote(code, type, content.trim()), "Đã lưu ghi chú.");
    if (ok) { setContent(""); onAdded?.(); }
  };
  return (
    <Card>
      <CardContent>
        <Typography variant="h6" sx={{ mb: 1 }}>Ghi chú theo giai đoạn</Typography>
        {showForm && !readOnly && (
          <Stack direction={{ xs: "column", sm: "row" }} spacing={1} sx={{ mb: 2 }}>
            <TextField select size="small" label="Loại" value={type} onChange={(e) => setType(e.target.value)} sx={{ minWidth: 160 }}>
              {["Tasting", "Fitting", "Supplier", "General"].map((t) => <MenuItem key={t} value={t}>{NOTE_TYPES[t].label}</MenuItem>)}
            </TextField>
            <TextField size="small" fullWidth multiline maxRows={4} label="Nội dung (khách chốt gì, cần lưu ý gì)" value={content}
              onChange={(e) => setContent(e.target.value)} slotProps={{ htmlInput: { maxLength: 1000 } }} />
            <Button variant="contained" startIcon={<NoteAddIcon />} disabled={busy || content.trim().length < 3} onClick={add}>Lưu</Button>
          </Stack>
        )}
        <List dense disablePadding>
          {notes.map((n) => (
            <ListItem key={n.id} disableGutters alignItems="flex-start" divider>
              <ListItemText
                primary={<Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap", gap: 0.5 }}>
                  <Chip size="small" label={NOTE_TYPES[n.noteType]?.label ?? n.noteType} color={NOTE_TYPES[n.noteType]?.color ?? "default"} />
                  <Typography variant="caption" color="text.secondary">Giai đoạn {n.stage}: {n.stageTitle} · {n.createdBy} · {dateTimeVi(n.createdAt)}</Typography>
                </Stack>}
                secondary={<>
                  <Typography component="span" variant="body2" color="text.primary" sx={{ display: "block", mt: 0.5, whiteSpace: "pre-line" }}>{n.content}</Typography>
                  {n.valueBefore != null && (
                    <Typography component="span" variant="caption" color="text.secondary">
                      Tổng giá trị: {money(n.valueBefore)} → <b>{money(n.valueAfter)}</b>
                    </Typography>
                  )}
                </>} />
            </ListItem>
          ))}
          {notes.length === 0 && <Typography color="text.secondary">Chưa có ghi chú.</Typography>}
        </List>
      </CardContent>
    </Card>
  );
}
