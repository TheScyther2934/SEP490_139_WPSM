import { Box, Card, CardContent, Typography } from "@mui/material";
import PhotoCard from "../../../../components/PhotoCard";
import { PATHS } from "../../../../routes/paths";

export default function LatestPhotos({ photos }) {
  return (
    <Card sx={{ height: "100%" }}>
      <CardContent>
        <Typography variant="h6" gutterBottom>Ảnh minh chứng mới nhất</Typography>
        {photos.length === 0 ? (
          <Typography color="text.secondary">Chưa có ảnh minh chứng.</Typography>
        ) : (
          <Box sx={{ display: "grid", gap: 1.5, gridTemplateColumns: "repeat(auto-fill, minmax(150px, 1fr))" }}>
            {photos.map((p) => <PhotoCard key={p.photoId} photo={p} height={110} to={PATHS.admin.photo(p.photoId)} />)}
          </Box>
        )}
      </CardContent>
    </Card>
  );
}
