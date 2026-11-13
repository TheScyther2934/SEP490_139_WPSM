import { Link as RouterLink } from "react-router-dom";
import { Box, Button, Card, CardContent, Stack, Typography } from "@mui/material";
import PhotoCard from "../../../../components/PhotoCard";
import { PATHS } from "../../../../routes/paths";

export default function LatestPhotos({ photos }) {
  return (
    <Card>
      <CardContent>
        <Stack direction="row" sx={{ alignItems: "center", justifyContent: "space-between", mb: 1.5 }}>
          <Typography variant="h6">Ảnh minh chứng mới nhất</Typography>
          <Button size="small" component={RouterLink} to={PATHS.admin.photoFeed}>Mở nguồn ảnh</Button>
        </Stack>
        {photos.length === 0 ? (
          <Typography color="text.secondary">Chưa có ảnh minh chứng. Ảnh xuất hiện khi quản lý tải lên trong ngày cưới.</Typography>
        ) : (
          <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { xs: "1fr 1fr", md: "repeat(5, 1fr)" } }}>
            {photos.map((p) => <PhotoCard key={p.photoId} photo={p} height={130} to={PATHS.admin.photo(p.photoId)} />)}
          </Box>
        )}
      </CardContent>
    </Card>
  );
}
