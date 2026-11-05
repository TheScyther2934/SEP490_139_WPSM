import { Alert, Box, Button, CircularProgress, Typography } from "@mui/material";

// Loading / error / empty wrapper used by every screen; children render only when data is ready.
export default function DataState({ loading, error, empty, emptyText = "Không có dữ liệu.", onRetry, children }) {
  if (loading)
    return (
      <Box sx={{ display: "flex", justifyContent: "center", py: 6 }}>
        <CircularProgress aria-label="Đang tải" />
      </Box>
    );
  if (error)
    return (
      <Alert severity="error" action={onRetry && <Button color="inherit" size="small" onClick={() => onRetry()}>Thử lại</Button>}>
        {error}
      </Alert>
    );
  if (empty)
    return (
      <Typography color="text.secondary" sx={{ py: 3, textAlign: "center" }}>
        {emptyText}
      </Typography>
    );
  return children;
}
