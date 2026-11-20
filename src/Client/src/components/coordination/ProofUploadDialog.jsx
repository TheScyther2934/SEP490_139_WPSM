import { useEffect, useMemo, useState } from "react";
import { Alert, Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, IconButton, Stack, TextField, Typography } from "@mui/material";
import PhotoCameraIcon from "@mui/icons-material/PhotoCamera";
import UploadFileIcon from "@mui/icons-material/UploadFile";
import PictureAsPdfIcon from "@mui/icons-material/PictureAsPdf";
import CloseIcon from "@mui/icons-material/Close";
import ImageIcon from "@mui/icons-material/Image";
import { FILE_ACCEPT, PHOTO_ACCEPT, canPreviewImage, isAllowedFile, isPdfFile } from "../../utils/files";

// 1–3 files per upload: photos (JPG/PNG, iPhone HEIC converted by the server) or documents (PDF, e.g. a delivery note), up to 10 MB each; the API checks again.
// `required`: completing a task that needs proof. `room`: how many files the task can still take.
export const proofError = (files, required, room = 3) => {
  const limit = Math.min(3, room);
  if (required && files.length === 0) return "Nhiệm vụ này cần ít nhất 1 ảnh hoặc tài liệu minh chứng.";
  if (files.length > limit) return limit < 3 ? `Nhiệm vụ này chỉ nhận thêm ${limit} tệp.` : "Mỗi lần tải lên tối đa 3 tệp.";
  if (files.some((f) => !isAllowedFile(f))) return "Chỉ nhận ảnh JPG, PNG, HEIC (iPhone), WebP hoặc tài liệu PDF, tối đa 10 MB mỗi tệp.";
  return "";
};

function Preview({ file, url, busy, onRemove }) {
  return (
    <Box sx={{ position: "relative" }}>
      {!canPreviewImage(file) ? (
        <Stack sx={{ width: 96, height: 96, borderRadius: 1, border: 1, borderColor: "divider", alignItems: "center", justifyContent: "center", p: 0.5 }}>
          {isPdfFile(file) ? <PictureAsPdfIcon color="error" /> : <ImageIcon color="primary" />}
          <Typography variant="caption" noWrap sx={{ maxWidth: "100%" }}>{file.name}</Typography>
        </Stack>
      ) : (
        <Box component="img" src={url} alt={file.name} sx={{ width: 96, height: 96, objectFit: "cover", borderRadius: 1, display: "block" }} />
      )}
      <IconButton size="small" aria-label="Bỏ tệp" disabled={busy} onClick={onRemove}
        sx={{ position: "absolute", top: 2, right: 2, bgcolor: "background.paper", "&:hover": { bgcolor: "background.paper" } }}>
        <CloseIcon fontSize="inherit" />
      </IconButton>
    </Box>
  );
}

// mode "complete": finish the task (proof required or optional, plus a note). mode "add": add proof to a task afterwards.
export default function ProofUploadDialog({ task, mode = "complete", maxPhotos = 6, busy, onClose, onSubmit }) {
  const [files, setFiles] = useState([]);
  const [note, setNote] = useState("");
  useEffect(() => { setFiles([]); setNote(""); }, [task, mode]);
  const previews = useMemo(() => files.map((f) => ({ file: f, url: canPreviewImage(f) ? URL.createObjectURL(f) : "" })), [files]);
  useEffect(() => () => previews.forEach((p) => p.url && URL.revokeObjectURL(p.url)), [previews]);

  const adding = mode === "add";
  const room = Math.max(0, maxPhotos - (task?.photos?.length ?? 0));
  const error = proofError(files, adding || task?.photoRequired, adding ? room : 3);
  const pick = (e) => {
    const picked = Array.from(e.target.files ?? []);
    e.target.value = "";
    setFiles((prev) => [...prev, ...picked]);
  };

  return (
    <Dialog open={!!task} onClose={busy ? undefined : onClose} fullWidth maxWidth="sm">
      <DialogTitle>{adding ? "Bổ sung minh chứng" : "Hoàn thành nhiệm vụ"}</DialogTitle>
      <DialogContent>
        <Typography fontWeight={600}>{task?.taskName}</Typography>
        <Typography color="text.secondary" sx={{ mb: 2 }}>
          {adding
            ? `Đang có ${task?.photos?.length ?? 0} minh chứng, lần này nhận thêm tối đa ${Math.min(3, room)} tệp.`
            : task?.photoRequired ? "Bắt buộc 1–3 ảnh chụp kết quả hoặc tài liệu (biên bản nghiệm thu, phiếu giao hàng)." : "Minh chứng không bắt buộc (tối đa 3 tệp)."}
        </Typography>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={1}>
          <Button component="label" variant="outlined" startIcon={<PhotoCameraIcon />} disabled={busy}>
            Chụp ảnh
            <input hidden type="file" accept={PHOTO_ACCEPT} capture="environment" multiple data-field="proofPhotos" onChange={pick} />
          </Button>
          <Button component="label" variant="outlined" startIcon={<UploadFileIcon />} disabled={busy}>
            Chọn ảnh hoặc PDF
            <input hidden type="file" accept={FILE_ACCEPT} multiple data-field="proofFiles" onChange={pick} />
          </Button>
        </Stack>
        <Stack direction="row" sx={{ mt: 2, flexWrap: "wrap", gap: 1 }}>
          {previews.map((p, i) => (
            <Preview key={`${p.file.name}-${i}`} file={p.file} url={p.url} busy={busy} onRemove={() => setFiles((prev) => prev.filter((_, j) => j !== i))} />
          ))}
        </Stack>
        {files.length > 0 && error && <Alert severity="error" sx={{ mt: 2 }}>{error}</Alert>}
        {!adding && (
          <TextField label="Ghi chú (không bắt buộc)" placeholder="Ví dụ: đã nghiệm thu với đội dựng rạp lúc 17:00" fullWidth multiline minRows={2}
            sx={{ mt: 2 }} value={note} onChange={(e) => setNote(e.target.value)} slotProps={{ htmlInput: { maxLength: 500 } }} />
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={busy}>Hủy</Button>
        <Button variant="contained" disabled={busy || !!error || (adding && files.length === 0)} onClick={() => onSubmit(files, note)}
          data-action={adding ? "addProofPhotos" : "completeTask"}>
          {busy ? "Đang tải lên..." : adding ? "Tải lên" : "Xác nhận hoàn thành"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
