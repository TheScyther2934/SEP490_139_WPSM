import { useEffect } from "react";
import { Box, Dialog, IconButton, Stack, Typography } from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import ChevronLeftIcon from "@mui/icons-material/ChevronLeft";
import ChevronRightIcon from "@mui/icons-material/ChevronRight";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";
import { dateTimeVi } from "../../../../utils/format";

// Full-size view of the proof photos of one task; arrows (or keyboard ←/→) move between them.
export default function PhotoViewer({ viewer, onClose, onMove }) {
  const photos = viewer?.photos ?? [];
  const index = viewer?.index ?? 0;
  const photo = photos[index];
  const go = (step) => onMove((index + step + photos.length) % photos.length);

  useEffect(() => {
    if (!viewer || photos.length < 2) return undefined;
    const onKey = (e) => {
      if (e.key === "ArrowLeft") onMove((index - 1 + photos.length) % photos.length);
      if (e.key === "ArrowRight") onMove((index + 1) % photos.length);
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [viewer, index, photos.length, onMove]);

  return (
    <Dialog open={!!photo} onClose={onClose} maxWidth="lg" fullWidth slotProps={{ paper: { sx: { bgcolor: "grey.900", color: "common.white" } } }}>
      {photo && (
        <>
          <Stack direction="row" sx={{ px: 2, py: 1, alignItems: "center", gap: 1 }}>
            <Box sx={{ flex: 1, minWidth: 0 }}>
              <Typography fontWeight={600} noWrap>{viewer.title}</Typography>
              <Typography variant="caption" sx={{ opacity: 0.8 }}>
                Ảnh {index + 1}/{photos.length} · {dateTimeVi(photo.uploadedAt)}{photo.uploaderName ? ` · ${photo.uploaderName}` : ""}
              </Typography>
            </Box>
            <IconButton component="a" href={photo.imageUrl} target="_blank" rel="noopener" aria-label="Mở ảnh gốc" sx={{ color: "inherit" }}><OpenInNewIcon /></IconButton>
            <IconButton onClick={onClose} aria-label="Đóng" sx={{ color: "inherit" }}><CloseIcon /></IconButton>
          </Stack>
          <Box sx={{ position: "relative", display: "flex", justifyContent: "center", alignItems: "center", minHeight: 300, bgcolor: "common.black" }}>
            <Box component="img" src={photo.imageUrl} alt={viewer.title} sx={{ maxWidth: "100%", maxHeight: "75vh", objectFit: "contain", display: "block" }} />
            {photos.length > 1 && (
              <>
                <IconButton onClick={() => go(-1)} aria-label="Ảnh trước"
                  sx={{ position: "absolute", left: 8, color: "common.white", bgcolor: "rgba(0,0,0,0.4)", "&:hover": { bgcolor: "rgba(0,0,0,0.6)" } }}>
                  <ChevronLeftIcon />
                </IconButton>
                <IconButton onClick={() => go(1)} aria-label="Ảnh sau"
                  sx={{ position: "absolute", right: 8, color: "common.white", bgcolor: "rgba(0,0,0,0.4)", "&:hover": { bgcolor: "rgba(0,0,0,0.6)" } }}>
                  <ChevronRightIcon />
                </IconButton>
              </>
            )}
          </Box>
        </>
      )}
    </Dialog>
  );
}
