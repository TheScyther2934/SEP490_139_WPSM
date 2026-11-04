import { useState } from "react";
import { Link as RouterLink } from "react-router-dom";
import { Button, Card, CardContent, Link, Stack, TextField, Typography } from "@mui/material";
import { PATHS } from "../../../../routes/paths";

// Detail of the selected complaint; "Giải quyết" opens the A-17 confirmation with the typed decision.
export default function ComplaintDetail({ complaint, onResolve }) {
  const [decision, setDecision] = useState("");
  const [error, setError] = useState("");
  const submit = () => {
    if (!decision.trim()) { setError("Trường Quyết định là bắt buộc."); return; }
    setError("");
    onResolve(decision.trim());
  };
  return (
    <Card id="a16Detail" sx={{ mt: 2 }}>
      <CardContent>
        <Typography variant="h6" gutterBottom>Khiếu nại {complaint.code}</Typography>
        <Stack spacing={1.5}>
          <div><Typography variant="caption" color="text.secondary">Nội dung</Typography><Typography>{complaint.description}</Typography></div>
          <div><Typography variant="caption" color="text.secondary">Ghi chú của quản lý</Typography><Typography>{complaint.managerNote || "—"}</Typography></div>
          <div>
            <Typography variant="caption" color="text.secondary">Minh chứng</Typography>
            <Typography>
              <Link component={RouterLink} to={`${PATHS.admin.photoFeed}?projectCode=${encodeURIComponent(complaint.projectCode)}`}>
                BEO v{complaint.beoVersion} · {complaint.photoCount} ảnh minh chứng
              </Link>
            </Typography>
          </div>
          <TextField id="a16Decision" label="Quyết định" multiline minRows={3} value={decision}
            onChange={(e) => setDecision(e.target.value)} error={!!error} helperText={error} />
          <Stack direction="row" sx={{ justifyContent: "flex-end" }}>
            <Button variant="contained" onClick={submit}>Giải quyết</Button>
          </Stack>
        </Stack>
      </CardContent>
    </Card>
  );
}
