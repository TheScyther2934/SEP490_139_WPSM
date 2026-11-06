import { useState } from "react";
import { Box, Dialog, Typography } from "@mui/material";
import PhotoCard from "../../../../components/PhotoCard";
import { dateTimeVi } from "../../../../utils/format";

// Grid of proof photos tagged with the task; a click opens the full-size viewer (C-11). Empty → MSG01.
export default function PhotoGallery({ photos }) {
  const [open, setOpen] = useState(null);
  if (!photos.length)
    return <Typography color="text.secondary" sx={{ py: 4, textAlign: "center" }}>Ảnh sẽ xuất hiện tại đây khi Ban điều phối tải lên hình ảnh thực tế.</Typography>;
  return (
    <>
      <Box id="photoGallery" sx={{ display: "grid", gap: 2, gridTemplateColumns: "repeat(auto-fill, minmax(240px, 1fr))" }}>
        {photos.map((p) => (
          <Box key={p.photoId} onClick={() => setOpen(p)} sx={{ cursor: "zoom-in" }}>
            <PhotoCard photo={p} height={220} title={p.taskName} subtitle={`${p.uploaderName} · ${dateTimeVi(p.uploadedAt)}`} />
          </Box>
        ))}
      </Box>
      <Dialog open={!!open} onClose={() => setOpen(null)} maxWidth="lg">
        {open && <Box component="img" src={open.imageUrl} alt={open.taskName} sx={{ maxWidth: "100%", maxHeight: "85vh", display: "block" }} />}
      </Dialog>
    </>
  );
}
