import { useEffect, useState } from "react";
import { Alert, Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, Typography } from "@mui/material";
import PhotoCameraIcon from "@mui/icons-material/PhotoCamera";

// M-17 (UC-31): 1–3 JPG/PNG photos up to 10 MB each; checked here and again by the API (GBR-06, MSG10, MSG17).
export const photoError = (files, required) => {
  if (files.length > 3 || (required && files.length === 0)) return "Vui lòng tải lên 1–3 ảnh để hoàn thành công việc này (MSG10).";
  if (files.some((f) => f.size > 10 * 1024 * 1024 || !/\.(jpe?g|png)$/i.test(f.name))) return "Chỉ chấp nhận ảnh JPG, PNG, tối đa 10 MB (MSG17).";
  return "";
};

export default function ProofUploadDialog({ task, busy, onClose, onComplete }) {
  const [files, setFiles] = useState([]);
  const [touched, setTouched] = useState(false);
  useEffect(() => { setFiles([]); setTouched(false); }, [task]);
  const error = photoError(files, task?.photoRequired);
  return (
    <Dialog open={!!task} onClose={busy ? undefined : onClose} fullWidth maxWidth="sm">
      <DialogTitle>Hoàn thành: {task?.taskName}</DialogTitle>
      <DialogContent>
        <Typography color="text.secondary" sx={{ mb: 2 }}>
          {task?.photoRequired ? "Công việc này bắt buộc 1–3 ảnh minh chứng." : "Ảnh minh chứng không bắt buộc (tối đa 3 ảnh)."}
        </Typography>
        <Button component="label" variant="outlined" startIcon={<PhotoCameraIcon />}>
          Chụp hoặc chọn ảnh
          <input hidden type="file" accept="image/jpeg,image/png" capture="environment" multiple data-field="proofPhotos"
            onChange={(e) => { setFiles(Array.from(e.target.files ?? [])); setTouched(true); }} />
        </Button>
        <Stack direction="row" spacing={1} sx={{ mt: 2, flexWrap: "wrap", gap: 1 }}>
          {files.map((f) => (
            <Box key={f.name} component="img" src={URL.createObjectURL(f)} alt={f.name} sx={{ width: 96, height: 96, objectFit: "cover", borderRadius: 1 }} />
          ))}
        </Stack>
        {touched && error && <Alert severity="error" sx={{ mt: 2 }}>{error}</Alert>}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={busy}>Hủy</Button>
        <Button variant="contained" disabled={busy || !!error} onClick={() => onComplete(files)} data-action="completeTask">Hoàn thành</Button>
      </DialogActions>
    </Dialog>
  );
}
