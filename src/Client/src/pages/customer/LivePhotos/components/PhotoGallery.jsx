import { Box, Typography } from "@mui/material";
import PhotoCard from "../../../../components/PhotoCard";
import { dateVi } from "../../../../utils/format";

// Grid of the event photos; the API currently returns only the latest one (C-11 gallery API pending).
export default function PhotoGallery({ photos }) {
  if (!photos.length)
    return <Typography color="text.secondary" sx={{ py: 4, textAlign: "center" }}>Ảnh sẽ xuất hiện tại đây khi Ban điều phối tải lên hình ảnh thực tế.</Typography>;
  return (
    <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: "repeat(auto-fill, minmax(240px, 1fr))" }}>
      {photos.map((p) => <PhotoCard key={p.photoId} photo={p} height={220} title="Ảnh chuẩn bị hôn lễ" subtitle={`${p.uploaderName} · ${dateVi(p.uploadedAt)}`} />)}
    </Box>
  );
}
