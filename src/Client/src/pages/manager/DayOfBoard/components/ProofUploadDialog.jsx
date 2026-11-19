import { useEffect, useMemo, useState } from "react";
import { Alert, Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, IconButton, Stack, Typography } from "@mui/material";
import PhotoCameraIcon from "@mui/icons-material/PhotoCamera";
import CloseIcon from "@mui/icons-material/Close";

const MAX_BYTES = 10 * 1024 * 1024;

// 1–3 JPG/PNG photos up to 10 MB each per upload; checked here and again by the API.
// `required`: completing a task that needs proof. `room`: how many photos the task can still take.
export const photoError = (files, required, room = 3) => {
  const limit = Math.min(3, room);
  if (required && files.length === 0) return "Công việc này cần ít nhất 1 ảnh minh chứng.";
  if (files.length > limit) return limit < 3 ? `Công việc này chỉ nhận thêm ${limit} ảnh.` : "Mỗi lần tải lên tối đa 3 ảnh.";
  if (files.some((f) => f.size > MAX_BYTES || !/\.(jpe?g|png)$/i.test(f.name))) return "Chỉ chấp nhận ảnh JPG, PNG, tối đa 10 MB mỗi ảnh.";
  return "";
};

// mode "complete": finish the task (photos required or optional). mode "add": add proof photos to a task afterwards.
export default function ProofUploadDialog({ task, mode = "complete", maxPhotos = 6, busy, onClose, onSubmit }) {
  const [files, setFiles] = useState([]);
  useEffect(() => { setFiles([]); }, [task, mode]);
  const previews = useMemo(() => files.map((f) => ({ file: f, url: URL.createObjectURL(f) })), [files]);
  useEffect(() => () => previews.forEach((p) => URL.revokeObjectURL(p.url)), [previews]);

  const adding = mode === "add";
  const room = Math.max(0, maxPhotos - (task?.photos?.length ?? 0));
  const error = photoError(files, adding || task?.photoRequired, adding ? room : 3);
  const pick = (e) => {
    const picked = Array.from(e.target.files ?? []);
    e.target.value = "";
    setFiles((prev) => [...prev, ...picked]);
  };

  return (
    <Dialog open={!!task} onClose={busy ? undefined : onClose} fullWidth maxWidth="sm">
      <DialogTitle>{adding ? "Bổ sung ảnh minh chứng" : "Hoàn thành công việc"}</DialogTitle>
      <DialogContent>
        <Typography fontWeight={600}>{task?.taskName}</Typography>
        <Typography color="text.secondary" sx={{ mb: 2 }}>
          {adding
            ? `Ảnh chụp kết quả công việc đã làm. Đang có ${task?.photos?.length ?? 0} ảnh, nhận thêm tối đa ${Math.min(3, room)} ảnh lần này.`
            : task?.photoRequired ? "Công việc này bắt buộc 1–3 ảnh minh chứng." : "Ảnh minh chứng không bắt buộc (tối đa 3 ảnh)."}
        </Typography>
        <Button component="label" variant="outlined" startIcon={<PhotoCameraIcon />} disabled={busy}>
          Chụp hoặc chọn ảnh
          <input hidden type="file" accept="image/jpeg,image/png" capture="environment" multiple data-field="proofPhotos" onChange={pick} />
        </Button>
        <Stack direction="row" sx={{ mt: 2, flexWrap: "wrap", gap: 1 }}>
          {previews.map((p, i) => (
            <Box key={p.url} sx={{ position: "relative" }}>
              <Box component="img" src={p.url} alt={p.file.name} sx={{ width: 96, height: 96, objectFit: "cover", borderRadius: 1, display: "block" }} />
              <IconButton size="small" aria-label="Bỏ ảnh" disabled={busy} onClick={() => setFiles((prev) => prev.filter((_, j) => j !== i))}
                sx={{ position: "absolute", top: 2, right: 2, bgcolor: "background.paper", "&:hover": { bgcolor: "background.paper" } }}>
                <CloseIcon fontSize="inherit" />
              </IconButton>
            </Box>
          ))}
        </Stack>
        {files.length > 0 && error && <Alert severity="error" sx={{ mt: 2 }}>{error}</Alert>}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={busy}>Hủy</Button>
        <Button variant="contained" disabled={busy || !!error || (adding && files.length === 0)} onClick={() => onSubmit(files)}
          data-action={adding ? "addProofPhotos" : "completeTask"}>
          {busy ? "Đang tải lên..." : adding ? "Tải ảnh lên" : "Xác nhận hoàn thành"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
