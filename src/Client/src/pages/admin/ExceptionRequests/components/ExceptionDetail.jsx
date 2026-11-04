import { useState } from "react";
import { Alert, Button, Card, CardContent, Stack, TextField, Typography } from "@mui/material";

// Pending requests need a decision note before approve or reject.
export default function ExceptionDetail({ request, onDecide }) {
  const [note, setNote] = useState("");
  const [error, setError] = useState("");
  const [saving, setSaving] = useState(false);

  const decide = async (approve) => {
    if (!note.trim()) { setError("Trường Ghi chú quyết định là bắt buộc."); return; }
    setSaving(true);
    const message = await onDecide(approve, note.trim());
    setSaving(false);
    setError(message ?? "");
  };

  const rows = [["Bối cảnh", request.context], ["Ghi chú của quản lý", request.managerNote], ["Ghi chú quyết định", request.decisionNote || "—"]];
  return (
    <Card id="a18Detail" sx={{ mt: 2 }}>
      <CardContent>
        <Typography variant="h6" gutterBottom>Yêu cầu {request.code}</Typography>
        <Stack spacing={1.5}>
          {rows.map(([label, value]) => (
            <div key={label}><Typography variant="caption" color="text.secondary">{label}</Typography><Typography>{value}</Typography></div>
          ))}
          {request.status === "Pending" && (
            <>
              {error && <Alert severity="error">{error}</Alert>}
              <TextField id="a18Note" label="Ghi chú quyết định" multiline minRows={3} value={note} onChange={(e) => setNote(e.target.value)} />
              <Stack direction="row" spacing={1} sx={{ justifyContent: "flex-end" }}>
                <Button color="error" variant="outlined" disabled={saving} onClick={() => decide(false)}>Từ chối</Button>
                <Button variant="contained" disabled={saving} onClick={() => decide(true)}>Chấp thuận</Button>
              </Stack>
            </>
          )}
        </Stack>
      </CardContent>
    </Card>
  );
}
