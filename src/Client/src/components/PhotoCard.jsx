import { useState } from "react";
import { Link as RouterLink } from "react-router-dom";
import { Box, Card, CardActionArea, CardContent, Typography } from "@mui/material";
import ImageNotSupportedIcon from "@mui/icons-material/ImageNotSupported";
import { timeVi } from "../utils/format";

// Proof photo tile. `photo.imageUrl` is a short-lived signed URL from the API: <img> cannot send the Bearer token.
export default function PhotoCard({ photo, to, height = 160, title, subtitle }) {
  const [broken, setBroken] = useState(false);
  const content = (
    <>
      {broken || !photo.imageUrl ? (
        <Box sx={{ height, display: "flex", alignItems: "center", justifyContent: "center", bgcolor: "action.hover" }}>
          <ImageNotSupportedIcon color="disabled" fontSize="large" />
        </Box>
      ) : (
        <Box component="img" loading="lazy" src={photo.imageUrl} alt={title ?? photo.taskName ?? "Ảnh minh chứng"}
          onError={() => setBroken(true)} sx={{ width: "100%", height, objectFit: "cover", display: "block" }} />
      )}
      <CardContent sx={{ py: 1.5 }}>
        <Typography variant="body2" fontWeight={600} noWrap>{title ?? `${photo.projectCode} · ${photo.taskName}`}</Typography>
        <Typography variant="caption" color="text.secondary">{subtitle ?? `${timeVi(photo.uploadedAt)} · ${photo.uploaderName}`}</Typography>
      </CardContent>
    </>
  );
  return <Card>{to ? <CardActionArea component={RouterLink} to={to}>{content}</CardActionArea> : content}</Card>;
}
